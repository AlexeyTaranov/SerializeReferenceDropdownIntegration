using System;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ToUnity;

public static class UnityBridgePackageManifest
{
    public const string PackageId = "com.alexeytaranov.serializereferencedropdown.riderintegration";

    public static bool ManifestContainsBridgePackage(string manifestText)
    {
        return !string.IsNullOrEmpty(manifestText) &&
               manifestText.IndexOf($"\"{PackageId}\"", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
