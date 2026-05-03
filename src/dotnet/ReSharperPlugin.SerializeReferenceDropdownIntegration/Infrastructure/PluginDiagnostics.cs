using System;
using System.Diagnostics;
using JetBrains.Application.Parts;
using JetBrains.ProjectModel;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public class PluginDiagnostics
{
    private readonly string myPrefix;

    public PluginDiagnostics(ISolution solution)
    {
        myPrefix = $"{Names.SRDShort}[{solution.Name}]";
    }

    [Conditional("SRD_DEV")]
    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Warn(string message)
    {
        Write("WARN", message);
    }

    public void Error(string message, Exception exception)
    {
        Write("ERROR", $"{message}{Environment.NewLine}{exception}");
    }

    private void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:O} {myPrefix} {level}: {message}";
        switch (level)
        {
            case "WARN":
                Trace.TraceWarning(line);
                break;
            case "ERROR":
                Trace.TraceError(line);
                break;
            default:
                Trace.WriteLine(line);
                break;
        }
    }
}
