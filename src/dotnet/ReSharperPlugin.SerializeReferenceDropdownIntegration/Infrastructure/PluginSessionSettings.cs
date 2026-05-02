using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Application.Parts;
using JetBrains.Application.Settings;
using JetBrains.ProjectModel;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename.MovedFrom;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename.ModifyUnityAsset;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;

[SettingsKey(typeof(JetBrains.Application.Settings.WellKnownRootKeys.EnvironmentSettings),
    "Serialize Reference Dropdown Integration settings")]
public class PluginSettings
{
    [SettingsEntry(ModifyYamlShowBehaviour.ShowAlways, "Show Unity asset rename page")]
    public ModifyYamlShowBehaviour ModifyYamlShowBehaviour { get; set; }

    [SettingsEntry(true, "Automatically check modified Unity asset files on rename")]
    public bool AutoCheckModifiedUnityAssetFiles { get; set; }

    [SettingsEntry(false, "Default state for applying modified Unity asset files")]
    public bool DefaultApplyModifiedUnityAssetFiles { get; set; }

    [SettingsEntry(true, "Show warning before applying Unity asset file changes")]
    public bool ShowApplyModifiedUnityAssetFilesWarning { get; set; }

    [SettingsEntry(MovedFromRefactoringSettings.ShowPopup, "MovedFrom attribute behaviour on rename")]
    public MovedFromRefactoringSettings MovedFromRefactoringSettings { get; set; }

    [SettingsEntry(true, "Show SerializeReference usage count Code Vision")]
    public bool ShowUsageCountCodeVision { get; set; }

    [SettingsEntry(false, "Hide SerializeReference usage count when no Unity assets reference the type")]
    public bool HideZeroUsageCountCodeVision { get; set; }

    [SettingsEntry(false, "Automatically refresh SerializeReference usage count database")]
    public bool AutoRefreshUsageCountDatabase { get; set; }

    [SettingsEntry(true, "Show Unity asset usage preview when clicking usage count")]
    public bool ShowUsagePreviewOnClick { get; set; }
}

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public class PluginSessionSettings
{
    private const string KeyModifyYamlShowBehaviour = "serializeReferenceDropdown.modifyYamlShowBehaviour";
    private const string KeyAutoCheckModifiedUnityAssetFiles = "serializeReferenceDropdown.autoCheckModifiedUnityAssetFiles";
    private const string KeyDefaultApplyModifiedUnityAssetFiles = "serializeReferenceDropdown.defaultApplyModifiedUnityAssetFiles";
    private const string KeyShowApplyModifiedUnityAssetFilesWarning = "serializeReferenceDropdown.showApplyModifiedUnityAssetFilesWarning";
    private const string KeyMovedFromRefactoringSettings = "serializeReferenceDropdown.movedFromRefactoringSettings";
    private const string KeyShowUsageCountCodeVision = "serializeReferenceDropdown.showUsageCountCodeVision";
    private const string KeyHideZeroUsageCountCodeVision = "serializeReferenceDropdown.hideZeroUsageCountCodeVision";
    private const string KeyAutoRefreshUsageCountDatabase = "serializeReferenceDropdown.autoRefreshUsageCountDatabase";
    private const string KeyShowUsagePreviewOnClick = "serializeReferenceDropdown.showUsagePreviewOnClick";

    private readonly ISettingsStore settingsStore;

    public PluginSessionSettings(ISettingsStore settingsStore)
    {
        this.settingsStore = settingsStore;
        SerializeReferenceDropdownOptions.Log("PluginSessionSettings created. Solution-level components are available.");
    }

    public bool? NeedOpenSearchTool { get; set; }
    public bool? NeedSwitchToUnityApp { get; set; }

    public MovedFromRefactoringSettings MovedFromRefactoringSettings
    {
        get => ReadEnum(KeyMovedFromRefactoringSettings, Read(settings => settings.MovedFromRefactoringSettings));
        set
        {
            Write(settings => settings.MovedFromRefactoringSettings, value);
            WriteSharedSetting(KeyMovedFromRefactoringSettings, value.ToString());
        }
    }

    public ModifyYamlShowBehaviour ModifyYamlShowBehaviour
    {
        get => ReadEnum(KeyModifyYamlShowBehaviour, Read(settings => settings.ModifyYamlShowBehaviour));
        set
        {
            Write(settings => settings.ModifyYamlShowBehaviour, value);
            WriteSharedSetting(KeyModifyYamlShowBehaviour, value.ToString());
        }
    }

    public bool AutoCheckModifiedUnityAssetFiles
    {
        get => ReadBool(KeyAutoCheckModifiedUnityAssetFiles, Read(settings => settings.AutoCheckModifiedUnityAssetFiles));
        set
        {
            Write(settings => settings.AutoCheckModifiedUnityAssetFiles, value);
            WriteSharedSetting(KeyAutoCheckModifiedUnityAssetFiles, value.ToString());
        }
    }

    public bool DefaultApplyModifiedUnityAssetFiles
    {
        get => ReadBool(KeyDefaultApplyModifiedUnityAssetFiles, Read(settings => settings.DefaultApplyModifiedUnityAssetFiles));
        set
        {
            Write(settings => settings.DefaultApplyModifiedUnityAssetFiles, value);
            WriteSharedSetting(KeyDefaultApplyModifiedUnityAssetFiles, value.ToString());
        }
    }

    public bool ShowApplyModifiedUnityAssetFilesWarning
    {
        get => ReadBool(KeyShowApplyModifiedUnityAssetFilesWarning, Read(settings => settings.ShowApplyModifiedUnityAssetFilesWarning));
        set
        {
            Write(settings => settings.ShowApplyModifiedUnityAssetFilesWarning, value);
            WriteSharedSetting(KeyShowApplyModifiedUnityAssetFilesWarning, value.ToString());
        }
    }

    public bool ShowUsageCountCodeVision
    {
        get => ReadBool(KeyShowUsageCountCodeVision, Read(settings => settings.ShowUsageCountCodeVision));
        set
        {
            Write(settings => settings.ShowUsageCountCodeVision, value);
            WriteSharedSetting(KeyShowUsageCountCodeVision, value.ToString());
        }
    }

    public bool HideZeroUsageCountCodeVision
    {
        get => ReadBool(KeyHideZeroUsageCountCodeVision, Read(settings => settings.HideZeroUsageCountCodeVision));
        set
        {
            Write(settings => settings.HideZeroUsageCountCodeVision, value);
            WriteSharedSetting(KeyHideZeroUsageCountCodeVision, value.ToString());
        }
    }

    public bool AutoRefreshUsageCountDatabase
    {
        get => ReadBool(KeyAutoRefreshUsageCountDatabase, Read(settings => settings.AutoRefreshUsageCountDatabase));
        set
        {
            Write(settings => settings.AutoRefreshUsageCountDatabase, value);
            WriteSharedSetting(KeyAutoRefreshUsageCountDatabase, value.ToString());
        }
    }

    public bool ShowUsagePreviewOnClick
    {
        get => ReadBool(KeyShowUsagePreviewOnClick, Read(settings => settings.ShowUsagePreviewOnClick));
        set
        {
            Write(settings => settings.ShowUsagePreviewOnClick, value);
            WriteSharedSetting(KeyShowUsagePreviewOnClick, value.ToString());
        }
    }

    private IContextBoundSettingsStore BoundStore => settingsStore.BindToContextTransient(
        ContextRange.ApplicationWide,
        BindToContextFlags.RespectReadonlyFlag | BindToContextFlags.EraseDefaultValues);

    private T Read<T>(System.Linq.Expressions.Expression<System.Func<PluginSettings, T>> expression) =>
        BoundStore.GetValue(expression);

    private void Write<T>(System.Linq.Expressions.Expression<System.Func<PluginSettings, T>> expression, T value) =>
        BoundStore.SetValue(expression, value);

    private static string SharedSettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".serialize-reference-dropdown-integration",
            "settings.properties");

    private static T ReadEnum<T>(string key, T fallback)
        where T : struct, Enum
    {
        var settings = ReadSharedSettings();
        return settings.TryGetValue(key, out var value) && Enum.TryParse<T>(value, out var parsed)
            ? parsed
            : fallback;
    }

    private static bool ReadBool(string key, bool fallback)
    {
        var settings = ReadSharedSettings();
        return settings.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }

    private static Dictionary<string, string> ReadSharedSettings()
    {
        var result = new Dictionary<string, string>();
        try
        {
            if (!File.Exists(SharedSettingsPath))
                return result;

            foreach (var line in File.ReadAllLines(SharedSettingsPath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                    continue;

                var key = line.Substring(0, separatorIndex).Trim();
                var value = line.Substring(separatorIndex + 1).Trim();
                result[key] = value;
            }
        }
        catch (Exception exception)
        {
            SerializeReferenceDropdownOptions.Log($"failed to read shared settings.{Environment.NewLine}{exception}");
        }

        return result;
    }

    private static void WriteSharedSetting(string key, string value)
    {
        try
        {
            var settings = ReadSharedSettings();
            settings[key] = value;

            var directory = Path.GetDirectoryName(SharedSettingsPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using var writer = new StreamWriter(SharedSettingsPath);
            writer.WriteLine("# Serialize Reference Dropdown Integration settings");
            foreach (var setting in settings)
                writer.WriteLine($"{setting.Key}={setting.Value}");
        }
        catch (Exception exception)
        {
            SerializeReferenceDropdownOptions.Log($"failed to write shared settings.{Environment.NewLine}{exception}");
        }
    }
}
