using NUnit.Framework;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.ToUnity;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Tests;

[TestFixture]
public class UnityBridgePackageManifestDetectorTests
{
    [Test]
    public void ManifestContainsBridgePackageWhenDependencyExists()
    {
        const string manifest = """
                                {
                                  "dependencies": {
                                    "com.unity.textmeshpro": "3.0.6",
                                    "com.alexeytaranov.serializereferencedropdown.riderintegration": "file:../Packages/SerializeReferenceDropdown.RiderIntegration"
                                  }
                                }
                                """;

        Assert.That(UnityBridgePackageManifest.ManifestContainsBridgePackage(manifest), Is.True);
    }

    [Test]
    public void ManifestDoesNotContainBridgePackageWhenDependencyIsMissing()
    {
        const string manifest = """
                                {
                                  "dependencies": {
                                    "com.unity.textmeshpro": "3.0.6"
                                  }
                                }
                                """;

        Assert.That(UnityBridgePackageManifest.ManifestContainsBridgePackage(manifest), Is.False);
    }
}
