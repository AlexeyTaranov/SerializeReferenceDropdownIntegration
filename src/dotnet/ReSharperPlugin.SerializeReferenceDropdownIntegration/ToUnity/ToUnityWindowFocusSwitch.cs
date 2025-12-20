using System.Diagnostics;
using System.Runtime.InteropServices;
using JetBrains.Application.Parts;
using JetBrains.ProjectModel;
using JetBrains.Util;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ToUnity;

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public class ToUnityWindowFocusSwitch
{
    private bool? needSwitchToUnityApp;

    public void SwitchToUnityApplication()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            needSwitchToUnityApp ??= MessageBox.ShowYesNoCancel("Need Switch to Unity in this session?");

            if (needSwitchToUnityApp == true)
            {
                Process.Start("osascript", "-e \"tell application \\\"Unity\\\" to activate\"");
            }
        }
    }
}