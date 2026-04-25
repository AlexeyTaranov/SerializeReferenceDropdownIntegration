using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Data;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

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
        var modifiedLines = lines.ToList();

        foreach (var prefabOverride in prefabOverrides)
        {
            var oldLine = modifiedLines[prefabOverride.LineIndex];
            modifiedLines[prefabOverride.LineIndex] = UnityAssetReferenceParser.PrefabOverrideSerializeReferenceTypeRegex
                .Replace(oldLine, $"value: {newType.AssemblyName} {newType.GetFullTypeName()}");
        }

        foreach (var reference in references.OrderByDescending(reference => reference.LineIndex))
        {
            var oldLineText = modifiedLines[reference.LineIndex];
            if (reference.MultiLine)
            {
                var nextLineIndex = reference.LineIndex + 1;
                var nextLineText = modifiedLines[nextLineIndex];
                oldLineText += nextLineText;
                modifiedLines.RemoveAt(nextLineIndex);

                modifiedLines[reference.LineIndex] = UnityAssetReferenceParser.SerializeReferenceRegex.Replace(oldLineText,
                    $"type: {{class: {newType.ClassName}, ns: {newType.Namespace},");
                modifiedLines.Insert(nextLineIndex, $"{GetIndent(nextLineText)}asm: {newType.AssemblyName}}}");
                continue;
            }

            modifiedLines[reference.LineIndex] = UnityAssetReferenceParser.SerializeReferenceRegex.Replace(oldLineText,
                $"type: {{class: {newType.ClassName}, ns: {newType.Namespace}, asm: {newType.AssemblyName}}}");
        }

        return modifiedLines;
    }

    private static string GetIndent(string line)
    {
        return Regex.Match(line, @"^\s*").Value;
    }
}
