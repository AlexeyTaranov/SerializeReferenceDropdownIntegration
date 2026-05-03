# Serialize Reference Dropdown Rider Integration

Unity editor package for `com.jetbrains.rider.plugins.serializereferencedropdownintegration`.

## Install

Add the package to the Unity project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.alexeytaranov.serializereferencedropdown.riderintegration": "file:/absolute/path/to/unity/com.alexeytaranov.serializereferencedropdown.riderintegration"
  }
}
```

## Commands

- `OpenAsset`: selects and pings a Unity asset by project-relative path, for example `Assets/Foo.prefab`.
- `ShowSearchTypeWindow`: resolves the type name payload and raises `SrdBridgeServer.SearchTypeWindowRequested`.

Commands are newline-terminated JSON messages sent through the `SerializeReferenceDropdownIntegration` named pipe.
Rider includes a temporary `replyPipe` name in each command.
After executing the command on the Unity main thread, the bridge opens that reply pipe and writes a newline-terminated JSON response.

```json
{"version":1,"command":"OpenAsset","payload":"Assets/Foo.prefab","replyPipe":"srd.abc123"}
{"version":1,"status":"Ok","message":"Unity asset was selected: Assets/Foo.prefab"}
```

Known response statuses are `Ok`, `InvalidJson`, `EmptyCommand`, `UnknownCommand`, `AssetNotFound`, `TypeNotResolved`, `Timeout`, and `Error`.

## Enable Or Disable

The bridge does not read preferences directly. The main SerializeReferenceDropdown editor package should call `SrdBridgeServer.SetEnabled(...)` when `SerializeReferenceToolsUserPreferences.EnableRiderIntegration` changes.

After changing the preference in UI, save the preferences and pass the value to the bridge:

```csharp
using SerializeReferenceDropdown.Editor.Preferences;
using SerializeReferenceDropdownBridge;

var preferences = SerializeReferenceToolsUserPreferences.GetOrLoadSettings();
preferences.EnableRiderIntegration = true;
preferences.SaveToEditorPrefs();
SrdBridgeServer.SetEnabled(preferences.EnableRiderIntegration);
```

The bridge starts enabled by default. Call `SrdBridgeServer.SetEnabled(false)` during editor initialization if your stored preference disables Rider integration.

## Connect Search UI

The simplest integration is to subscribe from another editor script and open your existing search window there.

```csharp
using SerializeReferenceDropdownBridge;
using System;
using UnityEditor;

[InitializeOnLoad]
internal static class SrdSearchWindowConnector
{
    static SrdSearchWindowConnector()
    {
        SrdBridgeServer.SearchTypeWindowRequested += OpenSearchWindow;
        SrdBridgeServer.SearchTypeWindowRequestedByName += OpenSearchWindowByName;
    }

    private static void OpenSearchWindow(Type type)
    {
        // Replace this with your search window implementation.
        // Example: SrdSearchWindow.Open(type);
        UnityEngine.Debug.Log($"Open SerializeReference search for {type.FullName}");
    }

    private static void OpenSearchWindowByName(string typeName)
    {
        // Fallback when Unity cannot resolve the type from loaded assemblies.
        UnityEngine.Debug.Log($"Open SerializeReference search for unresolved type {typeName}");
    }
}
```

Keep the search window implementation outside the bridge listener. The bridge package should only receive Rider commands, normalize them onto the Unity main thread, and expose small events/actions for feature code to handle.
