using System;
using System.Diagnostics;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration;

public class Log
{
    [Conditional("DEVLOG")]
    public static void DevInfo(string data)
    {
        Console.WriteLine($"SRD DEV: {data}");
    }
    
    [Conditional("DEVLOG")]
    public static void DevError(string data)
    {
        Console.WriteLine($"SRD DEV ERROR: {data}");
    }
}