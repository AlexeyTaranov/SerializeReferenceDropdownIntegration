using System.Diagnostics;
using JetBrains.Util;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration;

public class Log
{
    [Conditional("DEVLOG")]
    public static void DevInfo(string data)
    {
        MessageBox.ShowInfo(data, "SRD DEV");
    }
    
    [Conditional("DEVLOG")]
    public static void DevError(string data)
    {
        MessageBox.ShowInfo(data, "SRD DEV");
    }
}