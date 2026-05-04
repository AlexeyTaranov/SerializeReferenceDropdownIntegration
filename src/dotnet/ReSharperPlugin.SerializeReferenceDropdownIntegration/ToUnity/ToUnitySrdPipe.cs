using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Application.Parts;
using JetBrains.Application.Threading;
using JetBrains.IDE.UI;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using JetBrains.Threading;
using JetBrains.Util;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ToUnity;

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public class ToUnitySrdPipe
{
    private const string PipeName = "SerializeReferenceDropdownIntegration";
    private const int ConnectTimeoutMs = 2500;
    private const int ResponseTimeoutMs = 5000;
    private const int MaxSendAttempts = 3;
    private const int RetryDelayMs = 250;
    private const string ReplyPipePrefix = "srd.";
    private const string ShowSearchTypeWindowCommand = "ShowSearchTypeWindow";
    private const string OpenAssetCommand = "OpenAsset";

    private readonly Lifetime lifetime;
    private readonly IShellLocks shellLocks;
    private readonly PluginSessionSettings sessionSettings;
    private readonly PluginDiagnostics diagnostics;
    private readonly ToUnityWindowFocusSwitch windowFocusSwitch;
    private readonly UnityBridgePackageManifestDetector bridgePackageDetector;
    private readonly MissingUnityBridgePackageDialog missingBridgePackageDialog;
    private readonly SemaphoreSlim commandSemaphore = new(1, 1);
    private bool searchWindowHintShown;
    private bool missingBridgePackageWarningShown;

    public ToUnitySrdPipe(Lifetime lifetime, IShellLocks shellLocks, PluginSessionSettings sessionSettings, PluginDiagnostics diagnostics,
        ToUnityWindowFocusSwitch windowFocusSwitch, UnityBridgePackageManifestDetector bridgePackageDetector,
        IDialogHost dialogHost)
    {
        this.lifetime = lifetime;
        this.shellLocks = shellLocks;
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

        Task.Run(() => SendCommandToPipeAndSwitchFocusAsync(ShowSearchTypeWindowCommand, typeName, $"type '{typeName}'"));
        
        if (searchWindowHintShown == false)
        {
            MessageBox.ShowInfo("Check Unity window:)", $"{Names.SRDShort}");
            searchWindowHintShown = true;
        }
    }

    public void OpenUnityAsset(string relativeAssetPath)
    {
        diagnostics.Info($"OpenUnityAsset requested for '{relativeAssetPath}'.");
        if (string.IsNullOrWhiteSpace(relativeAssetPath))
        {
            diagnostics.Warn("Skip opening Unity asset because asset path is empty.");
            return;
        }

        if (!CanSendUnityBridgeCommand())
        {
            diagnostics.Warn($"Skip opening Unity asset '{relativeAssetPath}' because Unity bridge command cannot be sent.");
            return;
        }

        diagnostics.Info($"Queue Unity bridge command '{OpenAssetCommand}' for asset '{relativeAssetPath}'.");
        Task.Run(() => SendCommandToPipeAndSwitchFocusAsync(OpenAssetCommand, relativeAssetPath,
            $"asset '{relativeAssetPath}'"));
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

    private async Task SendCommandToPipeAndSwitchFocusAsync(string commandName, string payload, string diagnosticTarget)
    {
        try
        {
            diagnostics.Info($"Start Unity bridge command '{commandName}' for {diagnosticTarget}.");
            await commandSemaphore.WaitAsync();
            ToUnityBridgeResponse response;
            try
            {
                response = await SendCommandToPipeWithRetryAsync(commandName, payload, diagnosticTarget);
            }
            finally
            {
                commandSemaphore.Release();
            }

            if (response is { IsSuccess: true })
            {
                diagnostics.Info($"Unity bridge command '{commandName}' succeeded for {diagnosticTarget}; switching focus to Unity.");
                await shellLocks.StartMainRead(lifetime, () => windowFocusSwitch.SwitchToUnityApplication());
            }
            else
            {
                diagnostics.Warn($"Unity bridge command '{commandName}' did not succeed for {diagnosticTarget}: {response?.Status}. {response?.Message}");
            }
        }
        catch (Exception exception)
        {
            diagnostics.Error($"Failed to execute Unity bridge command for {diagnosticTarget}.", exception);
        }
    }

    private async Task<ToUnityBridgeResponse> SendCommandToPipeWithRetryAsync(string commandName, string payload,
        string diagnosticTarget)
    {
        ToUnityBridgeResponse response = null;
        for (var attempt = 1; attempt <= MaxSendAttempts; attempt++)
        {
            var isLastAttempt = attempt == MaxSendAttempts;
            response = SendCommandToPipe(commandName, payload, diagnosticTarget, isLastAttempt);
            if (response.Status != ToUnityBridgeResponseStatus.Timeout &&
                response.Status != ToUnityBridgeResponseStatus.Error)
            {
                return response;
            }

            if (!isLastAttempt)
            {
                diagnostics.Warn(
                    $"Unity bridge command for {diagnosticTarget} failed with {response.Status}; retrying attempt {attempt + 1}/{MaxSendAttempts}.");
                await Task.Delay(RetryDelayMs);
            }
        }

        return response ?? new ToUnityBridgeResponse(ToUnityBridgeResponseStatus.Error,
            "Unity bridge command failed before receiving a response.");
    }

    private ToUnityBridgeResponse SendCommandToPipe(string commandName, string payload, string diagnosticTarget,
        bool showErrorOnFailure)
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
                shellLocks.StartMainRead(lifetime, () => MessageBox.ShowError(response.Message, Names.SRDShort))
                    .NoAwait();
            }

            return response;
        }
        catch (Exception exception)
        {
            diagnostics.Error($"Failed to send {diagnosticTarget} to Unity pipe '{PipeName}' with command '{commandLine.TrimEnd()}'.",
                exception);
            if (showErrorOnFailure)
            {
                shellLocks.StartMainRead(lifetime,
                        () => MessageBox.ShowError(
                            "Unable to contact the Unity SRD bridge. Make sure Unity is running and the SRD package is loaded.",
                            Names.SRDShort))
                    .NoAwait();
            }

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
