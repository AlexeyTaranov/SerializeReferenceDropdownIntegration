using System.IO;
using System.Net;
using System.Net.Sockets;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration;

public static class UnityBridge
{
    public static int? portIndex;

    public static void OpenUnitySearchToolWindowWithType(string typeName)
    {
        //TODO: Run unity instance???
        if (portIndex != null)
        {
            using var client = new TcpClient(IPAddress.Loopback.ToString(), portIndex.Value);
            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream) { AutoFlush = true };
            var command = $"ShowSearchTypeWindow-{typeName}";
            writer.WriteLine(command);
        }
    }
}