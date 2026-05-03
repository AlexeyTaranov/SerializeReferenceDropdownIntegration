using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JetBrains.Application.BuildScript.Application.Zones;
using JetBrains.Application;
using JetBrains.Application.Components;
using JetBrains.Application.Parts;
using JetBrains.Application.UI.Options;
using JetBrains.Application.UI.Options.OptionsDialog;
using JetBrains.Application.UI.Options.OptionPages;
using JetBrains.IDE.UI.Options;
using JetBrains.Lifetimes;
using JetBrains.UI.RichText;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename.MovedFrom;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename.ModifyUnityAsset;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;

public static class SerializeReferenceDropdownOptions
{
    public const string Id = "SerializeReferenceDropdown";
    public const string DebugTopLevelId = "SerializeReferenceDropdownDebugTopLevel";
    public const string Name = "Serialize Reference Dropdown";

    public static void Log(string message)
    {
        var line = $"{DateTimeOffset.Now:O} {Names.SRDShort} OPTIONS: {message}";
        Trace.WriteLine(line);
        try
        {
            File.AppendAllText("/tmp/srd-options-debug.log", line + Environment.NewLine);
        }
        catch
        {
            // Diagnostics must never break Rider startup.
        }
    }

    public static void LogRegisteredPages(IOptionPagesList optionPagesList)
    {
        try
        {
            var pages = optionPagesList.GetOptionPages().ToArray();
            var matches = pages
                .Where(page =>
                    page.Attribute.Id.IndexOf("SerializeReference", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    page.Attribute.Name.IndexOf("Serialize", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(page =>
                    $"id={page.Attribute.Id}, name={page.Attribute.Name}, parent={page.Attribute.ParentId}, type={page.PageType.FullName}")
                .ToArray();
            Log($"GetOptionPages count={pages.Length}, serialize matches={matches.Length}: {string.Join(" | ", matches)}");
        }
        catch (Exception exception)
        {
            Log($"GetOptionPages failed.{Environment.NewLine}{exception}");
        }
    }

    public static void LogOptionsManager(OptionsManager optionsManager)
    {
        try
        {
            var pages = optionsManager.GetOptionPages().ToArray();
            var matches = pages
                .Where(page =>
                    page.Attribute.Id.IndexOf("SerializeReference", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    page.Attribute.Name.IndexOf("Serialize", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(page =>
                    $"id={page.Attribute.Id}, name={page.Attribute.Name}, parent={page.Attribute.ParentId}, type={page.PageType.FullName}")
                .ToArray();
            var tree = optionsManager.CreateOptionPagesTree();
            Log($"OptionsManager pages count={pages.Length}, serialize matches={matches.Length}: {string.Join(" | ", matches)}");
            Log($"OptionsManager tree has Tools: {tree.IsValidID("Tools")}, id={Id}: {tree.IsValidID(Id)}, debug id={DebugTopLevelId}: {tree.IsValidID(DebugTopLevelId)}");
            LogPageDescriptor(tree, "Tools");
            LogPageDescriptor(tree, Id);
            LogPageDescriptor(tree, DebugTopLevelId);
            Log($"page type implements IOptionsPage={typeof(IOptionsPage).IsAssignableFrom(typeof(SerializeReferenceDropdownOptionsPage))}.");
        }
        catch (Exception exception)
        {
            Log($"OptionsManager diagnostics failed.{Environment.NewLine}{exception}");
        }
    }

    public static void TryOpenOptionsFromMarker(OptionsManager optionsManager)
    {
        const string markerPath = "/tmp/srd-open-options-on-startup";
        try
        {
            if (!File.Exists(markerPath))
                return;

            Log($"open marker found at {markerPath}. Calling BeginShowOptions({Id}).");
            optionsManager.BeginShowOptions(Id);
            Log("BeginShowOptions returned without exception.");
        }
        catch (Exception exception)
        {
            Log($"BeginShowOptions failed.{Environment.NewLine}{exception}");
        }
    }

    private static void LogPageDescriptor(OptionPagesTree tree, string id)
    {
        var descriptor = tree.GetElementByID(id);
        if (descriptor == null)
        {
            Log($"OptionsManager tree descriptor id={id}: null.");
            return;
        }

        Log($"OptionsManager tree descriptor id={id}: path={descriptor.GetFullPath()}, parent={descriptor.Parent?.Id ?? "<root>"}.");
    }
}

[ShellComponent(Instantiation.DemandAnyThreadSafe)]
public class SerializeReferenceDropdownOptionsPageRegistrar : IStartupActivity
{
    public SerializeReferenceDropdownOptionsPageRegistrar(Lifetime lifetime,
        IOptionPagesList optionPagesList,
        OptionsManager optionsManager)
    {
        SerializeReferenceDropdownOptions.Log($"registrar created. Registering page id={SerializeReferenceDropdownOptions.Id}, parent=Tools.");
        try
        {
            var attribute = new OptionsPageAttribute(SerializeReferenceDropdownOptions.Id,
                SerializeReferenceDropdownOptions.Name,
                null)
            {
                ParentId = "Tools"
            };
            optionPagesList.AddOptionsPage(lifetime, typeof(SerializeReferenceDropdownOptionsPage), attribute);
            optionsManager.AddOptionsPage(lifetime, typeof(SerializeReferenceDropdownOptionsPage), attribute);
            SerializeReferenceDropdownOptions.Log("AddOptionsPage completed.");
            SerializeReferenceDropdownOptions.LogRegisteredPages(optionPagesList);
            SerializeReferenceDropdownOptions.LogOptionsManager(optionsManager);
            SerializeReferenceDropdownOptions.TryOpenOptionsFromMarker(optionsManager);
        }
        catch (Exception exception)
        {
            SerializeReferenceDropdownOptions.Log($"AddOptionsPage failed.{Environment.NewLine}{exception}");
            throw;
        }
    }
}

[ShellComponent(Instantiation.DemandAnyThreadSafe)]
public class SerializeReferenceDropdownTopLevelOptionsPageRegistrar : IStartupActivity
{
    public SerializeReferenceDropdownTopLevelOptionsPageRegistrar(Lifetime lifetime,
        IOptionPagesList optionPagesList,
        OptionsManager optionsManager)
    {
        SerializeReferenceDropdownOptions.Log($"top-level debug registrar created. Registering page id={SerializeReferenceDropdownOptions.DebugTopLevelId}.");
        try
        {
            var attribute = new OptionsPageAttribute(SerializeReferenceDropdownOptions.DebugTopLevelId,
                $"{SerializeReferenceDropdownOptions.Name} Debug",
                null)
            {
                Sequence = 9999
            };
            optionPagesList.AddOptionsPage(lifetime, typeof(SerializeReferenceDropdownOptionsPage), attribute);
            optionsManager.AddOptionsPage(lifetime, typeof(SerializeReferenceDropdownOptionsPage), attribute);
            SerializeReferenceDropdownOptions.Log("top-level debug AddOptionsPage completed.");
            SerializeReferenceDropdownOptions.LogRegisteredPages(optionPagesList);
            SerializeReferenceDropdownOptions.LogOptionsManager(optionsManager);
        }
        catch (Exception exception)
        {
            SerializeReferenceDropdownOptions.Log($"top-level debug AddOptionsPage failed.{Environment.NewLine}{exception}");
            throw;
        }
    }
}

[ZoneMarker(typeof(IToolsOptionsPageImplZone))]
[OptionsPage(SerializeReferenceDropdownOptions.Id, SerializeReferenceDropdownOptions.Name, null, ParentId = "Tools")]
public class SerializeReferenceDropdownOptionsPage : BeSimpleOptionsPage
{
    public SerializeReferenceDropdownOptionsPage(Lifetime lifetime,
        OptionsPageContext optionsPageContext,
        OptionsSettingsSmartContext optionsSettingsSmartContext)
        : base(lifetime, optionsPageContext, optionsSettingsSmartContext, true)
    {
        SerializeReferenceDropdownOptions.Log($"page constructed. id={SerializeReferenceDropdownOptions.Id}.");
        try
        {
            var showBehaviour = optionsSettingsSmartContext.GetValue((PluginSettings settings) => settings.ModifyYamlShowBehaviour);
            var autoCheck = optionsSettingsSmartContext.GetValue((PluginSettings settings) => settings.AutoCheckModifiedUnityAssetFiles);
            var defaultApply = optionsSettingsSmartContext.GetValue((PluginSettings settings) => settings.DefaultApplyModifiedUnityAssetFiles);
            var showWarning = optionsSettingsSmartContext.GetValue((PluginSettings settings) => settings.ShowApplyModifiedUnityAssetFilesWarning);
            var movedFromBehaviour = optionsSettingsSmartContext.GetValue((PluginSettings settings) => settings.MovedFromRefactoringSettings);
            var focusSwitch = optionsSettingsSmartContext.GetValue((PluginSettings settings) => settings.UnityWindowFocusSwitchSettings);
            SerializeReferenceDropdownOptions.Log($"settings snapshot showBehaviour={showBehaviour}, autoCheck={autoCheck}, defaultApply={defaultApply}, showWarning={showWarning}, movedFrom={movedFromBehaviour}, focusSwitch={focusSwitch}.");
        }
        catch (Exception exception)
        {
            SerializeReferenceDropdownOptions.Log($"failed to read settings snapshot.{Environment.NewLine}{exception}");
        }

        AddHeader("Unity asset references");
        AddComboEnum((PluginSettings settings) => settings.ModifyYamlShowBehaviour,
            "Show Unity asset rename page:");
        AddBoolOption((PluginSettings settings) => settings.AutoCheckModifiedUnityAssetFiles,
            true,
            false,
            new RichText("Automatically check modified Unity asset files on rename"),
            "Scan Unity asset files when the rename refactoring page opens.");
        AddBoolOption((PluginSettings settings) => settings.DefaultApplyModifiedUnityAssetFiles,
            true,
            false,
            new RichText("Enable Apply modified files by default after scan"),
            "The checkbox still stays disabled until changed files are detected.");
        AddBoolOption((PluginSettings settings) => settings.ShowApplyModifiedUnityAssetFilesWarning,
            true,
            false,
            new RichText("Show warning before applying Unity asset file changes"),
            "Display the VCS review warning on the rename page.");

        AddHeader("MovedFrom attribute");
        AddComboEnum((PluginSettings settings) => settings.MovedFromRefactoringSettings,
            "MovedFrom on class rename:");

        AddHeader("Unity integration");
        AddComboEnum((PluginSettings settings) => settings.UnityWindowFocusSwitchSettings,
            "Switch to Unity after bridge command:");
        AddBoolOption((PluginSettings settings) => settings.ShowMissingUnityBridgePackageWarning,
            true,
            false,
            new RichText("Show warning when Unity SRD package is missing"),
            "Checks Packages/manifest.json before sending Unity bridge commands.");

        AddKeyword("SerializeReference", "Unity", "YAML", "asset", nameof(ModifyYamlShowBehaviour),
            nameof(MovedFromRefactoringSettings), nameof(UnityWindowFocusSwitchSettings), "MovedFrom");
    }
}
