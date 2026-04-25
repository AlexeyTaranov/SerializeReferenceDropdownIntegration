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
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename.ModifyUnityAsset;

public class ModifyUnityAssetAtomicRename : AtomicRenameBase
{
    private readonly IDeclaredElementPointer<IDeclaredElement> myPointer;
    private readonly UnityAssetReferenceScanner scanner;
    private readonly PluginSessionSettings sessionSettings;
    private readonly PluginDiagnostics diagnostics;

    public ModifyUnityAssetAtomicRename(IDeclaredElement declaredElement, string newName,
        UnityAssetReferenceScanner scanner, PluginSessionSettings sessionSettings, PluginDiagnostics diagnostics)
    {
        myPointer = declaredElement.CreateElementPointer();
        this.scanner = scanner;
        this.sessionSettings = sessionSettings;
        this.diagnostics = diagnostics;
        NewName = newName;
        OldName = declaredElement.ShortName;
    }

    public override IRefactoringPage CreateRenamesConfirmationPage(IRenameWorkflow renameWorkflow,
        IProgressIndicator pi)
    {
        if (sessionSettings.ModifyYamlShowBehaviour == ModifyYamlShowBehaviour.DontShow)
        {
            return null;
        }

        var oldType = ExtractCurrentType();
        var newType = oldType with { ClassName = NewName };

        var implementation = new ModifyUnityAssetModel(oldType, newType, scanner, diagnostics);
        return new ModifyUnityAssetRefactoringPage(((RefactoringWorkflowBase)renameWorkflow).WorkflowExecuterLifetime,
            implementation, sessionSettings);
    }

    private UnityTypeData ExtractCurrentType()
    {
        var declaration = GetDeclaration(myPointer.FindDeclaredElement() as ITypeMember);
        var classDeclaration = declaration as IClassDeclaration;
        return  classDeclaration.ExtractUnityTypeFromClassDeclaration();
    }

    private IClassMemberDeclaration GetDeclaration(ITypeMember typeMember)
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
