using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using JetBrains.Util;
using Newtonsoft.Json.Linq;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration;

public enum LoadResult
{
    NoError,
    NoDatabaseFile,
    NoSRDPackage,
    ErrorLoading
}

public class DatabaseLoader
{
    private readonly string databaseJsonName = "SerializeReference_ToolSearch_DataCacheFile.json";
    private readonly ISolution solution;
    private readonly Lifetime lifetime;

    private readonly ConcurrentDictionary<string, int> typesCount = new();

    private static bool isRunningUpdate;
    private DateTime lastDatabaseUpdate;

    public DatabaseLoader(ISolution solution, Lifetime lifetime)
    {
        this.solution = solution;
        this.lifetime = lifetime;
    }

    public IReadOnlyDictionary<string, int> TypesCount => typesCount;
    public bool IsAvailableDatabase { get; private set; }
    public DateTime LastDatabaseUpdate => lastDatabaseUpdate;

    public async Task<LoadResult> LoadDatabase()
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
            var type = MakeType(array[0], array[1]);
            typesCount.TryGetValue(type, out var value);
            value++;
            typesCount[type] = value;
        }
    }

    public static string MakeType(string typeName, string asmName)
    {
        return $"{typeName},{asmName}".Replace(" ", "");
    }

    private async Task<bool> UpdateDatabaseImpl(string jsonPath)
    {
        isRunningUpdate = true;
        var ok = false;
        try
        {
            lastDatabaseUpdate = DateTime.Now;
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
    public async void UpdateDatabaseBackground()
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

    private static void FindObjectTypes(JToken token, string propertyName, ref List<string> values)
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

    private string GetDatabaseJsonPath()
    {
        var jsonPath = Path.Combine(solution.SolutionDirectory.FullPath, "Library", databaseJsonName);
        return jsonPath;
    }

    private string GetPackagesJsonPath()
    {
        var jsonPath = Path.Combine(solution.SolutionDirectory.FullPath, "Packages", "packages-lock.json");
        return jsonPath;
    }
}