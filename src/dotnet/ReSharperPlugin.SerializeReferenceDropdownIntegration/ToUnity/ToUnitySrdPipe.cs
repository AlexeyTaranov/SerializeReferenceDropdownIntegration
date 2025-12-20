using System;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Application.Parts;
using JetBrains.ProjectModel;
using JetBrains.Util;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ToUnity;

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public class ToUnitySrdPipe
{
    private const string PipeName = "SerializeReferenceDropdownIntegration";
    private bool showOnce;
    private bool? needOpenSearchTool;

    public void OpenUnitySearchToolWindowWithType(string typeName)
    {
        needOpenSearchTool ??= MessageBox.ShowYesNoCancel("Need open Search Tool in this session?");

        if (needOpenSearchTool != true)
        {
            return;
        }

        Task.Run(() => SendMessageToPipe(typeName));
        
        if (showOnce == false)
        {
            MessageBox.ShowInfo("Check Unity window:)", "SRD DEV");
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
            //
        }
    }
}