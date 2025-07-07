using System;
using System.IO.Pipes;
using System.Text;
using JetBrains.Util;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration;

public static class UnityBridge
{
    private const string pipeName = "SerializeReferenceDropdownIntegration";
    private static bool showOnce;

    public static void OpenUnitySearchToolWindowWithType(string typeName)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            client.Connect();
            var command = $"ShowSearchTypeWindow-{typeName}";
            Log.DevInfo($"Send message: {command}");
            var buffer = Encoding.UTF8.GetBytes(command);
            client.Write(buffer, 0, buffer.Length);
            client.Flush();
            if (showOnce == false)
            {
                MessageBox.ShowInfo("Check Unity app :)", "SRD DEV");
                showOnce = true;
            }
        }
        catch (Exception e)
        {
            Log.DevError($"Send message failed: {e}");
            Console.WriteLine(e);
            throw;
        }
    }
}