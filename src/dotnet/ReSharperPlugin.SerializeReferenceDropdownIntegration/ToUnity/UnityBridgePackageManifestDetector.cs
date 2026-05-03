using System;
using System.IO;
using JetBrains.Application.Parts;
using JetBrains.ProjectModel;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ToUnity;

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public sealed class UnityBridgePackageManifestDetector
{
    private readonly ISolution solution;
    private readonly PluginDiagnostics diagnostics;

    public UnityBridgePackageManifestDetector(ISolution solution, PluginDiagnostics diagnostics)
    {
        this.solution = solution;
        this.diagnostics = diagnostics;
    }

    public bool HasBridgePackageInstalled()
    {
        var manifestPath = GetManifestPath();
        if (!File.Exists(manifestPath))
        {
            diagnostics.Warn($"Unity package manifest was not found: {manifestPath}");
            return false;
        }

        try
        {
            return UnityBridgePackageManifest.ManifestContainsBridgePackage(File.ReadAllText(manifestPath));
        }
        catch (Exception exception)
        {
            diagnostics.Error($"Failed to read Unity package manifest: {manifestPath}", exception);
            return false;
        }
    }

    public string GetManifestPath() =>
        Path.Combine(solution.SolutionDirectory.FullPath, "Packages", "manifest.json");
}
