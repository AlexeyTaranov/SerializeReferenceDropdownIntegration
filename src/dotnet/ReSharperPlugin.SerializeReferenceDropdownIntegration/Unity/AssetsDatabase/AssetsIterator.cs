using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.ProjectModel;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

public static class AssetsIterator
{
    private static readonly string[] unitySerializeReferenceAssetsExtensions = [".unity", ".prefab", ".asset"];

    public static IReadOnlyList<string> GetUnityFilesInAssetsFolder(ISolution solution)
    {
        var unityAssetsPath = Path.Combine(solution.SolutionDirectory.FullPath, "Assets");
        if (!Directory.Exists(unityAssetsPath))
        {
            return [];
        }

        var allUnityFiles = Directory.EnumerateFiles(
                unityAssetsPath,
                "*.*",
                SearchOption.AllDirectories)
            .Where(path => unitySerializeReferenceAssetsExtensions.Any(path.EndsWith)).ToArray();
        return allUnityFiles;
    }
}
