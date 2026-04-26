using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Application.Parts;
using JetBrains.Application.Threading;
using JetBrains.DataFlow;
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
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ClassUsage;

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public class ClassUsageInsightsProvider : ICodeInsightsProvider
{
    private readonly Lifetime lifetime;
    private readonly IShellLocks shellLocks;
    private readonly BackgroundProgressManager backgroundProgressManager;
    private readonly UnityAssetReferenceScanner scanner;
    private readonly PluginDiagnostics diagnostics;

    public ClassUsageInsightsProvider(Lifetime lifetime,
        IShellLocks shellLocks,
        BackgroundProgressManager backgroundProgressManager,
        UnityAssetReferenceScanner scanner,
        PluginDiagnostics diagnostics)
    {
        this.lifetime = lifetime;
        this.shellLocks = shellLocks;
        this.backgroundProgressManager = backgroundProgressManager;
        this.scanner = scanner;
        this.diagnostics = diagnostics;
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
        ShowReferencesPreviewInBackground(solution, unityType);
    }

    private void ShowReferencesPreviewInBackground(ISolution solution, UnityTypeData unityType)
    {
        var progressProperty =
            new Property<double>($"{nameof(ShowReferencesPreviewInBackground)}::Progress", 0).EnsureNotOutside(0.0, 1.0);
        var headerProperty = new Property<string>($"{nameof(ShowReferencesPreviewInBackground)}::Header",
            $"Preparing Unity asset usage preview for {unityType.ClassName}");
        var previewLifetimeDefinition = Lifetime.Define(lifetime);

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

            var message = BuildReferencesPreview(solution, references);
            shellLocks.StartMainRead(previewLifetime, () =>
                MessageBox.ShowInfo(message, $"{Names.SRDShort}: Unity asset usages")).NoAwait();
        }
        catch (Exception exception)
        {
            diagnostics.Error($"Failed to show Unity asset usages for '{unityType.GetFullTypeName()}'.", exception);
            var previewLifetime = previewLifetimeDefinition.Lifetime;
            if (previewLifetime.IsAlive)
            {
                shellLocks.StartMainRead(previewLifetime, () =>
                    MessageBox.ShowError("Failed to scan Unity asset usages. Check Rider logs for details.",
                        Names.SRDShort)).NoAwait();
            }
        }
        finally
        {
            previewLifetimeDefinition.Terminate();
        }

    }

    private UnityTypeData GetUnityType(CodeInsightHighlightInfo highlightInfo)
    {
        var declarations = highlightInfo.CodeInsightsHighlighting.DeclaredElement.GetDeclarations();
        var classDeclaration = declarations.OfType<IClassDeclaration>().FirstOrDefault();
        return classDeclaration.ExtractUnityTypeFromClassDeclaration();
    }

    private static string BuildReferencesPreview(ISolution solution,
        IReadOnlyList<UnityAssetReferenceScanner.TypeReferenceData> references)
    {
        if (references == null)
        {
            return "Unity asset usage scan was cancelled.";
        }

        if (references.Count == 0)
        {
            return "No Unity asset files reference this type.";
        }

        const int maxFilesInPreview = 30;
        var solutionPath = solution.SolutionDirectory.FullPath;
        var totalReferences = references.Sum(reference => reference.References.Count + reference.PrefabOverrides.Count);
        var previewLines = references
            .Take(maxFilesInPreview)
            .Select(reference =>
            {
                var relativePath = GetRelativePath(solutionPath, reference.FilePath);
                var lineNumbers = reference.References
                    .Select(referenceLine => referenceLine.LineIndex + 1)
                    .Concat(reference.PrefabOverrides.Select(prefabOverride => prefabOverride.LineIndex + 1))
                    .OrderBy(lineIndex => lineIndex)
                    .ToArray();
                return $"{relativePath}: {lineNumbers.Length} references, lines {string.Join(", ", lineNumbers)}";
            });

        var preview = string.Join("\n", previewLines);
        if (references.Count > maxFilesInPreview)
        {
            preview += $"\n...and {references.Count - maxFilesInPreview} more files.";
        }

        return $"Found {totalReferences} references in {references.Count} Unity asset files.\n\n{preview}";
    }

    private static string GetRelativePath(string rootPath, string filePath)
    {
        var normalizedRoot = rootPath.TrimEnd(System.IO.Path.DirectorySeparatorChar,
            System.IO.Path.AltDirectorySeparatorChar);
        if (filePath.StartsWith(normalizedRoot))
        {
            return filePath.Substring(normalizedRoot.Length)
                .TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        }

        return filePath;
    }

    public void OnExtraActionClick(CodeInsightHighlightInfo highlightInfo, string actionId, ISolution solution)
    {
    }

    public string ProviderId => nameof(ClassUsageInsightsProvider);
    public string DisplayName => $"{Names.SRDShort}: Class Usages";
    public CodeVisionAnchorKind DefaultAnchor => CodeVisionAnchorKind.Top;

    public ICollection<CodeVisionRelativeOrdering> RelativeOrderings => new List<CodeVisionRelativeOrdering>()
        { new CodeVisionRelativeOrderingFirst() };
}
