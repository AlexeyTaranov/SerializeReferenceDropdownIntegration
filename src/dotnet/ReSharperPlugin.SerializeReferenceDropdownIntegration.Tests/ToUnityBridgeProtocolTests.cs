using NUnit.Framework;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.ToUnity;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Tests;

public class ToUnityBridgeProtocolTests
{
    [Test]
    public void BuildJsonCommandLineEscapesPayloadAndTerminatesWithNewLine()
    {
        var commandLine = ToUnityBridgeProtocol.BuildJsonCommandLine("OpenAsset", "Assets/Foo \"Bar\".prefab",
            "srd.test");

        Assert.That(commandLine, Is.EqualTo("{\"version\":1,\"command\":\"OpenAsset\",\"payload\":\"Assets/Foo \\\"Bar\\\".prefab\",\"replyPipe\":\"srd.test\"}\n"));
    }

    [Test]
    public void ParseResponseReadsKnownStatusAndMessage()
    {
        var response = ToUnityBridgeProtocol.ParseResponse(
            "{\"version\":1,\"status\":\"AssetNotFound\",\"message\":\"Unity asset was not found: Assets/Missing.prefab\"}");

        Assert.That(response.Status, Is.EqualTo(ToUnityBridgeResponseStatus.AssetNotFound));
        Assert.That(response.Message, Is.EqualTo("Unity asset was not found: Assets/Missing.prefab"));
        Assert.That(response.IsSuccess, Is.False);
    }

    [Test]
    public void TypeNotResolvedResponseIsSuccessfulBecauseUnityRaisesFallbackEvent()
    {
        var response = ToUnityBridgeProtocol.ParseResponse(
            "{\"version\":1,\"status\":\"TypeNotResolved\",\"message\":\"Fallback event was raised.\"}");

        Assert.That(response.Status, Is.EqualTo(ToUnityBridgeResponseStatus.TypeNotResolved));
        Assert.That(response.IsSuccess, Is.True);
    }
}
