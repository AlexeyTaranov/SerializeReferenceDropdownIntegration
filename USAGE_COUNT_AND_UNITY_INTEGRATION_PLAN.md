# Usage Count And Unity Integration Plan

This plan tracks the next improvements for SerializeReference usage preview and Rider-to-Unity integration.

## Current State

- `Usages Count` is shown through Rider Code Vision above supported C# classes.
- Clicking `Usages Count` currently opens a text preview with Unity asset file paths and line numbers.
- The preview is read-only and shown in a `MessageBox`.
- The preview can open the first listed asset in Unity through the JSON bridge.
- Individual asset entries in the preview are not clickable yet.
- `ToUnitySrdPipe` sends newline-framed JSON type-search and asset-open commands to Unity through a named pipe.
- Unity bridge replies through a temporary Rider-hosted `replyPipe` after command execution on the Unity main thread.
- `ToUnitySrdPipe` uses a connection timeout so Rider does not hang when Unity or the Unity bridge is unavailable.
- `ToUnityWindowFocusSwitch` can focus Unity on macOS through `osascript` and on Windows through `user32.dll`.

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

- Keep command names stable:
  - `ShowSearchTypeWindow`
  - `OpenAsset`
- Use JSON pipe messages:
  - `{"version":1,"command":"ShowSearchTypeWindow","payload":"Namespace.TypeName","replyPipe":"srd.abc123"}`
  - `{"version":1,"command":"OpenAsset","payload":"Assets/Foo.prefab","replyPipe":"srd.abc123"}`
- Include a temporary `replyPipe` field and read JSON responses from that pipe:
  - `{"version":1,"status":"Ok","message":"Unity asset was selected: Assets/Foo.prefab"}`
  - known statuses: `Ok`, `InvalidJson`, `EmptyCommand`, `UnknownCommand`, `AssetNotFound`, `TypeNotResolved`, `Timeout`, `Error`.
- Show a clear error if the Unity bridge cannot be reached.

## Windows Support

- Keep named pipe usage, because `NamedPipeClientStream(".", PipeName, ...)` can work on Windows when the Unity-side bridge listens on the same pipe name.
- Keep macOS support through `osascript`.
- For unsupported platforms, skip focus switching and keep the pipe command behavior.

## Suggested Implementation Order

1. Replace usage preview `MessageBox` with a simple structured UI.
2. Wire asset row click or button to `OpenUnityAsset`.
3. Smoke test request-response delivery on macOS first, then Windows with the Unity-side bridge.
4. Keep string-command fallback in Unity temporarily if older Rider builds must remain supported.

## Open Questions

- Should Rider send project-relative paths like `Assets/Foo.prefab`, or absolute file paths?
- Should opening an asset also ping/select it in Project view, open it in Inspector, or open the scene/prefab stage?
- Should usage preview show only asset file rows, or also individual YAML line references?
