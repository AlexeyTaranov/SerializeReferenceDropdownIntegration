using System.Collections.Generic;
using JetBrains.Application.Progress;
using JetBrains.Diagnostics;
using JetBrains.ReSharper.Feature.Services.Refactorings;
using JetBrains.ReSharper.Feature.Services.Refactorings.Specific.Rename;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Impl;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Pointers;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Refactorings.Rename;
using JetBrains.Util;
using JetBrains.Util.dataStructures;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename;

public class MovedFromAtomicRename : AtomicRenameBase
{
    private readonly KnownTypesCache myKnownTypesCache;
    private readonly IDeclaredElementPointer<IDeclaredElement> myPointer;
    private readonly MovedFromRenameModel myModel;

    public MovedFromAtomicRename(IDeclaredElement declaredElement, string newName,
        KnownTypesCache knownTypesCache)
    {
        myKnownTypesCache = knownTypesCache;
        NewName = newName;
        OldName = declaredElement.ShortName;
        myPointer = declaredElement.CreateElementPointer();
        myModel = new MovedFromRenameModel();
    }

    public override IRefactoringPage CreateRenamesConfirmationPage(IRenameWorkflow renameWorkflow,
        IProgressIndicator pi)
    {
        // hide confirmation page only, refactoring should update shared document too otherwise
        // we will get inconsistent change modification message box
        if (myModel.MovedFromRefactoringBehavior
            is MovedFromRefactoringBehavior.AddAndRemember
            or MovedFromRefactoringBehavior.DontAddAndRemember)
            return null;

        return new MovedFromRefactoringPage(
            ((RefactoringWorkflowBase)renameWorkflow).WorkflowExecuterLifetime, myModel, OldName);
    }

    public override void Rename(IRenameRefactoring executer, IProgressIndicator pi, bool hasConflictsWithDeclarations,
        IRefactoringDriver driver, PreviousAtomicRenames previousAtomicRenames)
    {
        if (myModel.MovedFromRefactoringBehavior
            is MovedFromRefactoringBehavior.DontAdd
            or MovedFromRefactoringBehavior.DontAddAndRemember)
            return;

        var classMemberDeclaration = GetDeclaration(myPointer.FindDeclaredElement() as ITypeMember);
        if (classMemberDeclaration == null)
            return;

        //TODO Ask about remove old attribute? We can't use together two or more MovedFrom attributes
        // RemoveExistingAttributesWithNewName(classMemberDeclaration);
        
        if (HasExistingMovedFromAttribute(classMemberDeclaration))
        {
            // Make sure textual occurrence rename doesn't rename the existing attribute parameter
            RemoveFromTextualOccurrences(executer, classMemberDeclaration);
            return;
        }

        //TODO Make rename source namespaces?
        var attribute = CreateMovedFromAttribute(classMemberDeclaration, oldClassName: OldName);
        if (attribute != null)
            classMemberDeclaration.AddAttributeAfter(attribute, null);
    }

    private void RemoveExistingAttributesWithNewName(IClassMemberDeclaration classMemberDeclaration)
    {
        var attributes = GetExistingFormerlySerializedAsAttributes(classMemberDeclaration, NewName);
        foreach (var attribute in attributes)
            classMemberDeclaration.RemoveAttribute(attribute);
    }

    private static IClassMemberDeclaration? GetDeclaration(ITypeMember? typeMember)
    {
        var declarations = typeMember?.GetDeclarations();
        if (declarations?.Count == 1)
            return declarations[0] as IClassMemberDeclaration;
        return null;
    }

    private bool HasExistingMovedFromAttribute(IClassMemberDeclaration classMemberDeclaration)
    {
        var attributes = GetExistingFormerlySerializedAsAttributes(classMemberDeclaration, OldName);
        return attributes.Count > 0;
    }

    private FrugalLocalList<IAttribute> GetExistingFormerlySerializedAsAttributes(
        IClassMemberDeclaration fieldDeclaration, string nameArgument)
    {
        var list = new FrugalLocalList<IAttribute>();
        foreach (var attribute in fieldDeclaration.AttributesEnumerable)
        {
            var attributeTypeElement = attribute.TypeReference?.Resolve().DeclaredElement as ITypeElement;
            if (attributeTypeElement == null)
                continue;

            if (Equals(attributeTypeElement.GetClrName(), KnownTypes.MovedFromAttribute))
            {
                var attributeInstance = attribute.GetAttributeInstance();
                var nameParameter = attributeInstance.PositionParameter(0);
                if (nameParameter.IsConstant && nameParameter.ConstantValue.IsString(out var stringValue) &&
                    stringValue == nameArgument)
                {
                    list.Add(attribute);
                }
            }
        }

        return list;
    }

    private void RemoveFromTextualOccurrences(IRenameRefactoring executor, IClassMemberDeclaration fieldDeclaration)
    {
        if (executor.Workflow is not RenameWorkflow workflow)
            return;

        var attributes = fieldDeclaration.Attributes;
        if (attributes.Count == 0)
            return;

        var attribute = attributes[0];
        var attributeSectionList = AttributeSectionListNavigator.GetByAttribute(attribute);
        if (attributeSectionList == null)
            return;

        var attributesRange = attributeSectionList.GetDocumentRange();

        foreach (var occurrence in workflow.DataModel.ActualOccurrences ??
                                   EmptyList<TextOccurrenceRenameMarker>.InstanceList)
        {
            if (!occurrence.Included)
                continue;


            var occurrenceRange = occurrence.Marker.DocumentRange;
            if (attributesRange.Contains(occurrenceRange))
            {
                occurrence.Included = false;
                break;
            }
        }
    }

    private IAttribute? CreateMovedFromAttribute(IClassMemberDeclaration owningNode,
        string oldClassName = null, string oldNamespace = null)
    {
        var module = owningNode.GetPsiModule();
        var elementFactory = CSharpElementFactory.GetInstance(owningNode);
        var attributeType = myKnownTypesCache.GetByClrTypeName(KnownTypes.MovedFromAttribute, module);
        var attributeTypeElement = attributeType.GetTypeElement();
        if (attributeTypeElement == null)
            return null;

        var movedFromArguments = new AttributeValue[]
        {
            new(ConstantValue.Bool(true, module)),
            new(ConstantValue.String(oldNamespace, module)),
            new(ConstantValue.String(null, module)),
            new(ConstantValue.String(oldClassName, module))
        };


        var movedFromAttribute = elementFactory.CreateAttribute(attributeTypeElement,
            movedFromArguments,
            EmptyArray<Pair<string, AttributeValue>>.Instance);

        return movedFromAttribute;
    }

    public override IDeclaredElement NewDeclaredElement => myPointer.FindDeclaredElement().NotNull();
    public override string NewName { get; }
    public override string OldName { get; }
    public override IDeclaredElement PrimaryDeclaredElement => myPointer.FindDeclaredElement().NotNull();
    public override IList<IDeclaredElement> SecondaryDeclaredElements => null;
}