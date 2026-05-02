using System;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Application.Parts;
using JetBrains.ProjectModel;
using JetBrains.Util;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ToUnity;

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public class ToUnitySrdPipe
{
    private const string PipeName = "SerializeReferenceDropdownIntegration";
    private const int ConnectTimeoutMs = 1500;
    private const string ShowSearchTypeWindowCommand = "ShowSearchTypeWindow";
    private const string OpenAssetCommand = "OpenAsset";
    private const int ProtocolVersion = 1;

    private readonly PluginSessionSettings sessionSettings;
    private readonly PluginDiagnostics diagnostics;
    private readonly ToUnityWindowFocusSwitch windowFocusSwitch;
    private bool searchWindowHintShown;

    public ToUnitySrdPipe(PluginSessionSettings sessionSettings, PluginDiagnostics diagnostics,
        ToUnityWindowFocusSwitch windowFocusSwitch)
    {
        this.sessionSettings = sessionSettings;
        this.diagnostics = diagnostics;
        this.windowFocusSwitch = windowFocusSwitch;
    }

    public void OpenUnitySearchToolWindowWithType(string typeName)
    {
        sessionSettings.NeedOpenSearchTool ??= MessageBox.ShowYesNoCancel("Need open Search Tool in this session?",
            Names.SRDShort);

        if (sessionSettings.NeedOpenSearchTool != true)
        {
            return;
        }

        Task.Run(() => SendCommandToPipeAndSwitchFocus(BuildJsonCommand(ShowSearchTypeWindowCommand, typeName),
            $"type '{typeName}'"));
        
        if (searchWindowHintShown == false)
        {
            MessageBox.ShowInfo("Check Unity window:)", $"{Names.SRDShort}");
            searchWindowHintShown = true;
        }
    }

    public void OpenUnityAsset(string relativeAssetPath)
    {
        if (string.IsNullOrWhiteSpace(relativeAssetPath))
        {
            diagnostics.Warn("Skip opening Unity asset because asset path is empty.");
            return;
        }

        Task.Run(() => SendCommandToPipeAndSwitchFocus(BuildJsonCommand(OpenAssetCommand, relativeAssetPath),
            $"asset '{relativeAssetPath}'"));
    }

    private void SendCommandToPipeAndSwitchFocus(string command, string diagnosticTarget)
    {
        SendCommandToPipe(command, diagnosticTarget);
        windowFocusSwitch.SwitchToUnityApplication();
    }

    private void SendCommandToPipe(string command, string diagnosticTarget)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(ConnectTimeoutMs);
            var buffer = Encoding.UTF8.GetBytes(command);
            client.Write(buffer, 0, buffer.Length);
            client.Flush();
        }
        catch (Exception exception)
        {
            diagnostics.Error($"Failed to send {diagnosticTarget} to Unity pipe '{PipeName}' with command '{command}'.",
                exception);
            MessageBox.ShowError("Unable to contact the Unity SRD bridge. Make sure Unity is running and the SRD package is loaded.",
                Names.SRDShort);
        }
    }

    private static string BuildJsonCommand(string commandName, string payload)
    {
        return $"{{\"version\":{ProtocolVersion},\"command\":\"{EscapeJsonString(commandName)}\",\"payload\":\"{EscapeJsonString(payload)}\"}}";
    }

    private static string EscapeJsonString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length + 8);
        foreach (var character in value)
        {
            switch (character)
            {
                case '\\':
                    builder.Append(@"\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\b':
                    builder.Append(@"\b");
                    break;
                case '\f':
                    builder.Append(@"\f");
                    break;
                case '\n':
                    builder.Append(@"\n");
                    break;
                case '\r':
                    builder.Append(@"\r");
                    break;
                case '\t':
                    builder.Append(@"\t");
                    break;
                default:
                    if (character < ' ')
                    {
                        builder.Append("\\u");
                        builder.Append(((int)character).ToString("x4"));
                    }
                    else
                    {
                        builder.Append(character);
                    }
                    break;
            }
        }

        return builder.ToString();
    }
}
