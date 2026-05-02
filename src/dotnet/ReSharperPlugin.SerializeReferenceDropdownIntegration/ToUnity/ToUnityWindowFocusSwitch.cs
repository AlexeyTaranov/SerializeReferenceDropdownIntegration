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
    private const int SwRestore = 9;

    private readonly PluginSessionSettings sessionSettings;
    private readonly PluginDiagnostics diagnostics;

    public ToUnityWindowFocusSwitch(PluginSessionSettings sessionSettings, PluginDiagnostics diagnostics)
    {
        this.sessionSettings = sessionSettings;
        this.diagnostics = diagnostics;
    }

    public void SwitchToUnityApplication()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                SwitchToUnityOnMacOs();
                return;
            }

            SwitchToUnityOnWindows();
        }
        catch (Exception e)
        {
            diagnostics.Error("Failed to switch focus to Unity application.", e);
            MessageBox.ShowError("Unable to switch focus to Unity automatically.", Names.SRDShort);
        }
    }

    private void SwitchToUnityOnMacOs()
    {
        using var process = Process.Start("osascript", "-e \"tell application \\\"Unity\\\" to activate\"");
        if (process == null)
        {
            diagnostics.Warn("osascript returned no process while trying to focus Unity.");
        }
    }

    private void SwitchToUnityOnWindows()
    {
        var unityProcesses = Process.GetProcessesByName("Unity");
        try
        {
            foreach (var process in unityProcesses)
            {
                var windowHandle = process.MainWindowHandle;
                if (windowHandle == IntPtr.Zero)
                {
                    continue;
                }

                ShowWindow(windowHandle, SwRestore);
                if (!SetForegroundWindow(windowHandle))
                {
                    diagnostics.Warn($"SetForegroundWindow returned false for Unity process '{process.Id}'.");
                }

                return;
            }

            diagnostics.Warn("Unity process with a main window was not found.");
            MessageBox.ShowError("Unable to find an open Unity window.", Names.SRDShort);
        }
        finally
        {
            foreach (var process in unityProcesses)
            {
                process.Dispose();
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
