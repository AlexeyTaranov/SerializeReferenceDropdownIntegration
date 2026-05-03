using System.Collections.Generic;
using JetBrains.Rider.Model;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ClassUsage;

public static class UnityAssetUsageIconProvider
{
    private static readonly Dictionary<UnityAssetUsagePreviewFileKind, IconModel> icons = new();

    public static IconModel GetIcon(UnityAssetUsagePreviewFileKind kind)
    {
        lock (icons)
        {
            if (icons.TryGetValue(kind, out var icon))
            {
                return icon;
            }

            icon = CreateIcon(kind);
            icons[kind] = icon;
            return icon;
        }
    }

    private static IconModel CreateIcon(UnityAssetUsagePreviewFileKind kind)
    {
        return new IdeaIconModel(GetIconUrl(kind));
    }

    private static string GetIconUrl(UnityAssetUsagePreviewFileKind kind)
    {
        switch (kind)
        {
            case UnityAssetUsagePreviewFileKind.Scene:
                return "/resharper/UnityFileType/FileUnity.svg";
            case UnityAssetUsagePreviewFileKind.Prefab:
                return "/resharper/UnityFileType/FileUnityPrefab.svg";
            default:
                return "/resharper/UnityFileType/FileUnityAsset.svg";
        }
    }
}
