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

    public void Info(string message)
    {
        Trace.WriteLine($"{myPrefix} INFO: {message}");
    }

    public void Warn(string message)
    {
        Trace.TraceWarning($"{myPrefix} WARN: {message}");
    }

    public void Error(string message, Exception exception)
    {
        Trace.TraceError($"{myPrefix} ERROR: {message}{Environment.NewLine}{exception}");
    }
}
