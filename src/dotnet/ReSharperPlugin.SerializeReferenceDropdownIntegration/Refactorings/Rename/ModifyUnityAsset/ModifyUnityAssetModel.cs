using System;
using System.Collections.Generic;
using System.IO;
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
        var allLines = new List<string>();
        foreach (var data in targetTypeData)
        {
            allLines.Clear();
            allLines.AddRange(File.ReadAllLines(data.FilePath));

            foreach (var prefabOverride in data.PrefabOverrides)
            {
                var oldLine = allLines[prefabOverride.LineIndex];

                var fullTypeName = string.IsNullOrEmpty(newType.Namespace)
                    ? newType.ClassName
                    : $"{newType.Namespace}.{newType.ClassName}";

                var newLine = UnityAssetReferenceParser.PrefabOverrideSerializeReferenceTypeRegex.Replace(
                    oldLine,
                    $"value: {newType.AssemblyName} {fullTypeName}"
                );

                allLines[prefabOverride.LineIndex] = newLine;
            }

            foreach (var modifyTypeData in data.References)
            {
                var oldLineText = allLines[modifyTypeData.LineIndex];
                if (modifyTypeData.MultiLine)
                {
                    var nextLineIndex = modifyTypeData.LineIndex + 1;
                    oldLineText += allLines[nextLineIndex];
                    allLines.RemoveAt(nextLineIndex);
                }

                var newLine = UnityAssetReferenceParser.SerializeReferenceRegex.Replace(oldLineText,
                    $"type: {{class: {newType.ClassName}, ns: {newType.Namespace}, asm: {newType.AssemblyName}}}");

                allLines[modifyTypeData.LineIndex] = newLine;
            }


            File.WriteAllLines(data.FilePath, allLines);
        }

        return Task.CompletedTask;
    }

    public void LogModificationFailure(Exception exception)
    {
        diagnostics.Error($"Failed to modify Unity assets for '{oldType.GetFullTypeName()}'.", exception);
    }
}
