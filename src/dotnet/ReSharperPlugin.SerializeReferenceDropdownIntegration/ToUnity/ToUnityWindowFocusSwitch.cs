using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using JetBrains.Application.Parts;
using JetBrains.ProjectModel;
using JetBrains.Util;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ToUnity;

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public class ToUnityWindowFocusSwitch
{
    private readonly PluginSessionSettings sessionSettings;
    private readonly PluginDiagnostics diagnostics;

    public ToUnityWindowFocusSwitch(PluginSessionSettings sessionSettings, PluginDiagnostics diagnostics)
    {
        this.sessionSettings = sessionSettings;
        this.diagnostics = diagnostics;
    }

    public void SwitchToUnityApplication()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return;
        }

        sessionSettings.NeedSwitchToUnityApp ??= MessageBox.ShowYesNoCancel("Need Switch to Unity in this session?",
            Names.SRDShort);

        if (sessionSettings.NeedSwitchToUnityApp != true)
        {
            return;
        }

        try
        {
            using var process = Process.Start("osascript", "-e \"tell application \\\"Unity\\\" to activate\"");
            if (process == null)
            {
                diagnostics.Warn("osascript returned no process while trying to focus Unity.");
            }
        }
        catch (Exception e)
        {
            diagnostics.Error("Failed to switch focus to Unity application.", e);
            MessageBox.ShowError("Unable to switch focus to Unity automatically.", Names.SRDShort);
        }
    }
}
