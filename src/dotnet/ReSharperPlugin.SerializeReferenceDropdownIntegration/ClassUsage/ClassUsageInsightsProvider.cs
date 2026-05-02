using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Application.Parts;
using JetBrains.Application.Threading;
using JetBrains.DataFlow;
using JetBrains.IDE.UI;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using JetBrains.ProjectModel.ProjectsHost.SolutionHost.Progress;
using JetBrains.ReSharper.Daemon.CodeInsights;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.Rider.Model;
using JetBrains.Threading;
using JetBrains.Util;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Data;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Extensions;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.ToUnity;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ClassUsage;

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public class ClassUsageInsightsProvider : ICodeInsightsProvider
{
    public const string RefreshUsageDatabaseActionId = "RefreshUsageDatabase";

    private readonly Lifetime lifetime;
    private readonly IShellLocks shellLocks;
    private readonly BackgroundProgressManager backgroundProgressManager;
    private readonly ReferencesCountDatabase countDatabase;
    private readonly UnityAssetReferenceScanner scanner;
    private readonly PluginDiagnostics diagnostics;
    private readonly PluginSessionSettings sessionSettings;
    private readonly ToUnitySrdPipe toUnitySrdPipe;
    private readonly IDialogHost dialogHost;
    private readonly object previewLifetimeLock = new();
    private LifetimeDefinition currentPreviewLifetimeDefinition;

    public ClassUsageInsightsProvider(Lifetime lifetime,
        IShellLocks shellLocks,
        BackgroundProgressManager backgroundProgressManager,
        ReferencesCountDatabase countDatabase,
        UnityAssetReferenceScanner scanner,
        PluginDiagnostics diagnostics,
        PluginSessionSettings sessionSettings,
        ToUnitySrdPipe toUnitySrdPipe,
        IDialogHost dialogHost)
    {
        this.lifetime = lifetime;
        this.shellLocks = shellLocks;
        this.backgroundProgressManager = backgroundProgressManager;
        this.countDatabase = countDatabase;
        this.scanner = scanner;
        this.diagnostics = diagnostics;
        this.sessionSettings = sessionSettings;
        this.toUnitySrdPipe = toUnitySrdPipe;
        this.dialogHost = dialogHost;
    }

    public bool IsAvailableIn(ISolution solution)
    {
        return true;
    }

    public void OnClick(CodeInsightHighlightInfo highlightInfo, ISolution solution, CodeInsightsClickInfo clickInfo)
    {
        if (!scanner.TryGetUnityAssetFiles(out _))
        {
            MessageBox.ShowError("Unity Assets folder was not found for this solution.", Names.SRDShort);
            return;
        }

        var unityType = GetUnityType(highlightInfo);
        var databaseState = countDatabase.CurrentState.Value;
        if (databaseState == ReferencesCountDatabase.DatabaseState.Empty)
        {
            countDatabase.RunRefreshDatabase();
            return;
        }

        if (databaseState == ReferencesCountDatabase.DatabaseState.Refreshing)
        {
            return;
        }

        if (!sessionSettings.ShowUsagePreviewOnClick)
        {
            countDatabase.RunRefreshDatabase();
            return;
        }

        ShowReferencesPreviewInBackground(solution, unityType);
    }

    private void ShowReferencesPreviewInBackground(ISolution solution, UnityTypeData unityType)
    {
        var progressProperty =
            new Property<double>($"{nameof(ShowReferencesPreviewInBackground)}::Progress", 0).EnsureNotOutside(0.0, 1.0);
        var headerProperty = new Property<string>($"{nameof(ShowReferencesPreviewInBackground)}::Header",
            $"Preparing Unity asset usage preview for {unityType.ClassName}");
        var previewLifetimeDefinition = Lifetime.Define(lifetime);
        lock (previewLifetimeLock)
        {
            currentPreviewLifetimeDefinition?.Terminate();
            currentPreviewLifetimeDefinition = previewLifetimeDefinition;
        }

        var progress = BackgroundProgressBuilder.Create()
            .WithTitle($"{Names.SRDShort}: Unity asset usages")
            .WithHeader(headerProperty)
            .WithProgress(progressProperty)
            .AsCancelable(previewLifetimeDefinition.Terminate)
            .Build();
        backgroundProgressManager.AddNewTask(previewLifetimeDefinition.Lifetime, progress);

        shellLocks.StartBackgroundAsync(previewLifetimeDefinition.Lifetime, () =>
            FetchAndShowReferencesPreviewAsync(solution, unityType, progressProperty, headerProperty,
                previewLifetimeDefinition)).NoAwait();
    }

    private async Task FetchAndShowReferencesPreviewAsync(ISolution solution, UnityTypeData unityType,
        Property<double> progressProperty, Property<string> headerProperty,
        LifetimeDefinition previewLifetimeDefinition)
    {
        try
        {
            var previewLifetime = previewLifetimeDefinition.Lifetime;
            var references = await scanner.CollectTypeReferencesAsync(unityType,
                previewLifetime.ToCancellationToken(), progressProperty, headerProperty);

            if (previewLifetime.IsNotAlive)
            {
                return;
            }

            var preview = UnityAssetUsagePreviewBuilder.Build(solution.SolutionDirectory.FullPath,
                ToUsagePreviewReferences(references));
            await shellLocks.StartMainRead(previewLifetime, () => ShowReferencesPreview(preview));
        }
        catch (Exception exception)
        {
            diagnostics.Error($"Failed to show Unity asset usages for '{unityType.GetFullTypeName()}'.", exception);
            var previewLifetime = previewLifetimeDefinition.Lifetime;
            if (previewLifetime.IsAlive)
            {
                await shellLocks.StartMainRead(previewLifetime, () =>
                    MessageBox.ShowError("Failed to scan Unity asset usages. Check Rider logs for details.",
                        Names.SRDShort));
            }
        }
        finally
        {
            lock (previewLifetimeLock)
            {
                if (ReferenceEquals(currentPreviewLifetimeDefinition, previewLifetimeDefinition))
                {
                    currentPreviewLifetimeDefinition = null;
                }
            }

            previewLifetimeDefinition.Terminate();
        }

    }

    private UnityTypeData GetUnityType(CodeInsightHighlightInfo highlightInfo)
    {
        var declarations = highlightInfo.CodeInsightsHighlighting.DeclaredElement.GetDeclarations();
        var classDeclaration = declarations.OfType<IClassDeclaration>().FirstOrDefault();
        return classDeclaration.ExtractUnityTypeFromClassDeclaration();
    }

    private static IReadOnlyList<UnityAssetUsageReferenceData> ToUsagePreviewReferences(
        IReadOnlyList<UnityAssetReferenceScanner.TypeReferenceData> references)
    {
        return references?.Select(reference => new UnityAssetUsageReferenceData(
                reference.FilePath,
                reference.References
                    .Select(referenceLine => referenceLine.LineIndex + 1)
                    .Concat(reference.PrefabOverrides.Select(prefabOverride => prefabOverride.LineIndex + 1))
                    .ToArray()))
            .ToArray();
    }

    private void ShowReferencesPreview(UnityAssetUsagePreview preview)
    {
        var dialog = new UnityAssetUsagePreviewDialog(dialogHost, lifetime, OpenAssetInUnity);
        dialog.Show(preview);
    }

    private void OpenAssetInUnity(string assetPath)
    {
        toUnitySrdPipe.OpenUnityAsset(assetPath);
    }

    public void OnExtraActionClick(CodeInsightHighlightInfo highlightInfo, string actionId, ISolution solution)
    {
        countDatabase.RunRefreshDatabase();
    }

    public string ProviderId => nameof(ClassUsageInsightsProvider);
    public string DisplayName => $"{Names.SRDShort}: Class Usages";
    public CodeVisionAnchorKind DefaultAnchor => CodeVisionAnchorKind.Top;

    public ICollection<CodeVisionRelativeOrdering> RelativeOrderings => new List<CodeVisionRelativeOrdering>()
        { new CodeVisionRelativeOrderingFirst() };
}
