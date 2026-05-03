using System.Collections.Generic;
using System.Linq;
using JetBrains.Application;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Refactorings.Specific.Rename;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.ReSharper.Psi.VB.Util;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.ProjectDetector;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename.ModifyUnityAsset;

[ShellFeaturePart]
public class ModifyUnityAssetAtomicRenameFactory : IAtomicRenameFactory
{
    //TODO HACK I don't know why IsApplicable() called twice(
    private static readonly List<string> renameDeclaredElements = new();

    public bool IsApplicable(IDeclaredElement declaredElement)
    {
        renameDeclaredElements.Clear();

        var isClass = declaredElement.IsClass();
        if (isClass)
        {
            return declaredElement.IsFromUnityProject();
        }

        return IsUnityNamespace(declaredElement);
    }

    public RenameAvailabilityCheckResult CheckRenameAvailability(IDeclaredElement element)
    {
        return RenameAvailabilityCheckResult.CanBeRenamed;
    }

    public IEnumerable<AtomicRenameBase> CreateAtomicRenames(IDeclaredElement declaredElement, string newName,
        bool doNotAddBindingConflicts)
    {
        var elementKey = GetElementKey(declaredElement);
        if (renameDeclaredElements.Contains(elementKey))
        {
            return [];
        }

        renameDeclaredElements.Add(elementKey);
        var solution = declaredElement.GetSolution();
        return [new ModifyUnityAssetAtomicRename(
            declaredElement,
            newName,
            solution.GetComponent<UnityAssetReferenceScanner>(),
            solution.GetComponent<UnityAssetReferenceRiderDocumentWriter>(),
            solution.GetComponent<PluginSessionSettings>(),
            solution.GetComponent<PluginDiagnostics>())];
    }

    private static string GetElementKey(IDeclaredElement declaredElement)
    {
        return declaredElement is INamespace namespaceElement
            ? $"namespace:{namespaceElement.QualifiedName}"
            : $"element:{declaredElement.ShortName}";
    }

    private static bool IsUnityNamespace(IDeclaredElement declaredElement)
    {
        if (declaredElement is not INamespace namespaceElement || namespaceElement.IsRootNamespace)
        {
            return false;
        }

        if (declaredElement.GetSourceFiles().Any(sourceFile => sourceFile.GetProject().IsUnityProject()))
        {
            return true;
        }

        return namespaceElement.Modules.Any(IsUnityPsiModule);
    }

    private static bool IsUnityPsiModule(IPsiModule psiModule)
    {
        return psiModule.ContainingProjectModule is IProject project && project.IsUnityProject();
    }
}
