using System;
using System.Linq;
using JetBrains.Lifetimes;
using JetBrains.ReSharper.Daemon.CodeInsights;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Util;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Extensions;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.ProjectDetector;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ClassUsage;

//TODO Move from problemAnalyzer to something else where usages works normally
[ElementProblemAnalyzer(typeof(IClassDeclaration))]
public class ClassUsageAnalyzer : ElementProblemAnalyzer<IClassDeclaration>
{
    private readonly ClassUsageInsightsProvider codeInsightsProvider;
    private readonly ReferencesCountDatabase countDatabase;
    private readonly UnityProjectDetector unityProjectDetector;
    private readonly Lifetime lifetime;

    public ClassUsageAnalyzer(ClassUsageInsightsProvider codeInsightsProvider,
        ReferencesCountDatabase assetsSerializeReferencesCountDatabase,
        UnityProjectDetector unityProjectDetector,
        Lifetime lifetime)
    {
        this.codeInsightsProvider = codeInsightsProvider;
        this.countDatabase = assetsSerializeReferencesCountDatabase;
        this.unityProjectDetector = unityProjectDetector;
        this.lifetime = lifetime;
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

            if (countDatabase.CurrentState.Value ==
                ReferencesCountDatabase.DatabaseState.Refreshing)
            {
                countDatabase.CurrentState.Advise(lifetime, db =>
                {
                    if (db == ReferencesCountDatabase.DatabaseState.DatabaseFilled)
                    {
                        Run(element, data, consumer);
                    }
                });
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

            var unityType = element.ExtractUnityTypeFromClassDeclaration();
            var usageCount = countDatabase.GetUsagesCount(unityType);

            //TODO Need check usages with MovedFrom attribute
            var tooltip = $"SerializeReferenceDropdown: '{unityType.ClassName}' {usageCount} - usages in project";
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
            //
        }
    }
}