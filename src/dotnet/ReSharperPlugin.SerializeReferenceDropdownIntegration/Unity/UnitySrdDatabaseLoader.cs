using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Application.Notifications;
using JetBrains.Application.Parts;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using JetBrains.Util;
using Newtonsoft.Json.Linq;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration;

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public class UnitySrdDatabaseLoader
{
    private enum LoadResult
    {
        NoError,
        NoDatabaseFile,
        NoSRDPackage,
        ErrorLoading
    }

    private const string DatabaseJsonName = "SerializeReference_ToolSearch_DataCacheFile.json";

    private readonly UserNotifications userNotifications;
    private readonly Lifetime lifetime;
    private readonly ISolution solution;

    private readonly ConcurrentDictionary<string, int> typesCount = new();

    private DateTime lastDatabaseUpdate;
    private DateTime lastDatabaseWriteTime;
    private bool isRunningUpdate;

    public IReadOnlyDictionary<string, int> TypesCount => typesCount;
    public bool IsAvailableDatabase { get; private set; }

    public UnitySrdDatabaseLoader(UserNotifications userNotifications, Lifetime lifetime, ISolution solution,
        UnityProjectDetector unityProjectDetector)
    {
        this.userNotifications = userNotifications;
        this.lifetime = lifetime;
        this.solution = solution;
        if (unityProjectDetector.IsUnityProject())
        {
            LoadDatabase();
        }
    }

    private string GetDatabaseJsonPath()
    {
        var jsonPath = Path.Combine(solution.SolutionDirectory.FullPath, "Library", DatabaseJsonName);
        return jsonPath;
    }

    private string GetPackagesJsonPath()
    {
        var jsonPath = Path.Combine(solution.SolutionDirectory.FullPath, "Packages", "packages-lock.json");
        return jsonPath;
    }

    private async void LoadDatabase()
    {
        Log.DevInfo("Start load database");
        var result = await LoadDatabaseImpl();
        if (result == LoadResult.NoError)
        {
            var body = $"Loaded - {TypesCount.Count} types \n" +
                       $"Last refresh: {lastDatabaseWriteTime}";

            userNotifications.CreateNotification(lifetime, NotificationSeverity.INFO,
                "SRD - Database loaded",
                body, closeAfterExecution: true);

            if ((DateTime.Now - lastDatabaseWriteTime).Days > 1)
            {
                userNotifications.CreateNotification(lifetime, NotificationSeverity.WARNING,
                    "SRD - Database need refresh?",
                    body, closeAfterExecution: true);
            }
        }

        if (result == LoadResult.NoDatabaseFile)
        {
            userNotifications.CreateNotification(lifetime, NotificationSeverity.WARNING,
                "SRD - No Database File",
                "Need generate database file", closeAfterExecution: true);
        }

        Log.DevInfo($"End load database: {result}");
    }


    private async Task<LoadResult> LoadDatabaseImpl()
    {
        var jsonPath = GetDatabaseJsonPath();
        if (File.Exists(jsonPath) == false)
        {
            var packagesJson = GetPackagesJsonPath();
            if (File.Exists(packagesJson))
            {
                var packagesData = File.ReadAllText(packagesJson);
                var isHavePackage = packagesData.Contains("com.alexeytaranov.serializereferencedropdown");
                return isHavePackage ? LoadResult.NoDatabaseFile : LoadResult.NoSRDPackage;
            }

            return LoadResult.NoSRDPackage;
        }

        var result = await UpdateDatabaseImpl(jsonPath);
        if (result)
        {
            IsAvailableDatabase = true;
        }

        return result ? LoadResult.NoError : LoadResult.ErrorLoading;
    }

    private void FillTypesFromPath(string path)
    {
        var allTypes = new List<string>();
        var json = File.ReadAllText(path);
        var root = JToken.Parse(json);
        FindObjectTypes(root, "objectType", ref allTypes);

        typesCount.Clear();
        foreach (var allType in allTypes)
        {
            var array = allType.Split(',');
            var type = TypeExtensions.MakeType(array[0], array[1]);
            typesCount.TryGetValue(type, out var value);
            value++;
            typesCount[type] = value;
        }
    }

    private async Task<bool> UpdateDatabaseImpl(string jsonPath)
    {
        isRunningUpdate = true;
        var ok = false;
        try
        {
            lastDatabaseUpdate = DateTime.Now;
            lastDatabaseWriteTime = File.GetLastWriteTime(jsonPath);
            await Task.Run(() => FillTypesFromPath(jsonPath), lifetime);
            ok = true;
        }
        catch (Exception e)
        {
            MessageBox.ShowInfo($"[SRD] Failed load database", $"{e}");
        }

        isRunningUpdate = false;
        return ok;
    }


    //TODO: make better bg update
    public async void RefreshDatabase()
    {
        var jsonPath = GetDatabaseJsonPath();
        if (File.Exists(jsonPath))
        {
            var writeTime = File.GetLastWriteTime(jsonPath);
            if (writeTime > lastDatabaseUpdate && isRunningUpdate == false)
            {
                Log.DevInfo("New Database version");
                var result = await UpdateDatabaseImpl(jsonPath);
                if (IsAvailableDatabase == false && result)
                {
                    IsAvailableDatabase = true;
                }
            }
        }
    }

    private void FindObjectTypes(JToken token, string propertyName, ref List<string> values)
    {
        if (token.Type == JTokenType.Object)
        {
            foreach (var prop in ((JObject)token).Properties())
            {
                if (prop.Name == propertyName)
                {
                    values.Add(prop.Value.ToString());
                }

                FindObjectTypes(prop.Value, propertyName, ref values);
            }
        }
        else if (token.Type == JTokenType.Array)
        {
            foreach (var item in (JArray)token)
            {
                FindObjectTypes(item, propertyName, ref values);
            }
        }
    }
}