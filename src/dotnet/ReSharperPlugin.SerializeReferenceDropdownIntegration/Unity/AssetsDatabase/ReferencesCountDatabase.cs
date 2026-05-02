using System;
using System.Collections.Generic;
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
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;

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
    private readonly UnityAssetReferenceScanner scanner;
    private readonly PluginDiagnostics diagnostics;

    private readonly ViewableProperty<DatabaseState> currentState;

    private Dictionary<UnityTypeData, int> typesCount = new();
    public IViewableProperty<DatabaseState> CurrentState => currentState;

    public ReferencesCountDatabase(Lifetime lifetime, ISolution solution, IShellLocks myShellLocks,
        BackgroundProgressManager myBackgroundProgressManager, UnityAssetReferenceScanner scanner,
        PluginDiagnostics diagnostics, PluginSessionSettings sessionSettings)
    {
        currentState = new ViewableProperty<DatabaseState>(DatabaseState.Empty);
        this.lifetime = lifetime;
        this.solution = solution;
        this.myShellLocks = myShellLocks;
        this.myBackgroundProgressManager = myBackgroundProgressManager;
        this.scanner = scanner;
        this.diagnostics = diagnostics;

        if (sessionSettings.AutoRefreshUsageCountDatabase)
        {
            RunRefreshDatabase();
        }
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
                currentState.Value = DatabaseState.Filled;
                refreshDatabaseLifetimeDefinition.Terminate();
                InvalidateSolution();
            }
            catch (Exception _)
            {
                diagnostics.Error("Failed to refresh SerializeReference database.", _);
                DropDatabase();
                refreshDatabaseLifetimeDefinition.Terminate();
                return;
                //
            }

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
        return await scanner.FetchSerializeReferenceTypesCountAsync(progress, description, cancellationToken);
    }
}
