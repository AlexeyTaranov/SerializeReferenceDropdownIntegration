using System;
using System.Linq;
using JetBrains.Application.Parts;
using JetBrains.ProjectModel;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.SRD;

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public class UnityProjectDetector
{
    private readonly ISolution solution;

    //Can we update projects,assemblies, etc at runtime in current solution?
    private bool? isUnityProject;

    public static UnityProjectDetector Instance { get; private set; }

    public UnityProjectDetector(ISolution solution)
    {
        this.solution = solution;
        Instance = this;
    }

    public bool IsUnityProject()
    {
        if (isUnityProject != null)
        {
            return isUnityProject.Value;
        }

        try
        {
            foreach (var project in solution.GetAllProjects())
            {
                var references = project.GetAllReferencedAssemblies().Select(r => r.Name);
                if (references.Any(r => r.Contains("UnityEngine") || r.Contains("UnityEditor")))
                {
                    isUnityProject = true;
                    return true;
                }
            }

            isUnityProject = false;
        }
        catch (Exception e)
        {
            //
        }

        return false;
    }
}