using System;
using System.Diagnostics;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration;

public static class WindowFocusSwitch
{
    public static void SwitchToUnityApplication()
    {
        SwitchOnMacOS();
    }

    private static void SwitchOnMacOS()
    {
        Process.Start("osascript", "-e \"tell application \\\"Unity\\\" to activate\"");
    }
}