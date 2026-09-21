# Sources

This editor utility was extracted from this repository's existing `PlayerInputActionMenu.cs` and made reusable by Player Studio and Objects Logic Studio. The original project code is shared rather than duplicated. No third-party implementation was copied.

References: installed Unity 6000.6 editor APIs (`AssetDatabase`, `SerializedObject`, `EditorGUILayout.Popup`, `EditorApplication.projectChanged`) and Input System 1.20 public types (`InputActionAsset`, `InputActionReference`, stable action IDs).

This package contains editor code only. It does not create input actions, enable maps, pair devices or modify runtime input ownership.
