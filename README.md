# Serialize Reference Dropdown Integration for Rider

Rider plugin for Unity projects that use `SerializeReference`.

![Serialize Reference Dropdown Integration](https://github.com/user-attachments/assets/e26e63d6-ef0b-433d-a4e1-e3c8077411d8)

## Features

- [Usage Count](#usage-count): Unity Serialize references counter.
- [Rename Refactoring](#rename-refactoring): rename class and namespace support for Unity YAML.
- [[Unity Package] Unity Bridge](#unity-package-unity-bridge): open assets and connect Unity with Rider.
- [[Unity Package] Asmdef Rename Tools](#unity-package-asmdef-tools): Asmdef rename and SerializeReference `asm` retargeting.
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

<img width="826" height="463" alt="Screenshot 2026-05-04 at 20 40 02" src="https://github.com/user-attachments/assets/547357df-e130-44e7-adcd-67cc6c7393d1" />

## Rename Refactoring

https://github.com/user-attachments/assets/e4d34262-80ad-4c8d-a23a-843da0031ed4

You can add MovedFrom attribute too!

!!! Use VCS before applying YAML edits. Rider undo for saved Unity YAML files is not guaranteed yet. !!!

## [Unity Package] Unity Bridge

https://github.com/user-attachments/assets/f7493c69-d642-408c-9fcf-7cff2e22789d

## [Unity Package] Asmdef Tools

https://github.com/user-attachments/assets/fd157311-db4e-48a5-8835-6767f5cf8f01

## Settings

Open `Settings | Tools | Serialize Reference Dropdown`.

<img width="980" height="723" alt="image" src="https://github.com/user-attachments/assets/503eb07a-1ea0-4d5e-be35-6e59b7894021" />

