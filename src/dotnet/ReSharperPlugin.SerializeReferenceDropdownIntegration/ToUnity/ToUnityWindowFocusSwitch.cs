using System;
using System.Diagnostics;
using System.Linq;
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

        if (!ShouldSwitchToUnityApplication())
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

    private bool ShouldSwitchToUnityApplication()
    {
        switch (sessionSettings.UnityWindowFocusSwitchSettings)
        {
            case UnityWindowFocusSwitchSettings.AlwaysSwitch:
                return true;
            case UnityWindowFocusSwitchSettings.NeverSwitch:
                return false;
            default:
                sessionSettings.NeedSwitchToUnityApp ??= MessageBox.ShowYesNoCancel(
                    "Switch to Unity after sending commands in this session?",
                    Names.SRDShort);
                return sessionSettings.NeedSwitchToUnityApp == true;
        }
    }

    private void SwitchToUnityOnMacOs()
    {
        var unityProcesses = Process.GetProcessesByName("Unity");
        try
        {
            var unityProcess = unityProcesses.FirstOrDefault(process => process.MainWindowHandle != IntPtr.Zero) ??
                               unityProcesses.FirstOrDefault();
            if (unityProcess == null)
            {
                diagnostics.Warn("Unity process was not found while trying to focus Unity on macOS.");
                MessageBox.ShowError("Unable to find an open Unity window.", Names.SRDShort);
                return;
            }

            var appPath = TryGetUnityApplicationPath(unityProcess);
            if (!string.IsNullOrEmpty(appPath))
            {
                StartMacOsFocusProcess("/usr/bin/open", $"-a \"{appPath}\"");
                return;
            }

            StartMacOsFocusProcess("/usr/bin/osascript",
                $"-e \"tell application \\\"System Events\\\" to set frontmost of first application process whose unix id is {unityProcess.Id} to true\"");
        }
        finally
        {
            foreach (var process in unityProcesses)
            {
                process.Dispose();
            }
        }
    }

    private static string TryGetUnityApplicationPath(Process unityProcess)
    {
        try
        {
            var executablePath = unityProcess.MainModule?.FileName;
            if (string.IsNullOrEmpty(executablePath))
            {
                return string.Empty;
            }

            const string applicationPathSuffix = ".app/Contents/MacOS/Unity";
            var applicationPathEndIndex = executablePath.IndexOf(applicationPathSuffix, StringComparison.Ordinal);
            if (applicationPathEndIndex < 0)
            {
                return string.Empty;
            }

            return executablePath.Substring(0, applicationPathEndIndex + ".app".Length);
        }
        catch
        {
            return string.Empty;
        }
    }

    private void StartMacOsFocusProcess(string fileName, string arguments)
    {
        using var process = Process.Start(fileName, arguments);
        if (process == null)
        {
            diagnostics.Warn($"'{fileName}' returned no process while trying to focus Unity.");
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
