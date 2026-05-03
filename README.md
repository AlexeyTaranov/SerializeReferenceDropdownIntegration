# Serialize Reference Dropdown Integration for Rider

Rider plugin for Unity projects that use `SerializeReference`.

It shows where serialized managed-reference types are used in Unity assets, helps keep YAML references in sync during Rider rename refactorings, and can open referenced assets in Unity through a small Unity bridge package.

![Serialize Reference Dropdown Integration](https://github.com/user-attachments/assets/e26e63d6-ef0b-433d-a4e1-e3c8077411d8)

## Features

- [Usage Count And Asset Preview](#usage-count-and-asset-preview): Code Vision counter and referenced Unity asset list.
- [Rename YAML Updates](#rename-yaml-updates): opt-in class and namespace rename support for Unity YAML.
- [MovedFrom Rename Helper](#movedfrom-rename-helper): optionally add Unity `MovedFrom` during class rename.
- [[Unity Package] Unity Bridge](#unity-package-unity-bridge): open assets and connect Unity search UI from Rider.
- [[Unity Package] Asmdef Tools](#unity-package-asmdef-tools): Unity inspector helpers for asmdef rename and SerializeReference `asm` retargeting.
- [Settings](#settings): persistent Rider settings for plugin behavior.

## Install

### Rider Plugin

1. Download the Rider plugin archive from the release page.
2. Open Rider `Settings | Plugins`.
3. Click the gear icon and choose `Install Plugin from Disk...`.
4. Restart Rider.

### Unity Bridge Package

Install the Unity package through Unity Package Manager with this Git URL:

```text
https://github.com/AlexeyTaranov/SerializeReferenceDropdownIntegration.git?path=/unity/com.alexeytaranov.serializereferencedropdown.riderintegration
```

Or add it to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.alexeytaranov.serializereferencedropdown.riderintegration": "https://github.com/AlexeyTaranov/SerializeReferenceDropdownIntegration.git?path=/unity/com.alexeytaranov.serializereferencedropdown.riderintegration"
  }
}
```

The bridge starts automatically when Unity loads the package.

## Usage Count And Asset Preview

The plugin adds an `SRD` Code Vision entry above supported Unity C# classes. It counts matching `SerializeReference` entries in Unity YAML assets.

![Usage Count](https://github.com/user-attachments/assets/e60d8c04-5dc9-4d14-b847-d8157b939d45)

If the usage database has not been built yet, Rider shows that Unity files have not been analyzed instead of silently hiding the counter as `0`.

Click the `SRD` usage count to open a compact asset list. Rows show asset type icons, asset names, and project-relative folders.

When Unity is running with the bridge package installed, clicking an asset row sends `OpenAsset` to Unity and selects/pings that asset.

## Rename YAML Updates

During class or namespace rename, the plugin can scan Unity YAML assets and update matching `SerializeReference` type data.

The flow is intentionally explicit:

- `Scan / Rescan` checks affected Unity asset files.
- `No changes` keeps Unity YAML untouched.
- `Write YAML changes to disk` applies scanned YAML changes after the refactoring finishes.
- The write option stays unavailable until affected files are found.

![Rename YAML Updates](https://github.com/user-attachments/assets/3da99091-0787-4b70-ae32-42140a0f469e)

Use VCS before applying YAML edits. Rider undo for saved Unity YAML files is not guaranteed yet.

## MovedFrom Rename Helper

During class rename, the plugin can add `UnityEngine.Scripting.APIUpdating.MovedFromAttribute`.

![MovedFrom Rename Helper](https://github.com/user-attachments/assets/aae2dd53-977a-4138-9f4a-ca80e02ce6f1)

The behavior can be configured as ask, always add, or never add.

## [Unity Package] Unity Bridge

The Unity bridge package receives newline-terminated JSON commands from Rider through the `SerializeReferenceDropdownIntegration` named pipe.

Supported commands:

- `OpenAsset`: selects and pings a Unity asset by project-relative path, for example `Assets/Foo.prefab`.
- `ShowSearchTypeWindow`: resolves a type name and raises `SrdBridgeServer.SearchTypeWindowRequested` or `SearchTypeWindowRequestedByName`.

Use `SerializeReferenceDropdownBridge.Bridge.SrdBridgeServer` to subscribe to bridge events or toggle the bridge. The bridge is enabled by default. If another Unity package has its own preferences UI, it can call `SrdBridgeServer.SetEnabled(false)` and `SrdBridgeServer.SetEnabled(true)`.

## [Unity Package] Asmdef Tools

The Unity package adds `Serialize Reference Dropdown Tools` to the inspector for `.asmdef` assets.

Available actions:

- Rename an asmdef and update asmdef/asmref references.
- Rename the `.asmdef` asset file.
- Rewrite matching Unity YAML `SerializeReference` `asm` values.
- Move SerializeReference YAML entries from another assembly name, such as `Assembly-CSharp`, to the selected asmdef.

These tools live in Unity because asmdef assets and imports are owned by Unity.

## Settings

Open `Settings | Tools | Serialize Reference Dropdown`.

Settings include:

- show or hide the Unity asset rename page,
- scan modified Unity assets automatically,
- default YAML apply behavior,
- warning before writing Unity YAML,
- `MovedFrom` behavior,
- show or hide `SRD` Code Vision,
- hide `0 usages` only after Unity files were analyzed,
- auto-refresh the usage database,
- usage-count click behavior,
- Unity focus switching after bridge commands,
- missing Unity bridge package warnings.

## Limitations

- Unity YAML edits are saved to disk and should be reviewed in VCS.
- Undo integration for saved Unity YAML files is still a research item.
- Large Unity projects can take time to scan.
- Rider-to-Unity actions require Unity to be running with the bridge package installed.

## Build

```bash
./gradlew --no-configuration-cache buildPlugin
dotnet test ReSharperPlugin.SerializeReferenceDropdownIntegration.sln
```

The Rider plugin archive is copied to `output/`.

## License

MIT. See [LICENSE](LICENSE).
