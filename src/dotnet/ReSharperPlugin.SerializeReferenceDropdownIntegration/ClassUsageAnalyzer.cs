using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Application.DataContext;
using JetBrains.Application.Notifications;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Daemon.CodeInsights;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using Newtonsoft.Json.Linq;

#if RIDER
using JetBrains.Diagnostics;
using JetBrains.Lifetimes;
using JetBrains.ReSharper.Features.Altering.CodeCleanup2;
using JetBrains.Rider.Backend.Platform.Icons;
using JetBrains.Util;
#endif


namespace ReSharperPlugin.SerializeReferenceDropdownIntegration;

//TODO Resharper support?
[ElementProblemAnalyzer(typeof(IClassDeclaration))]
public class ClassUsageAnalyzer : ElementProblemAnalyzer<IClassDeclaration>
{
#if RIDER
    private readonly ClassUsageInsightsProvider codeInsightsProvider;
    private readonly UserNotifications userNotifications;
    private readonly Lifetime lifetime;
    private readonly ISolution solution;

    public ClassUsageAnalyzer(ClassUsageInsightsProvider codeInsightsProvider, UserNotifications userNotifications,
        Lifetime lifetime, IDataContext context, ISolution solution)
    {
        this.codeInsightsProvider = codeInsightsProvider;
        this.userNotifications = userNotifications;
        this.lifetime = lifetime;
        this.solution = solution;
        LoadDatabase();
    }
#endif

    private readonly string databaseJsonName = "SerializeReference_ToolSearch_DataCacheFile.txt";

    private readonly ConcurrentDictionary<string, int> typesCount = new();
    public static readonly Dictionary<string, string> shortTypeToFullType = new();

    private static bool isRunningUpdate;
    private DateTime lastDatabaseUpdate;

    private async void LoadDatabase()
    {
#if RIDER
        var jsonPath = GetJsonPath();
        if (File.Exists(jsonPath) == false)
        {
            userNotifications.CreateNotification(lifetime, NotificationSeverity.WARNING,
                "SRD - No Database File",
                "Need generate database file", closeAfterExecution: true);
        }
        else
        {
            await UpdateDatabaseImpl(jsonPath);
            var body = $"Loaded - {typesCount.Count} types \n" +
                       $"Last refresh: {lastDatabaseUpdate}";
            userNotifications.CreateNotification(lifetime, NotificationSeverity.INFO,
                "SRD - Database loaded",
                body, closeAfterExecution: true);
        }
#endif
    }

    private string GetJsonPath()
    {
#if RIDER
        var jsonPath = Path.Combine(solution.SolutionDirectory.FullPath, "Library", databaseJsonName);
        return jsonPath;
#else
        return string.Empty;
#endif
    }

    private async Task UpdateDatabaseImpl(string jsonPath)
    {
#if RIDER
        isRunningUpdate = true;
        try
        {
            lastDatabaseUpdate = DateTime.Now;
            await Task.Run(() => FillTypesFromPath(jsonPath), lifetime);
        }
        catch (Exception e)
        {
            MessageBox.ShowInfo($"Failed load database", $"{e}");
        }

        isRunningUpdate = false;
#endif
    }

    private void UpdateDatabase()
    {
        var jsonPath = GetJsonPath();
        if (File.Exists(jsonPath))
        {
            var writeTime = File.GetLastWriteTime(jsonPath);
            if (writeTime > lastDatabaseUpdate && isRunningUpdate == false)
            {
                UpdateDatabaseImpl(jsonPath);
            }
        }
    }

    public string MakeType(string typeName, string asmName) =>
        $"{typeName},{asmName}".Replace(" ", "");

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

    private static void FindObjectTypes(JToken token, string propertyName, ref List<string> values)
    {
        if (token.Type == JTokenType.Object)
        {
            foreach (var prop in ((JObject)token).Properties())
            {
                if (prop.Name == "objectType")
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

    protected override void Run(IClassDeclaration element, ElementProblemAnalyzerData data,
        IHighlightingConsumer consumer)
    {
#if RIDER
        UpdateDatabase();
        var clrName = element.DeclaredElement.GetClrName();
        var name = clrName.FullName;
        var asmName = element.GetPsiModule().ContainingProjectModule.Name;
        var type = MakeType(name, asmName);
        typesCount.TryGetValue(type, out var usageCount);
        shortTypeToFullType[clrName.ShortName] = type;

        consumer.AddHighlighting(
            new CodeInsightsHighlighting(
                element.GetNameDocumentRange(),
                displayText: $"SRD: {usageCount} usages",
                tooltipText: $"SerializeReferenceDropdown: {usageCount} - usages in project",
                moreText: String.Empty,
                codeInsightsProvider,
                element.DeclaredElement, null));
#endif
    }
}