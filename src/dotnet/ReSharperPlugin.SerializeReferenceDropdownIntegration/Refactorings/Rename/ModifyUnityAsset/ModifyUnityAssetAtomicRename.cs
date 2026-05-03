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
    private readonly UnityAssetReferenceDocumentWriter documentWriter;
    private readonly PluginSessionSettings sessionSettings;
    private readonly PluginDiagnostics diagnostics;
    private readonly UnityTypeData? oldType;
    private readonly string oldNamespace;
    private readonly string newNamespace;
    private ModifyUnityAssetModel model;

    public ModifyUnityAssetAtomicRename(IDeclaredElement declaredElement, string newName,
        UnityAssetReferenceScanner scanner, UnityAssetReferenceDocumentWriter documentWriter,
        PluginSessionSettings sessionSettings, PluginDiagnostics diagnostics)
    {
        myPointer = declaredElement.CreateElementPointer();
        this.scanner = scanner;
        this.documentWriter = documentWriter;
        this.sessionSettings = sessionSettings;
        this.diagnostics = diagnostics;
        NewName = newName;
        OldName = declaredElement.ShortName;
        oldType = ExtractCurrentType(declaredElement as ITypeMember);
        oldNamespace = ExtractCurrentNamespace(declaredElement as INamespace);
        newNamespace = BuildNewNamespace(oldNamespace, newName);
    }

    public override IRefactoringPage CreateRenamesConfirmationPage(IRenameWorkflow renameWorkflow,
        IProgressIndicator pi)
    {
        if (sessionSettings.ModifyYamlShowBehaviour == ModifyYamlShowBehaviour.DontShow)
        {
            return null;
        }

        model = CreateModel();
        if (model == null)
        {
            return null;
        }

        return new ModifyUnityAssetRefactoringPage(((RefactoringWorkflowBase)renameWorkflow).WorkflowExecuterLifetime,
            model, sessionSettings, diagnostics);
    }

    private UnityTypeData? ExtractCurrentType(ITypeMember typeMember)
    {
        var declaration = GetDeclaration(typeMember);
        var classDeclaration = declaration as IClassDeclaration;
        return classDeclaration?.ExtractUnityTypeFromClassDeclaration();
    }

    private IClassMemberDeclaration GetDeclaration(ITypeMember typeMember)
    {
        var declarations = typeMember?.GetDeclarations();
        if (declarations?.Count == 1)
            return declarations[0] as IClassMemberDeclaration;
        return null;
    }

    private static string ExtractCurrentNamespace(INamespace namespaceElement)
    {
        if (namespaceElement == null || namespaceElement.IsRootNamespace)
        {
            return null;
        }

        return namespaceElement.QualifiedName;
    }

    private static string BuildNewNamespace(string oldQualifiedNamespace, string newName)
    {
        if (string.IsNullOrEmpty(oldQualifiedNamespace) || string.IsNullOrEmpty(newName))
        {
            return null;
        }

        if (newName.Contains("."))
        {
            return newName;
        }

        var lastDot = oldQualifiedNamespace.LastIndexOf('.');
        return lastDot < 0
            ? newName
            : $"{oldQualifiedNamespace.Substring(0, lastDot + 1)}{newName}";
    }

    private ModifyUnityAssetModel CreateModel()
    {
        if (oldType != null)
        {
            var newType = oldType.Value with { ClassName = NewName };
            return new ModifyUnityAssetModel(oldType.Value, newType, scanner, documentWriter, diagnostics);
        }

        if (!string.IsNullOrEmpty(oldNamespace) && !string.IsNullOrEmpty(newNamespace))
        {
            return ModifyUnityAssetModel.CreateNamespaceRenameModel(
                oldNamespace,
                newNamespace,
                scanner,
                documentWriter,
                diagnostics);
        }

        return null;
    }

    public override void Rename(IRenameRefactoring executer, IProgressIndicator pi, bool hasConflictsWithDeclarations,
        IRefactoringDriver driver, PreviousAtomicRenames previousAtomicRenames)
    {
        if (sessionSettings.ModifyYamlShowBehaviour == ModifyYamlShowBehaviour.DontShow)
        {
            return;
        }

        model ??= CreateModel();
        if (model == null)
        {
            return;
        }

        try
        {
            model.ModifyAllFilesAsync().GetAwaiter().GetResult();
        }
        catch (System.Exception exception)
        {
            model.LogModificationFailure(exception);
        }
    }

    public override IDeclaredElement NewDeclaredElement => myPointer.FindDeclaredElement();
    public override string NewName { get; }
    public override string OldName { get; }
    public override IDeclaredElement PrimaryDeclaredElement => myPointer.FindDeclaredElement().NotNull();
    public override IList<IDeclaredElement> SecondaryDeclaredElements => null;
}
