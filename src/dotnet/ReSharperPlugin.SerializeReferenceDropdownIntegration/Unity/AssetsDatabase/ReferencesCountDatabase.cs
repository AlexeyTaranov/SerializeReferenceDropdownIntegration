using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Application.Notifications;
using JetBrains.Application.Parts;
using JetBrains.Collections.Viewable;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Data;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public class ReferencesCountDatabase
{
    public enum DatabaseState
    {
        EmptyDatabase,
        Refreshing,
        DatabaseFilled
    }

    private readonly UserNotifications userNotifications;
    private readonly Lifetime lifetime;
    private readonly ISolution solution;

    private readonly ViewableProperty<DatabaseState> currentState;

    private Dictionary<UnityTypeData, int> typesCount = new();
    public IViewableProperty<DatabaseState> CurrentState => currentState;

    public ReferencesCountDatabase(UserNotifications userNotifications, Lifetime lifetime, ISolution solution)
    {
        currentState = new ViewableProperty<DatabaseState>(DatabaseState.EmptyDatabase);
        this.userNotifications = userNotifications;
        this.lifetime = lifetime;
        this.solution = solution;
    }

    public int GetUsagesCount(UnityTypeData type)
    {
        typesCount.TryGetValue(type, out var count);
        return count;
    }

    public async void RunRefreshDatabase()
    {
        if (CurrentState.Value == DatabaseState.Refreshing)
        {
            return;
        }

        currentState.Value = DatabaseState.Refreshing;

        try
        {
            var result = await Task.Run(FetchSerializeReferenceTypesCountAsync);
            typesCount = result;

            userNotifications.CreateNotification(lifetime, NotificationSeverity.INFO, "SRD - Database refreshed");
        }
        catch (Exception e)
        {
            //
        }

        currentState.Value = DatabaseState.DatabaseFilled;
    }

    private async Task<Dictionary<UnityTypeData, int>> FetchSerializeReferenceTypesCountAsync()
    {
        var typeCount = new Dictionary<UnityTypeData, int>();
        var allFiles = AssetsIterator.GetUnityFilesInAssetsFolder(solution);
        foreach (var filePath in allFiles)
        {
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