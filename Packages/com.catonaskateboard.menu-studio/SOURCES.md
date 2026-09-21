# Sources and provenance

The C# files in this package were newly written for this task. Existing project tools were inspected as functional references; their implementations were not copied. No third-party library implementation, native binary, shader, font, sprite, bank or audio file is redistributed here.

Unity and, where applicable, FMOD remain separately installed dependencies with their own terms. Referencing a public API does not transfer that dependency's implementation into this package. No ownership or license is asserted over third-party dependencies.

## Local functional references

- OFF BEAT DIVERPUNK: `Assets/Scripts/Game/Scriptable Presets/HUD/GameHudButtonInteractionSettings.cs`: independent menu profiles, state graphics, image content, transform feedback, pulses and clips.
- OFF BEAT DIVERPUNK: `GameHudSettingsNavigationSettings.cs`: action navigation, repeat and focused-option presentation.
- OFF BEAT DIVERPUNK: `Assets/Scripts/Player/UI/Menu/MainMenuController.cs`, `GameplayMenuController.cs` and `Settings/`: menu flow and scene structure.
- OFF BEAT DIVERPUNK: `Assets/Scripts/Editor/GameManagement Tool/Graphic/HUD/GameHudCreditsPanelUtility.cs`: credits close action.
- Roadside-Space-Kebab: Player Studio workspace organization as a layout reference.

## Official API references

- [Unity Input System UI support](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/UISupport.html)
- [Unity custom packages](https://docs.unity3d.com/6000.0/Documentation/Manual/CustomPackages.html)
- Installed Unity 6000.6.0f1, uGUI and Input System assemblies: API signatures used for compilation.

UI is generated with Unity's existing components in editor code. The package references Unity's built-in font through its API; the font file is not bundled. No original project prefab, TMP font, sprite, clip, scene, gameplay system or ECS/DOTS assembly is redistributed.
