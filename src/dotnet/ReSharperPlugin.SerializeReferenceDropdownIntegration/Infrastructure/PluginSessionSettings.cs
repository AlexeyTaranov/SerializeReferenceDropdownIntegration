using JetBrains.Application.Parts;
using JetBrains.ProjectModel;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename.ModifyUnityAsset;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename.MovedFrom;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public class PluginSessionSettings
{
    public bool? NeedOpenSearchTool { get; set; }
    public bool? NeedSwitchToUnityApp { get; set; }

    public ModifyYamlShowBehaviour ModifyYamlShowBehaviour { get; set; } = ModifyYamlShowBehaviour.ShowAlways;

    public MovedFromRefactoringSettings MovedFromRefactoringSettings { get; set; } =
        MovedFromRefactoringSettings.ShowPopup;
}
