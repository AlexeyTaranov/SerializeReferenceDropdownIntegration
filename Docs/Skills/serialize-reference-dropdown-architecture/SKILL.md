---
name: serialize-reference-dropdown-architecture
description: Use when working on the SerializeReferenceDropdown Rider plugin in this repository: rename refactorings, Unity asset YAML scanning/rewriting, Rider document writes, plugin settings, UI pages, tests, release preparation, or architecture decisions.
---

# SerializeReferenceDropdown Rider Plugin Architecture

## Project Shape

- Rider backend plugin code lives in `src/dotnet/ReSharperPlugin.SerializeReferenceDropdownIntegration`.
- Lightweight NUnit tests live in `src/dotnet/ReSharperPlugin.SerializeReferenceDropdownIntegration.Tests`.
- Tests intentionally avoid Rider SDK dependencies; production pure logic is linked into the test project.
- `README.md` and `CHANGELOG.md` are user-facing release docs.

## Main Capabilities

- **Class usage insight**: `ClassUsage/*` reads cached Unity asset reference counts.
- **Unity bridge**: `ToUnity/*` communicates with Unity and switches focus.
- **Rename refactoring: MovedFrom**: `Refactorings/Rename/MovedFrom/*` optionally adds Unity `MovedFrom` metadata during Rider rename.
- **Rename refactoring: Modify Unity assets**: `Refactorings/Rename/ModifyUnityAsset/*` previews and optionally applies YAML asset type changes during rename.
- **Unity asset database**: `Unity/AssetsDatabase/*` finds Unity assets, parses serialize reference data, previews rewrites, applies document edits, and stores counts.

## Unity Asset Rename Flow

1. `ModifyUnityAssetAtomicRenameFactory` creates `ModifyUnityAssetAtomicRename` only for Unity project classes.
2. `ModifyUnityAssetAtomicRename.CreateRenamesConfirmationPage` creates `ModifyUnityAssetModel` and `ModifyUnityAssetRefactoringPage`.
3. The page can run `Show modified files` to populate preview data and enables `Apply modified files` only after changes are found.
4. `ModifyUnityAssetRefactoringPage.Commit` writes the checkbox state into `ModifyUnityAssetModel.ShouldApplyModifiedFiles`.
5. `ModifyUnityAssetAtomicRename.Rename` runs in the actual rename pipeline and calls `ModifyUnityAssetModel.ModifyAllFilesAsync`.
6. `ModifyUnityAssetModel` collects references if preview was not loaded and passes file changes to `UnityAssetReferenceDocumentWriter`.

## Unity Package Namespace Guidance

- Unity editor scripts may live under an `Editor/` folder, but avoid using `Editor` as a namespace segment.
- Prefer namespaces such as `SerializeReferenceDropdownBridge` over `SerializeReferenceDropdownBridge.Editor`.
- This avoids collisions with `UnityEditor.Editor` and similar Unity API type names inside editor-only code.

## Asset Parsing And Rewriting Layers

- `AssetsIterator` enumerates Unity asset files under the solution `Assets` folder.
- `UnityAssetReferenceScanner` scans candidate files and returns references for a target `UnityTypeData`.
- `UnityAssetReferenceParser` extracts serialize reference type lines, including multiline `type` blocks and prefab override values.
- `UnityAssetReferenceRewriter` builds preview changes and applies line-level changes.
- `UnityAssetReferenceTextRewriter` applies changes to full document text while preserving newline style and final newline behavior.

## Document Writer Layers

- `UnityAssetReferenceDocumentWriter` is pure/testable. It depends on `IUnityAssetReferenceDocumentSession` and `IUnityAssetReferenceDocument`.
- `UnityAssetReferenceRiderDocumentWriter` adapts Rider SDK services: `DocumentManager`, `IDocumentStorageHelpers`, and `ICommandProcessor`.
- The Rider adapter uses current command prolongation when a rename command is already executing; otherwise it opens its own batch text change.
- Current caveat: asset changes are saved to disk after document replacement. Rider undo may restore the document buffer but not reliably persist the undo result back to disk.

## Testing Guidance

- Add tests to the lightweight NUnit project when possible.
- Prefer testing pure layers: parser, rewriter, text rewriter, and pure document writer.
- Do not link Rider SDK-dependent files into the test project.
- Current verification commands:
  - `dotnet test src/dotnet/ReSharperPlugin.SerializeReferenceDropdownIntegration.Tests/ReSharperPlugin.SerializeReferenceDropdownIntegration.Tests.csproj --no-restore --logger "console;verbosity=minimal"`
  - `dotnet build src/dotnet/ReSharperPlugin.SerializeReferenceDropdownIntegration/ReSharperPlugin.SerializeReferenceDropdownIntegration.Rider.csproj --no-restore`

## Known Risks And Design Notes

- Undo for saved Unity asset edits is not considered solved. Treat it as a separate research task before promising undo-safe behavior.
- Rename namespace support is still missing; current rename asset flow changes class name only.
- UI should avoid applying asset modifications unless the user explicitly opted in after checking affected files.
- Keep Unity YAML changes previewable and deterministic; do not mutate files during preview.
