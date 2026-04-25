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
    private readonly PluginSessionSettings sessionSettings;
    private readonly PluginDiagnostics diagnostics;
    private bool showOnce;

    public ToUnitySrdPipe(PluginSessionSettings sessionSettings, PluginDiagnostics diagnostics)
    {
        this.sessionSettings = sessionSettings;
        this.diagnostics = diagnostics;
    }

    public void OpenUnitySearchToolWindowWithType(string typeName)
    {
        sessionSettings.NeedOpenSearchTool ??= MessageBox.ShowYesNoCancel("Need open Search Tool in this session?",
            Names.SRDShort);

        if (sessionSettings.NeedOpenSearchTool != true)
        {
            return;
        }

        Task.Run(() => SendMessageToPipe(typeName));
        
        if (showOnce == false)
        {
            MessageBox.ShowInfo("Check Unity window:)", $"{Names.SRDShort}");
            showOnce = true;
        }
    }

    private void SendMessageToPipe(string typeName)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect();
            var command = $"ShowSearchTypeWindow-{typeName}";
            var buffer = Encoding.UTF8.GetBytes(command);
            client.Write(buffer, 0, buffer.Length);
            client.Flush();
        }
        catch (Exception _)
        {
            diagnostics.Error($"Failed to send type '{typeName}' to Unity pipe '{PipeName}'.", _);
            MessageBox.ShowError("Unable to contact the Unity SRD bridge. Make sure Unity is running and the SRD package is loaded.",
                Names.SRDShort);
        }
    }
}
