using System.Collections.Generic;
using JetBrains.Application;
using JetBrains.ReSharper.Feature.Services.Refactorings.Specific.Rename;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.VB.Util;
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
        var isUnityProject = UnityProjectDetector.Instance.IsUnityProject();
        return isClass && isUnityProject;
    }

    public RenameAvailabilityCheckResult CheckRenameAvailability(IDeclaredElement element)
    {
        return RenameAvailabilityCheckResult.CanBeRenamed;
    }

    public IEnumerable<AtomicRenameBase> CreateAtomicRenames(IDeclaredElement declaredElement, string newName,
        bool doNotAddBindingConflicts)
    {
        if (renameDeclaredElements.Contains(declaredElement.ShortName))
        {
            return [];
        }

        renameDeclaredElements.Add(declaredElement.ShortName);
        var solution = declaredElement.GetSolution();
        return [new ModifyUnityAssetAtomicRename(declaredElement, solution, newName)];
    }
}