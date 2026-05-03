# Serialize Reference Dropdown Integration for Rider

Rider plugin for Unity projects that use SerializeReference-heavy workflows.

The plugin helps Rider understand where serialized managed-reference types are used in Unity YAML assets, and adds safer rename-time helpers for keeping Unity assets in sync with C# refactorings.

Unity bridge package: [`com.alexeytaranov.serializereferencedropdown.riderintegration`](unity/com.alexeytaranov.serializereferencedropdown.riderintegration)

## Features

- Shows SerializeReference usage counts above supported C# classes through Rider Code Vision.
- Opens a compact asset usage preview when clicking the usage count.
- Selects and pings referenced Unity assets through the optional Unity bridge package.
- Adds optional `MovedFrom` support during class rename.
- Updates Unity YAML SerializeReference entries during class or namespace rename after explicit user opt-in.
- Provides persistent settings under `Settings | Tools | Serialize Reference Dropdown`.
- Adds Unity-side asmdef tools for renaming asmdefs and retargeting SerializeReference YAML assembly names.

## Install

### Rider plugin

1. Download the plugin archive from the release page.
2. Open Rider `Settings | Plugins`.
3. Click the gear icon and choose `Install Plugin from Disk...`.
4. Restart Rider.

### Unity bridge package

Add the Unity package to the Unity project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.alexeytaranov.serializereferencedropdown.riderintegration": "file:/absolute/path/to/unity/com.alexeytaranov.serializereferencedropdown.riderintegration"
  }
}
```

The bridge is required only for Rider-to-Unity actions, such as opening an asset from the usage preview or opening a SerializeReference search UI in Unity.

## Usage Count

1. Open a Unity C# type that can appear in SerializeReference YAML.
2. Click the `SRD` Code Vision entry.
3. Refresh the usage database if Rider asks for it.
4. Click the usage count to preview Unity asset files.
5. Click an asset row to select and ping it in Unity.

If the usage database has not been built yet, Rider will show that Unity files have not been analyzed instead of hiding the usage count as `0`.

## Rename Support

During class or namespace rename, the plugin can scan Unity YAML assets and show how many SerializeReference entries can be updated.

The rename page is intentionally opt-in:

- `No changes` keeps Unity YAML untouched.
- `Write YAML changes to disk` saves the scanned Unity YAML changes after the refactoring finishes.
- The write option is disabled until scanning finds affected files.

Use VCS before applying YAML edits. Rider undo for saved Unity YAML files is not guaranteed yet, so Git or another VCS is the recommended rollback path.

## Settings

Open `Settings | Tools | Serialize Reference Dropdown` to configure:

- whether the Unity asset rename page is shown,
- whether modified Unity assets are scanned automatically,
- default YAML apply behavior,
- warning behavior before writing Unity YAML,
- `MovedFrom` behavior on class rename,
- Unity focus switching after bridge commands,
- missing Unity bridge package warnings.

## Unity Asmdef Tools

The Unity package adds inspector tools for `.asmdef` assets:

- rename the asmdef and update asmdef/asmref references,
- update matching SerializeReference `asm` values in Unity YAML,
- rename the `.asmdef` asset file,
- move SerializeReference YAML entries from another assembly name to the selected asmdef.

These tools live on the Unity side because asmdef assets and their import behavior are owned by Unity.

## Limitations

- Unity YAML edits are saved to disk and should be reviewed in VCS.
- Undo integration for saved Unity YAML files is still a research item.
- Large Unity projects can take time to scan; use the refresh action deliberately.
- Rider-to-Unity actions require Unity to be running with the bridge package installed and enabled.

## Build

```bash
./gradlew --no-configuration-cache buildPlugin
dotnet test ReSharperPlugin.SerializeReferenceDropdownIntegration.sln
```

The Rider plugin archive is copied to `output/` by `buildPlugin`.

## Release Checklist

- Update `CHANGELOG.md`.
- Verify `PluginVersion` in `gradle.properties` and `<version>` in `src/rider/main/resources/META-INF/plugin.xml`.
- Build the plugin archive with `./gradlew --no-configuration-cache buildPlugin`.
- Install the archive from disk into Rider.
- Smoke test usage count refresh, asset usage preview, class rename YAML opt-in, namespace rename YAML opt-in, settings persistence, and Unity bridge asset opening.
- Confirm the Unity bridge package README and `package.json` match the release.

## License

MIT. See [LICENSE](LICENSE).
