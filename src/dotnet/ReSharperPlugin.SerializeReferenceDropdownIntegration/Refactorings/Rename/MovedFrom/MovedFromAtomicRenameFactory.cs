using System.Collections.Generic;
using JetBrains.Application;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Refactorings.Specific.Rename;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.VB.Util;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.KnownTypes;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.ProjectDetector;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename.MovedFrom;

[ShellFeaturePart]
public class MovedFromAtomicRenameFactory : IAtomicRenameFactory
{
    //TODO HACK I don't know why IsApplicable() called twice(
    private static readonly List<string> renameDeclaredElements = new();

    public bool IsApplicable(IDeclaredElement declaredElement)
    {
        renameDeclaredElements.Clear();

        //TODO Support rename namespaces?
        var isClass = declaredElement.IsClass();

        return isClass && declaredElement.IsFromUnityProject();
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
        var knownTypesCache = declaredElement.GetSolution().GetComponent<KnownTypesCache>();
        return [new MovedFromAtomicRename(declaredElement, newName, knownTypesCache)];
    }
}