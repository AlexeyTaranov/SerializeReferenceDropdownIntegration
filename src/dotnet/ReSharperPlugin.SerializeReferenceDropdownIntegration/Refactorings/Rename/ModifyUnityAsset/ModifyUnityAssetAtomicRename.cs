using System.Collections.Generic;
using JetBrains.Application.Progress;
using JetBrains.Diagnostics;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Refactorings;
using JetBrains.ReSharper.Feature.Services.Refactorings.Specific.Rename;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Pointers;
using JetBrains.ReSharper.Refactorings.Rename;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Data;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Extensions;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename.ModifyUnityAsset;

public class ModifyUnityAssetAtomicRename : AtomicRenameBase
{
    private readonly IDeclaredElementPointer<IDeclaredElement> myPointer;
    private readonly ISolution solution;

    public ModifyUnityAssetAtomicRename(IDeclaredElement declaredElement, ISolution solution, string newName)
    {
        myPointer = declaredElement.CreateElementPointer();
        this.solution = solution;
        NewName = newName;
    }

    public override IRefactoringPage CreateRenamesConfirmationPage(IRenameWorkflow renameWorkflow,
        IProgressIndicator pi)
    {
        if (ModifyUnityAssetRefactoringPage.ShowBehaviour == ModifyYamlShowBehaviour.DontShow)
        {
            return null;
        }

        var oldType = ExtractCurrentType();
        var newType = oldType with { ClassName = NewName };

        var implementation = new ModifyUnityAssetModel(oldType, newType, solution);
        return new ModifyUnityAssetRefactoringPage(((RefactoringWorkflowBase)renameWorkflow).WorkflowExecuterLifetime,
            implementation);
    }

    private UnityTypeData ExtractCurrentType()
    {
        var declaration = GetDeclaration(myPointer.FindDeclaredElement() as ITypeMember);
        var classDeclaration = declaration as IClassDeclaration;
        return  classDeclaration.ExtractUnityTypeFromClassDeclaration();
    }

    private IClassMemberDeclaration? GetDeclaration(ITypeMember? typeMember)
    {
        var declarations = typeMember?.GetDeclarations();
        if (declarations?.Count == 1)
            return declarations[0] as IClassMemberDeclaration;
        return null;
    }

    public override void Rename(IRenameRefactoring executer, IProgressIndicator pi, bool hasConflictsWithDeclarations,
        IRefactoringDriver driver, PreviousAtomicRenames previousAtomicRenames)
    {
        // Actually all modify yaml logic - in confirmation page
        return;
    }

    public override IDeclaredElement NewDeclaredElement => myPointer.FindDeclaredElement();
    public override string NewName { get; }
    public override string OldName { get; }
    public override IDeclaredElement PrimaryDeclaredElement => myPointer.FindDeclaredElement().NotNull();
    public override IList<IDeclaredElement> SecondaryDeclaredElements => null;
}