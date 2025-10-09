using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration;

public static class WindowFocusSwitch
{
    public static void SwitchToUnityApplication()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("osascript", "-e \"tell application \\\"Unity\\\" to activate\"");
        }
    }
}