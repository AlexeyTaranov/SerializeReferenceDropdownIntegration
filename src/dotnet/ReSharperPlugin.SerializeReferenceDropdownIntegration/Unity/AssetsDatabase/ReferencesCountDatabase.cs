using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Application.Parts;
using JetBrains.Application.Threading;
using JetBrains.Collections.Viewable;
using JetBrains.DataFlow;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using JetBrains.ProjectModel.ProjectsHost.SolutionHost.Progress;
using JetBrains.ReSharper.Daemon;
using JetBrains.Threading;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Data;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public class ReferencesCountDatabase
{
    public enum DatabaseState
    {
        Empty,
        Refreshing,
        Filled
    }

    private readonly Lifetime lifetime;
    private readonly ISolution solution;
    private readonly IShellLocks myShellLocks;
    private readonly BackgroundProgressManager myBackgroundProgressManager;

    private readonly ViewableProperty<DatabaseState> currentState;

    private Dictionary<UnityTypeData, int> typesCount = new();
    public IViewableProperty<DatabaseState> CurrentState => currentState;

    public ReferencesCountDatabase(Lifetime lifetime, ISolution solution, IShellLocks myShellLocks,
        BackgroundProgressManager myBackgroundProgressManager)
    {
        currentState = new ViewableProperty<DatabaseState>(DatabaseState.Empty);
        this.lifetime = lifetime;
        this.solution = solution;
        this.myShellLocks = myShellLocks;
        this.myBackgroundProgressManager = myBackgroundProgressManager;
    }

    public int GetUsagesCount(UnityTypeData type)
    {
        typesCount.TryGetValue(type, out var count);
        return count;
    }

    public void RunRefreshDatabase()
    {
        if (CurrentState.Value == DatabaseState.Refreshing)
        {
            return;
        }

        var refreshDatabaseProgressProperty =
            new Property<double>($"{nameof(RunRefreshDatabase)}::Progress", 0).EnsureNotOutside(0.0, 1.0);

        //TODO: Check how to use lifetimes and cancellationTokens
        var refreshDatabaseLifetimeDefinition = Lifetime.Define(lifetime);
        var cancellationTokenSource = new CancellationTokenSource();

        var progress = BackgroundProgressBuilder.Create()
            .WithHeader("Refresh SRD Database")
            .WithProgress(refreshDatabaseProgressProperty)
            .AsCancelable(() =>
            {
                cancellationTokenSource.Cancel();
                refreshDatabaseLifetimeDefinition.Terminate();
            })
            .Build();
        myBackgroundProgressManager.AddNewTask(refreshDatabaseLifetimeDefinition.Lifetime, progress);

        myShellLocks.StartBackgroundAsync(refreshDatabaseLifetimeDefinition.Lifetime,
            () => RefreshDatabase(refreshDatabaseProgressProperty, cancellationTokenSource.Token)).NoAwait();

        async Task RefreshDatabase(Property<double> progressProperty, CancellationToken cancellationToken)
        {
            currentState.Value = DatabaseState.Refreshing;
            try
            {
                var result = await FetchSerializeReferenceTypesCountAsync(progressProperty, cancellationToken);
                if (result == null)
                {
                    DropDatabase();
                    return;
                }

                typesCount = result;
                InvalidateSolution();
                refreshDatabaseLifetimeDefinition.Terminate();
            }
            catch (Exception _)
            {
                DropDatabase();
                refreshDatabaseLifetimeDefinition.Terminate();
                return;
                //
            }

            currentState.Value = DatabaseState.Filled;

            void DropDatabase()
            {
                currentState.Value = DatabaseState.Empty;
                typesCount.Clear();
                InvalidateSolution();
            }

            //HACK: Bad idea(( Need to find solution how to reload only ClassUsageAnalyzers
            void InvalidateSolution()
            {
                DaemonBase.GetInstance(solution).Invalidate();
            }
        }
    }

    private async Task<Dictionary<UnityTypeData, int>> FetchSerializeReferenceTypesCountAsync(Property<double> progress,
        CancellationToken cancellationToken)
    {
        var typeCount = new Dictionary<UnityTypeData, int>();
        var allFiles = AssetsIterator.GetUnityFilesInAssetsFolder(solution);

        //TODO: Parallel read files? 
        for (var i = 0; i < allFiles.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var filePath = allFiles[i];
            progress.Value = (float)(i + 1) / (float)allFiles.Count;
            await AssetsIterator.ReadReferencesBlockInUnityAsset(filePath, OnReferenceLineRead);
        }

        return typeCount;

        void OnReferenceLineRead(AssetsIterator.LineInfo lineInfo)
        {
            var match = Regex.Match(
                lineInfo.Text,
                @"class:\s*(?<class>[^,]+),\s*ns:\s*(?<ns>[^,]+),\s*asm:\s*(?<asm>[^}\s]+)"
            );

            if (match.Success)
            {
                var className = match.Groups["class"].Value;
                var ns = match.Groups["ns"].Value;
                var asm = match.Groups["asm"].Value;
                var unityTypeData = new UnityTypeData()
                {
                    ClassName = className,
                    Namespace = ns,
                    AssemblyName = asm
                };
                typeCount.TryGetValue(unityTypeData, out var count);
                typeCount[unityTypeData] = count + 1;
            }
        }
    }
}