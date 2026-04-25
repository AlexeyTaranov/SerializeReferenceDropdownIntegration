using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Data;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename.ModifyUnityAsset;

public class ModifyUnityAssetModel
{
    private readonly UnityTypeData oldType;
    private readonly UnityTypeData newType;
    private readonly UnityAssetReferenceScanner scanner;
    private readonly PluginDiagnostics diagnostics;
    private readonly List<UnityAssetReferenceScanner.TypeReferenceData> targetTypeData = new();

    public ModifyUnityAssetModel(UnityTypeData oldType, UnityTypeData newType, UnityAssetReferenceScanner scanner,
        PluginDiagnostics diagnostics)
    {
        this.oldType = oldType;
        this.newType = newType;
        this.scanner = scanner;
        this.diagnostics = diagnostics;
    }

    public async Task<int> FetchSerializeReferenceCountInAssetsFolderAsync(CancellationToken cancellationToken)
    {
        targetTypeData.Clear();
        var collectedTypeData = await scanner.CollectTypeReferencesAsync(oldType, cancellationToken);
        if (collectedTypeData == null)
        {
            return -1;
        }

        targetTypeData.AddRange(collectedTypeData);
        return targetTypeData.Sum(t => t.References.Count + t.PrefabOverrides.Count);
    }

    public Task ModifyAllFilesAsync()
    {
        foreach (var data in targetTypeData)
        {
            UnityAssetReferenceRewriter.RewriteFile(data.FilePath, data.References, data.PrefabOverrides, newType);
        }

        return Task.CompletedTask;
    }

    public void LogModificationFailure(Exception exception)
    {
        diagnostics.Error($"Failed to modify Unity assets for '{oldType.GetFullTypeName()}'.", exception);
    }
}
