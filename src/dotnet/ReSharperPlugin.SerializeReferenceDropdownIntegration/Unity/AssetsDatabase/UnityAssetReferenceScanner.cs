using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Application.Parts;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Data;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public class UnityAssetReferenceScanner
{
    public readonly record struct TypeReferenceData(
        string FilePath,
        IReadOnlyList<UnityReferenceTypeLineData> References,
        IReadOnlyList<UnityReferenceTypePrefabOverrideLineData> PrefabOverrides);

    private readonly ISolution solution;
    private readonly PluginDiagnostics diagnostics;

    public UnityAssetReferenceScanner(ISolution solution, PluginDiagnostics diagnostics)
    {
        this.solution = solution;
        this.diagnostics = diagnostics;
    }

    public bool TryGetUnityAssetFiles(out IReadOnlyList<string> allUnityFiles)
    {
        allUnityFiles = AssetsIterator.GetUnityFilesInAssetsFolder(solution);
        var assetsPath = Path.Combine(solution.SolutionDirectory.FullPath, "Assets");
        if (Directory.Exists(assetsPath))
        {
            return true;
        }

        diagnostics.Warn($"Unity Assets folder was not found: {assetsPath}");
        return false;
    }

    public async Task<Dictionary<UnityTypeData, int>> FetchSerializeReferenceTypesCountAsync(Property<double> progress,
        Property<string> description, CancellationToken cancellationToken)
    {
        if (!TryGetUnityAssetFiles(out var allFiles))
        {
            description.Value = "Assets folder not found";
            progress.Value = 1.0;
            return new Dictionary<UnityTypeData, int>();
        }

        var typeCount = new Dictionary<UnityTypeData, int>();
        description.Value = "1/2: Check All Unity Files";

        var referenceTypes = new List<UnityReferenceTypeLineData>();
        var prefabOverrides = new List<UnityReferenceTypePrefabOverrideLineData>();
        for (var i = 0; i < allFiles.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            description.Value = $"2/2: Read Unity files: {i}/{allFiles.Count}";
            progress.Value = allFiles.Count == 0 ? 1.0 : (float)(i + 1) / allFiles.Count;

            referenceTypes.Clear();
            prefabOverrides.Clear();
            await UnityAssetReferenceParser.FillReferenceTypesBlocksAsync(allFiles[i], referenceTypes, prefabOverrides);

            foreach (var checkType in referenceTypes.Select(t => t.Type).Concat(prefabOverrides.Select(t => t.Type)))
            {
                typeCount.TryGetValue(checkType, out var count);
                typeCount[checkType] = count + 1;
            }
        }

        if (allFiles.Count == 0)
        {
            progress.Value = 1.0;
            description.Value = "No Unity assets found";
        }

        return typeCount;
    }

    public async Task<IReadOnlyList<TypeReferenceData>> CollectTypeReferencesAsync(UnityTypeData targetType,
        CancellationToken cancellationToken)
    {
        return await CollectTypeReferencesAsync(targetType, cancellationToken, null, null);
    }

    public async Task<IReadOnlyList<TypeReferenceData>> CollectTypeReferencesAsync(UnityTypeData targetType,
        CancellationToken cancellationToken, Property<double> progress, Property<string> description)
    {
        if (!TryGetUnityAssetFiles(out var allUnityFiles))
        {
            if (description != null)
            {
                description.Value = "Assets folder not found";
            }

            if (progress != null)
            {
                progress.Value = 1.0;
            }

            return [];
        }

        if (description != null)
        {
            description.Value = $"Scanning Unity assets for {targetType.ClassName}";
        }
        var targetTypeData = new List<TypeReferenceData>();
        var allReferences = new List<UnityReferenceTypeLineData>();
        var allPrefabOverrides = new List<UnityReferenceTypePrefabOverrideLineData>();

        for (var i = 0; i < allUnityFiles.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var filePath = allUnityFiles[i];
            if (description != null)
            {
                description.Value = $"Scanning Unity assets: {i + 1}/{allUnityFiles.Count}";
            }

            if (progress != null)
            {
                progress.Value = allUnityFiles.Count == 0 ? 1.0 : (double)(i + 1) / allUnityFiles.Count;
            }

            allReferences.Clear();
            allPrefabOverrides.Clear();

            await UnityAssetReferenceParser.FillReferenceTypesBlocksAsync(filePath, allReferences, allPrefabOverrides);

            var targetReferences = allReferences.Where(t => t.Type == targetType).ToArray();
            var targetPrefabOverrides = allPrefabOverrides.Where(t => t.Type == targetType).ToArray();
            if (targetReferences.Length > 0 || targetPrefabOverrides.Length > 0)
            {
                targetTypeData.Add(new TypeReferenceData(filePath, targetReferences, targetPrefabOverrides));
            }
        }

        if (allUnityFiles.Count == 0)
        {
            if (description != null)
            {
                description.Value = "No Unity assets found";
            }

            if (progress != null)
            {
                progress.Value = 1.0;
            }
        }

        return targetTypeData;
    }
}
