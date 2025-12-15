using System.Collections.Generic;
using JetBrains.Application;
using JetBrains.ReSharper.Feature.Services.Refactorings.Specific.Rename;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.VB.Util;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.SRD;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename.ModifyUnityAsset;

[ShellFeaturePart]
public class ModifyUnityAssetAtomicRenameFactory : IAtomicRenameFactory
{
    //TODO HACK I don't know why IsApplicable() called twice(
    private static List<string> _renameDeclaredElements = new();

    public bool IsApplicable(IDeclaredElement declaredElement)
    {
        _renameDeclaredElements.Clear();

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
        if (_renameDeclaredElements.Contains(declaredElement.ShortName))
        {
            return [];
        }

        _renameDeclaredElements.Add(declaredElement.ShortName);
        var solution = declaredElement.GetSolution();
        return [new ModifyUnityAssetAtomicRename(declaredElement, solution, newName)];
    }
}