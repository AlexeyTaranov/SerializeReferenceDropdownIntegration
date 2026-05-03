using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Application.Parts;
using JetBrains.IDE.UI;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using JetBrains.Util;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ToUnity;

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public class ToUnitySrdPipe
{
    private const string PipeName = "SerializeReferenceDropdownIntegration";
    private const int ConnectTimeoutMs = 1500;
    private const int ResponseTimeoutMs = 5000;
    private const string ReplyPipePrefix = "srd.";
    private const string ShowSearchTypeWindowCommand = "ShowSearchTypeWindow";
    private const string OpenAssetCommand = "OpenAsset";
    private const string OpenAsmdefRenameWindowCommand = "OpenAsmdefRenameWindow";

    private readonly PluginSessionSettings sessionSettings;
    private readonly PluginDiagnostics diagnostics;
    private readonly ToUnityWindowFocusSwitch windowFocusSwitch;
    private readonly UnityBridgePackageManifestDetector bridgePackageDetector;
    private readonly MissingUnityBridgePackageDialog missingBridgePackageDialog;
    private bool searchWindowHintShown;
    private bool missingBridgePackageWarningShown;

    public ToUnitySrdPipe(PluginSessionSettings sessionSettings, PluginDiagnostics diagnostics,
        ToUnityWindowFocusSwitch windowFocusSwitch, UnityBridgePackageManifestDetector bridgePackageDetector,
        IDialogHost dialogHost, Lifetime lifetime)
    {
        this.sessionSettings = sessionSettings;
        this.diagnostics = diagnostics;
        this.windowFocusSwitch = windowFocusSwitch;
        this.bridgePackageDetector = bridgePackageDetector;
        missingBridgePackageDialog = new MissingUnityBridgePackageDialog(dialogHost, lifetime, sessionSettings);
    }

    public void OpenUnitySearchToolWindowWithType(string typeName)
    {
        sessionSettings.NeedOpenSearchTool ??= MessageBox.ShowYesNoCancel("Need open Search Tool in this session?",
            Names.SRDShort);

        if (sessionSettings.NeedOpenSearchTool != true)
        {
            return;
        }

        if (!CanSendUnityBridgeCommand())
        {
            return;
        }

        Task.Run(() => SendCommandToPipeAndSwitchFocus(ShowSearchTypeWindowCommand, typeName, $"type '{typeName}'"));
        
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

        if (!CanSendUnityBridgeCommand())
        {
            return;
        }

        Task.Run(() => SendCommandToPipeAndSwitchFocus(OpenAssetCommand, relativeAssetPath,
            $"asset '{relativeAssetPath}'"));
    }

    public void OpenAsmdefRenameWindow(string asmdefAssetPath = null)
    {
        if (!CanSendUnityBridgeCommand())
        {
            return;
        }

        Task.Run(() => SendCommandToPipeAndSwitchFocus(OpenAsmdefRenameWindowCommand, asmdefAssetPath ?? string.Empty,
            "asmdef rename window"));
    }

    private bool CanSendUnityBridgeCommand()
    {
        if (bridgePackageDetector.HasBridgePackageInstalled())
        {
            return true;
        }

        diagnostics.Warn($"Unity bridge package '{UnityBridgePackageManifest.PackageId}' is missing from manifest '{bridgePackageDetector.GetManifestPath()}'.");
        if (sessionSettings.ShowMissingUnityBridgePackageWarning && !missingBridgePackageWarningShown)
        {
            missingBridgePackageDialog.Show(bridgePackageDetector.GetManifestPath());
            missingBridgePackageWarningShown = true;
        }

        return false;
    }

    private void SendCommandToPipeAndSwitchFocus(string commandName, string payload, string diagnosticTarget)
    {
        var response = SendCommandToPipe(commandName, payload, diagnosticTarget);
        if (response is { IsSuccess: true })
        {
            windowFocusSwitch.SwitchToUnityApplication();
        }
    }

    private ToUnityBridgeResponse SendCommandToPipe(string commandName, string payload, string diagnosticTarget)
    {
        var replyPipeName = ReplyPipePrefix + Guid.NewGuid().ToString("N").Substring(0, 12);
        var commandLine = ToUnityBridgeProtocol.BuildJsonCommandLine(commandName, payload, replyPipeName);

        try
        {
            using var responseServer = new NamedPipeServerStream(replyPipeName, PipeDirection.In, 1,
                PipeTransmissionMode.Byte, PipeOptions.None);

            diagnostics.Info($"Connecting to Unity pipe '{PipeName}' for {diagnosticTarget}.");
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(ConnectTimeoutMs);
            diagnostics.Info($"Connected to Unity pipe '{PipeName}' for {diagnosticTarget}.");
            var buffer = Encoding.UTF8.GetBytes(commandLine);
            client.Write(buffer, 0, buffer.Length);
            client.Flush();
            diagnostics.Info($"Sent {buffer.Length} bytes for {diagnosticTarget} to Unity pipe '{PipeName}' with command '{commandLine.TrimEnd()}'.");

            var waitResponseTask = Task.Run(() => responseServer.WaitForConnection());
            if (!waitResponseTask.Wait(ResponseTimeoutMs))
            {
                diagnostics.Warn($"Timed out waiting for Unity bridge response pipe '{replyPipeName}' for {diagnosticTarget}.");
                return new ToUnityBridgeResponse(ToUnityBridgeResponseStatus.Timeout,
                    "Timed out waiting for Unity bridge response.");
            }

            var responseLine = ReadResponseLine(responseServer);
            var response = ToUnityBridgeProtocol.ParseResponse(responseLine);
            diagnostics.Info($"Unity bridge response for {diagnosticTarget}: {response.Status}. {response.Message}");
            if (!response.IsSuccess)
            {
                MessageBox.ShowError(response.Message, Names.SRDShort);
            }

            return response;
        }
        catch (Exception exception)
        {
            diagnostics.Error($"Failed to send {diagnosticTarget} to Unity pipe '{PipeName}' with command '{commandLine.TrimEnd()}'.",
                exception);
            MessageBox.ShowError("Unable to contact the Unity SRD bridge. Make sure Unity is running and the SRD package is loaded.",
                Names.SRDShort);
            return new ToUnityBridgeResponse(ToUnityBridgeResponseStatus.Error, exception.Message);
        }
    }

    private static string ReadResponseLine(Stream client)
    {
        var builder = new StringBuilder();
        while (true)
        {
            var read = client.ReadByte();
            if (read < 0 || read == '\n')
            {
                return builder.ToString();
            }

            builder.Append((char)read);
        }
    }
}
