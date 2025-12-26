using System;
using System.Collections.Generic;
using System.Linq;
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

        var refreshDatabaseLifetimeDefinition = Lifetime.Define(lifetime);
        var refreshDatabaseHeader =
            new Property<string>($"{nameof(RunRefreshDatabase)}::Refresh Header", String.Empty);

        var progress = BackgroundProgressBuilder.Create()
            .WithTitle(Names.SRDShort)
            .WithHeader(refreshDatabaseHeader)
            .WithProgress(refreshDatabaseProgressProperty)
            .AsCancelable(() =>
            {
                refreshDatabaseLifetimeDefinition.Terminate();
            })
            .Build();
        myBackgroundProgressManager.AddNewTask(refreshDatabaseLifetimeDefinition.Lifetime, progress);

        myShellLocks.StartBackgroundAsync(refreshDatabaseLifetimeDefinition.Lifetime,
            () => RefreshDatabase(refreshDatabaseProgressProperty,
                refreshDatabaseLifetimeDefinition.Lifetime.ToCancellationToken())).NoAwait();

        async Task RefreshDatabase(Property<double> progressProperty, CancellationToken cancellationToken)
        {
            currentState.Value = DatabaseState.Refreshing;
            try
            {
                var result = await FetchSerializeReferenceTypesCountAsync(progressProperty, refreshDatabaseHeader,
                    cancellationToken);
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
        Property<string> description,
        CancellationToken cancellationToken)
    {
        var typeCount = new Dictionary<UnityTypeData, int>();
        description.Value = "1/2: Check All Unity Files";
        var allFiles = AssetsIterator.GetUnityFilesInAssetsFolder(solution);

        var referenceTypes = new List<AssetsIterator.UnityReferenceTypeLineData>();
        var prefabOverrides = new List<AssetsIterator.UnityReferenceTypePrefabOverrideLineData>();
        //TODO: Parallel read files? 
        for (var i = 0; i < allFiles.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            description.Value = $"2/2: Read Unity files: {i}/{allFiles.Count}";
            progress.Value = (float)(i + 1) / (float)allFiles.Count;

            var filePath = allFiles[i];

            referenceTypes.Clear();
            prefabOverrides.Clear();
            await AssetsIterator.FillReferenceTypesBlocksAsync(filePath, referenceTypes, prefabOverrides);
            var allTypes = referenceTypes.Select(t => t.Type).Concat(prefabOverrides.Select(t => t.Type));
            foreach (var checkType in allTypes)
            {
                typeCount.TryGetValue(checkType, out var count);
                typeCount[checkType] = count + 1;
            }
        }

        return typeCount;
    }
}