using System;
using System.Diagnostics;
using System.IO;
using JetBrains.Application.Parts;
using JetBrains.ProjectModel;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public class PluginDiagnostics
{
    public const string LogPath = "/tmp/srd-plugin-debug.log";

    private readonly string myPrefix;

    public PluginDiagnostics(ISolution solution)
    {
        myPrefix = $"{Names.SRDShort}[{solution.Name}]";
    }

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

        try
        {
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch
        {
            // Diagnostics must never break Rider startup or refactoring flows.
        }
    }
}
