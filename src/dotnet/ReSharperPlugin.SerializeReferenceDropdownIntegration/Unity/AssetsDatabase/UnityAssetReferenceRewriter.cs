using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Data;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

public enum UnityAssetReferenceChangeKind
{
    SerializeReference,
    PrefabOverride
}

public readonly record struct UnityAssetReferenceChange(
    UnityAssetReferenceChangeKind Kind,
    int LineIndex,
    UnityTypeData OldType,
    UnityTypeData NewType,
    IReadOnlyList<string> OldLines,
    IReadOnlyList<string> NewLines,
    int ReplacedLineCount);

public static class UnityAssetReferenceRewriter
{
    public static void RewriteFile(string filePath,
        IReadOnlyCollection<UnityReferenceTypeLineData> references,
        IReadOnlyCollection<UnityReferenceTypePrefabOverrideLineData> prefabOverrides,
        UnityTypeData newType)
    {
        var lines = File.ReadAllLines(filePath);
        var modifiedLines = RewriteLines(lines, references, prefabOverrides, newType);
        File.WriteAllLines(filePath, modifiedLines);
    }

    public static IReadOnlyList<string> RewriteLines(IReadOnlyList<string> lines,
        IReadOnlyCollection<UnityReferenceTypeLineData> references,
        IReadOnlyCollection<UnityReferenceTypePrefabOverrideLineData> prefabOverrides,
        UnityTypeData newType)
    {
        return ApplyChanges(lines, PreviewChanges(lines, references, prefabOverrides, newType));
    }

    public static IReadOnlyList<UnityAssetReferenceChange> PreviewChanges(IReadOnlyList<string> lines,
        IReadOnlyCollection<UnityReferenceTypeLineData> references,
        IReadOnlyCollection<UnityReferenceTypePrefabOverrideLineData> prefabOverrides,
        UnityTypeData newType)
    {
        return PreviewChanges(lines, references, prefabOverrides, _ => newType);
    }

    public static IReadOnlyList<UnityAssetReferenceChange> PreviewChanges(IReadOnlyList<string> lines,
        IReadOnlyCollection<UnityReferenceTypeLineData> references,
        IReadOnlyCollection<UnityReferenceTypePrefabOverrideLineData> prefabOverrides,
        Func<UnityTypeData, UnityTypeData?> tryMapType)
    {
        var changes = new List<UnityAssetReferenceChange>();

        foreach (var prefabOverride in prefabOverrides)
        {
            var newType = tryMapType(prefabOverride.Type);
            if (newType == null)
            {
                continue;
            }

            var oldLine = lines[prefabOverride.LineIndex];
            var newLine = UnityAssetReferenceParser.PrefabOverrideSerializeReferenceTypeRegex
                .Replace(oldLine, $"value: {newType.Value.AssemblyName} {newType.Value.GetFullTypeName()}");

            changes.Add(new UnityAssetReferenceChange(
                UnityAssetReferenceChangeKind.PrefabOverride,
                prefabOverride.LineIndex,
                prefabOverride.Type,
                newType.Value,
                new[] { oldLine },
                new[] { newLine },
                1));
        }

        foreach (var reference in references)
        {
            var newType = tryMapType(reference.Type);
            if (newType == null)
            {
                continue;
            }

            var oldLineText = lines[reference.LineIndex];
            IReadOnlyList<string> oldLines;
            IReadOnlyList<string> newLines;
            var replacedLineCount = 1;

            if (reference.MultiLine)
            {
                var nextLineText = lines[reference.LineIndex + 1];
                oldLines = new[] { oldLineText, nextLineText };
                oldLineText += nextLineText;
                newLines = new[]
                {
                    UnityAssetReferenceParser.SerializeReferenceRegex.Replace(oldLineText,
                        $"type: {{class: {newType.Value.ClassName}, ns: {newType.Value.Namespace},"),
                    $"{GetIndent(nextLineText)}asm: {newType.Value.AssemblyName}}}"
                };
                replacedLineCount = 2;
            }
            else
            {
                oldLines = new[] { oldLineText };
                newLines = new[]
                {
                    UnityAssetReferenceParser.SerializeReferenceRegex.Replace(oldLineText,
                        $"type: {{class: {newType.Value.ClassName}, ns: {newType.Value.Namespace}, asm: {newType.Value.AssemblyName}}}")
                };
            }

            changes.Add(new UnityAssetReferenceChange(
                UnityAssetReferenceChangeKind.SerializeReference,
                reference.LineIndex,
                reference.Type,
                newType.Value,
                oldLines,
                newLines,
                replacedLineCount));
        }

        return changes
            .Where(change => change.NewLines.Count > 0 && !change.NewLines.SequenceEqual(change.OldLines))
            .OrderBy(change => change.LineIndex)
            .ToArray();
    }

    public static IReadOnlyList<string> ApplyChanges(IReadOnlyList<string> lines,
        IReadOnlyCollection<UnityAssetReferenceChange> changes)
    {
        var modifiedLines = lines.ToList();

        foreach (var change in changes.OrderByDescending(change => change.LineIndex))
        {
            for (var i = 0; i < change.ReplacedLineCount; i++)
            {
                modifiedLines.RemoveAt(change.LineIndex);
            }

            modifiedLines.InsertRange(change.LineIndex, change.NewLines);
        }

        return modifiedLines;
    }

    private static string GetIndent(string line)
    {
        return Regex.Match(line, @"^\s*").Value;
    }
}
