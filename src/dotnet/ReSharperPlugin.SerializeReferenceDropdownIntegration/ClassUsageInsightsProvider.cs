using System.Collections.Generic;
using JetBrains.Application.Parts;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Daemon.CodeInsights;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Util;
using JetBrains.Rider.Model;
using JetBrains.Util;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration;

[SolutionComponent(Instantiation.ContainerAsyncPrimaryThread)]
public class ClassUsageInsightsProvider : ICodeInsightsProvider
{
    public bool IsAvailableIn(ISolution solution)
    {
        return true;
    }

    public void OnClick(CodeInsightHighlightInfo highlightInfo, ISolution solution)
    {
        var typeName = GetFullTypeName(highlightInfo);
        UnityBridge.OpenUnitySearchToolWindowWithType(typeName);
    }
    
    private string GetFullTypeName(CodeInsightHighlightInfo highlightInfo)
    {
        //Need better solution and get full names everywhere
        var shortName = highlightInfo.CodeInsightsHighlighting.DeclaredElement.ShortName;
        if (ClassUsageAnalyzer.shortTypeToFullType.TryGetValue(shortName, out var fullName))
        {
            return fullName;
        }

        return shortName;
    }

    public void OnExtraActionClick(CodeInsightHighlightInfo highlightInfo, string actionId, ISolution solution)
    {
    }

    public string ProviderId => nameof(ClassUsageInsightsProvider);
    public string DisplayName => "SRD: Class Usages";
    public CodeVisionAnchorKind DefaultAnchor => CodeVisionAnchorKind.Top;

    public ICollection<CodeVisionRelativeOrdering> RelativeOrderings => new List<CodeVisionRelativeOrdering>()
        { new CodeVisionRelativeOrderingFirst() };
}