using System.Linq;
using JetBrains.Annotations;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.ProjectDetector;

public static class UnityProjectDetectorExtensions
{
    public static bool IsUnityProject(this IProject project)
    {
        var frameworkId = project.GetCurrentTargetFrameworkId();
        var moduleReferences = project.GetModuleReferences(frameworkId);
        var anyUnityReference = moduleReferences.Any(t => t.Name.Contains("UnityEngine"));
        return anyUnityReference;
    }

    public static bool IsFromUnityProject([NotNull] this IDeclaredElement element)
    {
        return element.PresentationLanguage.Is<CSharpLanguage>() &&
               element.GetSourceFiles().Any(sf => sf.GetProject().IsUnityProject());
    }
}