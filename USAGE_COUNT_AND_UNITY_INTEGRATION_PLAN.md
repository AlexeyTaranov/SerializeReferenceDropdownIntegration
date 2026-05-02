# Usage Count And Unity Integration Plan

This plan tracks the next improvements for SerializeReference usage preview and Rider-to-Unity integration.

## Current State

- `Usages Count` is shown through Rider Code Vision above supported C# classes.
- Clicking `Usages Count` currently opens a text preview with Unity asset file paths and line numbers.
- The preview is read-only and shown in a `MessageBox`.
- Asset entries in the preview are not clickable yet.
- `ToUnitySrdPipe` can send a type search command to Unity through a named pipe.
- `ToUnityWindowFocusSwitch` currently focuses Unity only on macOS through `osascript`.

## Remaining Usage Count Work

- Replace the `MessageBox` preview with a lightweight dialog or tool window.
- Show asset files as structured rows: relative path, reference count, and line numbers.
- Add an `Open in Unity` action for each asset row.
- Keep the preview read-only in the first UI version.
- Keep the Code Vision extra action for refreshing the usage database.
- Verify the new `Usage Count` settings in Rider:
  - show or hide Code Vision usage count,
  - hide zero usages,
  - auto-refresh usage database,
  - click behavior for preview versus refresh.

## Unity Integration Work

- Extend `ToUnitySrdPipe` with a new method for opening a Unity asset.
- Keep the existing type search command for compatibility.
- Add a new command format for asset opening, for example:
  - `OpenAsset-{relativeAssetPath}`
- Prefer moving to JSON commands later to avoid escaping issues with paths, spaces, and special symbols.
- Add a timeout to pipe connection so Rider does not hang if Unity or the Unity bridge is unavailable.
- Show a clear error if the Unity bridge cannot be reached.

## Windows Support

- Keep named pipe usage, because `NamedPipeClientStream(".", PipeName, ...)` can work on Windows when the Unity-side bridge listens on the same pipe name.
- Add Windows focus support in `ToUnityWindowFocusSwitch`.
- Use `Process.GetProcessesByName("Unity")` to find Unity.
- Use `user32.dll` interop for `ShowWindow` and `SetForegroundWindow`.
- Keep macOS support through `osascript`.
- For unsupported platforms, skip focus switching and keep the pipe command behavior.

## Suggested Implementation Order

1. Add `ToUnitySrdPipe.OpenUnityAsset(relativeAssetPath)` with connection timeout and diagnostics.
2. Add Windows Unity focus support.
3. Replace usage preview `MessageBox` with a simple structured UI.
4. Wire asset row click or button to `OpenUnityAsset`.
5. Smoke test on macOS first, then Windows with the Unity-side bridge.
6. Consider JSON-based pipe commands once both `ShowSearchTypeWindow` and `OpenAsset` exist.

## Open Questions

- What exact command name does the Unity package expect for opening an asset?
- Should Rider send project-relative paths like `Assets/Foo.prefab`, or absolute file paths?
- Should opening an asset also ping/select it in Project view, open it in Inspector, or open the scene/prefab stage?
- Should usage preview show only asset file rows, or also individual YAML line references?
