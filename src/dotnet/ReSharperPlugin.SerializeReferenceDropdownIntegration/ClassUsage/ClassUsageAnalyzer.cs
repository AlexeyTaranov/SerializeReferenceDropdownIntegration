using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Application.DataContext;
using JetBrains.Application.Notifications;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Daemon.CodeInsights;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Util;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ClassUsage;

[ElementProblemAnalyzer(typeof(IClassDeclaration))]
public class ClassUsageAnalyzer : ElementProblemAnalyzer<IClassDeclaration>
{
    private readonly ClassUsageInsightsProvider codeInsightsProvider;
    private readonly UserNotifications userNotifications;
    private readonly Lifetime lifetime;
    private readonly DatabaseLoader databaseLoader;

    public static readonly Dictionary<string, string> shortTypeToFullType = new();

    public ClassUsageAnalyzer(ClassUsageInsightsProvider codeInsightsProvider, UserNotifications userNotifications,
        Lifetime lifetime, IDataContext context, ISolution solution)
    {
        this.codeInsightsProvider = codeInsightsProvider;
        this.userNotifications = userNotifications;
        this.lifetime = lifetime;
        databaseLoader = new DatabaseLoader(solution, this.lifetime);
        LoadDatabase();
    }

    private async void LoadDatabase()
    {
        Log.DevInfo("Start load database");
        var result = await databaseLoader.LoadDatabase();
        Log.DevInfo($"End load database: {result}");
        if (result == LoadResult.NoError)
        {
            var body = $"Loaded - {databaseLoader.TypesCount.Count} types \n" +
                       $"Last refresh: {databaseLoader.DatabaseLastWriteTime}";

            userNotifications.CreateNotification(lifetime, NotificationSeverity.INFO,
                "SRD - Database loaded",
                body, closeAfterExecution: true);

            if ((DateTime.Now - databaseLoader.DatabaseLastWriteTime).Days > 1)
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
    }


    protected override void Run(IClassDeclaration element, ElementProblemAnalyzerData data,
        IHighlightingConsumer consumer)
    {
        databaseLoader.UpdateDatabaseBackground();
        if (databaseLoader.IsAvailableDatabase == false)
        {
            return;
        }

        var nonReferenceType = element.IsStatic || element.IsAbstract;
        if (nonReferenceType)
        {
            return;
        }

        var superClassNames = element.DeclaredElement.GetAllSuperClasses().Select(t => t.GetClrName());
        var inheritedFromUnityObject = superClassNames.Any(t => t.FullName == "UnityEngine.Object");
        if (inheritedFromUnityObject)
        {
            return;
        }

        var clrName = element.DeclaredElement.GetClrName();
        var name = clrName.FullName;
        var asmName = element.GetPsiModule().ContainingProjectModule.Name;
        var type = DatabaseLoader.MakeType(name, asmName);
        databaseLoader.TypesCount.TryGetValue(type, out var usageCount);
        shortTypeToFullType[clrName.ShortName] = type;

        var tooltip = $"SerializeReferenceDropdown: '{clrName.ShortName}' {usageCount} - usages in project";
        consumer.AddHighlighting(
            new CodeInsightsHighlighting(
                element.GetNameDocumentRange(),
                displayText: $"SRD: {usageCount} usages",
                tooltipText: tooltip,
                moreText: String.Empty,
                codeInsightsProvider,
                element.DeclaredElement, null));
    }
}