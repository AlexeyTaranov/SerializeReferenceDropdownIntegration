using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration;

public static class UnityBridge
{
    //TODO: expose to settings
    private const int portIndex = 11000;
    
    public static void OpenUnitySearchToolWindowWithType(string typeName)
    {
        //TODO: Run unity instance???
        using var client = new TcpClient(IPAddress.Loopback.ToString(), portIndex);
        using var stream = client.GetStream();
        using var writer = new StreamWriter(stream) { AutoFlush = true };
        var command = $"ShowSearchTypeWindow-{typeName}";
        writer.WriteLine(command);
    }
}