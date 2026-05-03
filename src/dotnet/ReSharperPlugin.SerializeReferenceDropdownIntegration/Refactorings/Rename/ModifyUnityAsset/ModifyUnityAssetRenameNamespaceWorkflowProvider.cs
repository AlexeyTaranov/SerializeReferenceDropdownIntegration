using System.Collections.Generic;
using System.Linq;
using JetBrains.Application.DataContext;
using JetBrains.ProjectModel;
using JetBrains.ProjectModel.DataContext;
using JetBrains.ReSharper.Feature.Services.ActionsMenu;
using JetBrains.ReSharper.Feature.Services.Refactorings;
using JetBrains.ReSharper.Feature.Services.Refactorings.Specific;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.DataContext;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename.ModifyUnityAsset;

[RefactoringWorkflowProvider]
public class ModifyUnityAssetRenameNamespaceWorkflowProvider : RenameWorkflowProvider, IOverridingWorkflowProvider
{
    public override IEnumerable<IRefactoringWorkflow> CreateWorkflow(IDataContext dataContext)
    {
        var solution = dataContext.GetData(ProjectModelDataConstants.SOLUTION);
        var namespaceElement = dataContext.GetData(PsiDataConstants.DECLARED_ELEMENTS)
            ?.OfType<INamespace>()
            .FirstOrDefault();
        if (solution == null || namespaceElement == null || namespaceElement.IsRootNamespace)
        {
            return [];
        }

        return [new ModifyUnityAssetRenameNamespaceWorkflow(
            solution,
            ActionId,
            namespaceElement.QualifiedName,
            solution.GetComponent<UnityAssetReferenceScanner>(),
            solution.GetComponent<UnityAssetReferenceRiderDocumentWriter>(),
            solution.GetComponent<PluginSessionSettings>(),
            solution.GetComponent<PluginDiagnostics>())];
    }

    public bool HideOtherActions(IDataContext dataContext)
    {
        return dataContext.GetData(PsiDataConstants.DECLARED_ELEMENTS)
            ?.OfType<INamespace>()
            .Any(namespaceElement => !namespaceElement.IsRootNamespace) == true;
    }
}
