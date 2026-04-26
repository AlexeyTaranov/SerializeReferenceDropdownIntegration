using System;
using System.Collections.Generic;
using System.Linq;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

public static class UnityAssetReferenceTextRewriter
{
    public static string ApplyChanges(string originalText, IReadOnlyCollection<UnityAssetReferenceChange> changes)
    {
        var originalLines = SplitLines(originalText);
        var modifiedLines = UnityAssetReferenceRewriter.ApplyChanges(originalLines, changes);

        return JoinLinesPreservingStyle(originalText, modifiedLines);
    }

    private static IReadOnlyList<string> SplitLines(string text)
    {
        return text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static string JoinLinesPreservingStyle(string originalText, IReadOnlyList<string> lines)
    {
        var newline = originalText.Contains("\r\n") ? "\r\n" : "\n";
        var hasFinalNewline = originalText.EndsWith("\n", StringComparison.Ordinal) ||
                              originalText.EndsWith("\r", StringComparison.Ordinal);
        var linesToJoin = hasFinalNewline && lines.Count > 0 && lines.Last().Length == 0
            ? lines.Take(lines.Count - 1)
            : lines;
        var result = string.Join(newline, linesToJoin);

        return hasFinalNewline ? result + newline : result;
    }
}
