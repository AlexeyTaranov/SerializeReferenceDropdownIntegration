using System.Diagnostics;
using System.Runtime.InteropServices;
using JetBrains.Application.Parts;
using JetBrains.ProjectModel;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ToUnity;

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public class ToUnityWindowFocusSwitch
{
    public void SwitchToUnityApplication()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("osascript", "-e \"tell application \\\"Unity\\\" to activate\"");
        }
    }
}