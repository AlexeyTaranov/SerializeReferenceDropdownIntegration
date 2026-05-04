# Serialize Reference Dropdown Integration for Rider

Rider plugin for Unity projects that use `SerializeReference`.

![Serialize Reference Dropdown Integration](https://github.com/user-attachments/assets/e26e63d6-ef0b-433d-a4e1-e3c8077411d8)

## Features

- [Usage Count](#usage-count): Unity Serialize references counter.
- [Rename YAML](#rename-yaml): rename class and namespace support for Unity YAML.
- [MovedFrom Rename Helper](#movedfrom-rename-helper): optionally add Unity `MovedFrom` during class rename.
- [[Unity Package] Unity Bridge](#unity-package-unity-bridge): open assets and connect Unity with Rider.
- [[Unity Package] Asmdef Tools](#unity-package-asmdef-tools): Asmdef rename and SerializeReference `asm` retargeting.
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

The bridge starts automatically when Unity loads the package.

## Usage Count

The plugin adds an `SRD` Code Vision entry above supported Unity C# classes. It counts matching `SerializeReference` entries in Unity YAML assets.

![Usage Count](https://github.com/user-attachments/assets/e60d8c04-5dc9-4d14-b847-d8157b939d45)

## Rename YAML

During class or namespace rename, the plugin can scan Unity YAML assets and update matching `SerializeReference` type data.

![Rename YAML Updates](https://github.com/user-attachments/assets/3da99091-0787-4b70-ae32-42140a0f469e)

!!! Use VCS before applying YAML edits. Rider undo for saved Unity YAML files is not guaranteed yet. !!!

## MovedFrom Rename Helper

The plugin can add `UnityEngine.Scripting.APIUpdating.MovedFromAttribute`.

![MovedFrom Rename Helper](https://github.com/user-attachments/assets/aae2dd53-977a-4138-9f4a-ca80e02ce6f1)

## [Unity Package] Unity Bridge

## [Unity Package] Asmdef Tools

The Unity package adds `Serialize Reference Dropdown Tools` to the inspector for `.asmdef` assets.

## Settings

Open `Settings | Tools | Serialize Reference Dropdown`.
