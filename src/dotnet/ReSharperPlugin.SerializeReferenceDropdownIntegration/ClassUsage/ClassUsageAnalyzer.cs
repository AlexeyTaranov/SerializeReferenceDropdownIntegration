using System;
using System.Linq;
using JetBrains.ReSharper.Daemon.CodeInsights;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Util;
using JetBrains.Rider.Model;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Extensions;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.ProjectDetector;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ClassUsage;

//TODO: Usage is not problem. Need only highlight on non-zero references count
[ElementProblemAnalyzer(typeof(IClassDeclaration))]
public class ClassUsageAnalyzer : ElementProblemAnalyzer<IClassDeclaration>
{
    private readonly ClassUsageInsightsProvider codeInsightsProvider;
    private readonly ReferencesCountDatabase countDatabase;
    private readonly PluginDiagnostics diagnostics;
    private readonly PluginSessionSettings sessionSettings;

    public ClassUsageAnalyzer(ClassUsageInsightsProvider codeInsightsProvider,
        ReferencesCountDatabase assetsSerializeReferencesCountDatabase,
        PluginDiagnostics diagnostics,
        PluginSessionSettings sessionSettings)
    {
        this.codeInsightsProvider = codeInsightsProvider;
        this.countDatabase = assetsSerializeReferencesCountDatabase;
        this.diagnostics = diagnostics;
        this.sessionSettings = sessionSettings;
    }

    protected override void Run(IClassDeclaration element, ElementProblemAnalyzerData data,
        IHighlightingConsumer consumer)
    {
        try
        {
            if (!sessionSettings.ShowUsageCountCodeVision)
            {
                return;
            }

            if (element.DeclaredElement?.IsFromUnityProject() != true)
            {
                return;
            }

            var nonReferenceType = element.IsStatic || element.IsAbstract;
            if (nonReferenceType)
            {
                return;
            }

            var superClassNames = element.DeclaredElement.GetAllSuperClasses().Select(t => t.GetClrName()).ToArray();
            var inheritedFromUnityObject = superClassNames.Any(t => t.FullName == "UnityEngine.Object");
            if (inheritedFromUnityObject)
            {
                return;
            }

            var noParentClass = superClassNames.Length == 1 && superClassNames.First().FullName == "System.Object";

            var anySuperClass = element.SuperTypeUsageNodes.Any();
            if (noParentClass && anySuperClass == false)
            {
                return;
            }

            var dbState = countDatabase.CurrentState.Value;

            var displayText = $"{Names.SRDShort}: assets not analyzed";
            var tooltip = "Unity asset files were not analyzed yet. Refresh the usage database to show SerializeReference usages.";
            if (dbState == ReferencesCountDatabase.DatabaseState.Refreshing)
            {
                displayText = $"{Names.SRDShort}: analyzing assets...";
                tooltip = "SerializeReference usage database is being refreshed.";
            }
            else if (dbState == ReferencesCountDatabase.DatabaseState.Filled)
            {
                var unityType = element.ExtractUnityTypeFromClassDeclaration();
                var usageCount = countDatabase.GetUsagesCount(unityType);
                if (usageCount == 0 && sessionSettings.HideZeroUsageCountCodeVision)
                {
                    return;
                }

                displayText = $"{Names.SRDShort}: {usageCount} usages";
                tooltip = $"SerializeReferenceDropdown: '{unityType.ClassName}' {usageCount} - usages in project";
            }

            consumer.AddHighlighting(
                new CodeInsightsHighlighting(
                    element.GetNameDocumentRange(),
                    displayText: displayText,
                    tooltipText: tooltip,
                    moreText: "Refresh usages",
                    codeInsightsProvider,
                    element.DeclaredElement,
                    null,
                    new System.Collections.Generic.List<CodeVisionEntryExtraActionModel>
                    {
                        new CodeVisionEntryExtraActionModel(
                            ClassUsageInsightsProvider.RefreshUsageDatabaseActionId,
                            "Refresh usage database")
                    }));
        }
        catch (Exception e)
        {
            diagnostics.Error($"Failed to compute class usage insight for '{element.DeclaredName}'.", e);
        }
    }
}
