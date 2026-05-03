using System;
using System.Diagnostics;
using JetBrains.Application.BuildScript.Application.Zones;
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
    public const string Name = "Serialize Reference Dropdown";

    public static void Log(string message)
    {
        var line = $"{DateTimeOffset.Now:O} {Names.SRDShort} OPTIONS: {message}";
        Trace.WriteLine(line);
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
