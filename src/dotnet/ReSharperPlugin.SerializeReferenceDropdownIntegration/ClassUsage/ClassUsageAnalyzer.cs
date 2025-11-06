using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.ReSharper.Daemon.CodeInsights;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Util;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.SRD;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ClassUsage;

//TODO Move from problemAnalyzer to something else where usages works normally
[ElementProblemAnalyzer(typeof(IClassDeclaration))]
public class ClassUsageAnalyzer : ElementProblemAnalyzer<IClassDeclaration>
{
    private readonly ClassUsageInsightsProvider codeInsightsProvider;
    private readonly UnitySrdDatabaseLoader unitySrdDatabaseLoader;
    private readonly UnityProjectDetector unityProjectDetector;

    public static readonly Dictionary<string, string> shortTypeToFullType = new();

    public ClassUsageAnalyzer(ClassUsageInsightsProvider codeInsightsProvider,
        UnitySrdDatabaseLoader unitySrdDatabaseLoader, UnityProjectDetector unityProjectDetector)
    {
        this.codeInsightsProvider = codeInsightsProvider;
        this.unitySrdDatabaseLoader = unitySrdDatabaseLoader;
        this.unityProjectDetector = unityProjectDetector;
    }

    protected override void Run(IClassDeclaration element, ElementProblemAnalyzerData data,
        IHighlightingConsumer consumer)
    {
        try
        {
            if (unityProjectDetector.IsUnityProject() == false)
            {
                return;
            }

            unitySrdDatabaseLoader.RefreshDatabase();
            if (unitySrdDatabaseLoader.IsAvailableDatabase == false)
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
            var type = TypeExtensions.MakeType(name, asmName);
            unitySrdDatabaseLoader.TypesCount.TryGetValue(type, out var usageCount);
            shortTypeToFullType[clrName.ShortName] = type;

            //TODO Need check usages with MovedFrom attribute
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
        catch (Exception e)
        {
            Log.DevError(e.ToString());
        }
    }
}