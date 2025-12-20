using System.Collections.Generic;
using System.Linq;
using JetBrains.Application.Parts;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Daemon.CodeInsights;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.Rider.Model;
using JetBrains.Util;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Extensions;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.ToUnity;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ClassUsage;

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public class ClassUsageInsightsProvider : ICodeInsightsProvider
{
    private readonly ToUnitySrdPipe toUnitySrdPipe;
    private readonly ToUnityWindowFocusSwitch toUnityWindowFocusSwitch;
    private readonly ReferencesCountDatabase countDatabase;

    public ClassUsageInsightsProvider(ToUnitySrdPipe toUnitySrdPipe,
        ToUnityWindowFocusSwitch toUnityWindowFocusSwitch,
        ReferencesCountDatabase countDatabase)
    {
        this.toUnitySrdPipe = toUnitySrdPipe;
        this.toUnityWindowFocusSwitch = toUnityWindowFocusSwitch;
        this.countDatabase = countDatabase;
    }

    public bool IsAvailableIn(ISolution solution)
    {
        return true;
    }

    //TODO: On short click can show window as Unity Resharper plugin. Now its message hell on first launch =_=
    public void OnClick(CodeInsightHighlightInfo highlightInfo, ISolution solution, CodeInsightsClickInfo clickInfo)
    {
        if (countDatabase.CurrentState.Value != ReferencesCountDatabase.DatabaseState.Refreshing)
        {
            var needRefreshDB = MessageBox.ShowYesNo("Need Refresh SRD Database?");
            if (needRefreshDB)
            {
                countDatabase.RunRefreshDatabase();
            }
        }

        var typeName = GetFullTypeName(highlightInfo);
        toUnitySrdPipe.OpenUnitySearchToolWindowWithType(typeName);
        toUnityWindowFocusSwitch.SwitchToUnityApplication();
    }

    private string GetFullTypeName(CodeInsightHighlightInfo highlightInfo)
    {
        var declarations = highlightInfo.CodeInsightsHighlighting.DeclaredElement.GetDeclarations();
        var classDeclaration = declarations.OfType<IClassDeclaration>().FirstOrDefault();
        var unityType = classDeclaration.ExtractUnityTypeFromClassDeclaration();
        return unityType.GetFullTypeName();
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