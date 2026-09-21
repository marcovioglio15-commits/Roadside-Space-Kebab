# Menu Studio

Requires **Studio Colors**, uGUI and Input System. Open **Tools > Menu Studio**, create a preset, edit its compact tabs and Apply. In Create, choose Main or Pause and a destination scene, then **Create in scene**. The scene stays open for review and save; existing menu roots are not replaced. Scene creation supports Undo.

Main menu: Play, optional Settings/Credits, Quit. Pause menu: Resume, optional Restart/Settings, Main Menu, Quit. Optional quit confirmation is preauthored. Settings includes optional Audio, Video and Controls tabs with Apply/Back, focus styles and Input Action navigation. Controls is a project-authored reference, not rebinding.

Independent Main/Pause/Settings profiles expose per-state text, sprite and color overrides, per-button image content, whole-button/content motion, hover pulses and animation clips. Image IDs match generated object names such as `Play`, `Resume`, `Credits` or `ApplySettings`. Generated labels use uGUI Text and Unity Font assets; no source-project artwork or fonts are shipped.

Assign Play/Main Menu scene paths and include those scenes in Build Settings. An existing EventSystem is preserved; configure its Move/Submit and repeat behavior separately. Without one, generation creates an InputSystemUIInputModule and a project-owned action asset. Pause, Cancel and tab actions are assigned to the generated MenuInput.

UI objects, graphics and references are created only in the editor. Runtime opens existing panels, preserves the previous time scale/cursor and updates motion only while animation is active. Use `MenuHost.VisibilityChanged` to suspend project gameplay input while menus are open. `CommandRequested`, `AudioCue` and settings volume events are explicit project integration points. Master can use AudioListener; Music/SFX require a project audio adapter.

Structural preset edits do not silently rebuild existing scene roots: generate another instance when changing layout, enabled pages or input references. Save a selected MenuHost as a prefab through the Create tab; transfer its preset and generated input assets together when reusing that prefab.

Install Studio Colors first and then this package from disk, an embedded directory or its exported tarball. See `SOURCES.md` for provenance.
