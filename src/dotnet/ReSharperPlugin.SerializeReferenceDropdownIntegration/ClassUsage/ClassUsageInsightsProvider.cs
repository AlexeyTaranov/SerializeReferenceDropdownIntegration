using System.Collections.Generic;
using JetBrains.Application.Parts;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Daemon.CodeInsights;
using JetBrains.Rider.Model;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.ToUnity;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ClassUsage;

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public class ClassUsageInsightsProvider : ICodeInsightsProvider
{
    private readonly ToUnitySrdPipe toUnitySrdPipe;
    private readonly ToUnityWindowFocusSwitch toUnityWindowFocusSwitch;

    public ClassUsageInsightsProvider(ToUnitySrdPipe toUnitySrdPipe,
        ToUnityWindowFocusSwitch toUnityWindowFocusSwitch)
    {
        this.toUnitySrdPipe = toUnitySrdPipe;
        this.toUnityWindowFocusSwitch = toUnityWindowFocusSwitch;
    }

    public bool IsAvailableIn(ISolution solution)
    {
        return true;
    }

    public void OnClick(CodeInsightHighlightInfo highlightInfo, ISolution solution, CodeInsightsClickInfo clickInfo)
    {
        var typeName = GetFullTypeName(highlightInfo);
        toUnitySrdPipe.OpenUnitySearchToolWindowWithType(typeName);
        toUnityWindowFocusSwitch.SwitchToUnityApplication();
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