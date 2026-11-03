using System;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shows reveal controls and only the effect strengths used by this entry's text.</summary>
    internal static class DialogueTextControls
    {
        #region Methods

        #region Drawing

        /// <summary>Edits entry-wide letter timing and token-specific strengths.</summary>
        /// <param name="settings">Detached text-effects settings.</param>
        /// <param name="pages">Current pages used to detect active tokens.</param>
        internal static void Draw(SerializedProperty settings, DialogueLine[] pages)
        {
            settings.isExpanded = EditorGUILayout.Foldout(settings.isExpanded, new GUIContent("Text Effects", settings.tooltip), true);
            if (!settings.isExpanded)
                return;
            using StudioIndentScope indent = new StudioIndentScope();
            HoverControls.Field(settings, "Typewriter");
            if (settings.FindPropertyRelative("Typewriter").boolValue)
            {
                HoverControls.Field(settings, "LettersPerSecond");
                HoverControls.Field(settings, "PunctuationPause");
            }
            HoverControls.Field(settings, "Slam");
            if (settings.FindPropertyRelative("Slam").boolValue || Uses(pages, "slam"))
            {
                HoverControls.Field(settings, "SlamScale");
                HoverControls.Field(settings, "SlamHeight");
                HoverControls.Field(settings, "SlamDuration");
            }
            if (Uses(pages, "shake"))
            {
                HoverControls.Field(settings, "ShakeAmount");
                HoverControls.Field(settings, "ShakeRate");
            }
            if (Uses(pages, "wave"))
            {
                HoverControls.Field(settings, "WaveAmount");
                HoverControls.Field(settings, "WaveRate");
            }
            if (Uses(pages, "rainbow"))
                HoverControls.Field(settings, "RainbowRate");
            if (Uses(pages, "rotate", false))
                HoverControls.Field(settings, "RotationDegrees");
        }

        /// <summary>Wraps the selected text or inserts a token example at the retained caret.</summary>
        /// <param name="text">Page text receiving the selected example.</param>
        /// <param name="selection">Native text selection retained while the menu opens.</param>
        /// <param name="colors">Entry palette used by the Color submenu.</param>
        internal static void Tokens(SerializedProperty text, DialogueTextSelection selection, DialogueNamedColor[] colors)
        {
            Rect rect = EditorGUILayout.GetControlRect();
            if (!GUI.Button(rect, new GUIContent("Insert Text Token", "Wrap selected text. Without a selection, insert at the caret. Use [[ for a literal opening bracket."), EditorStyles.popup))
                return;
            UnityEngine.Object owner = text.serializedObject.targetObject;
            string path = text.propertyPath;
            Func<bool> guard = StudioFieldMenu.Guard(owner, path);
            DialogueTextSelection.Range range = selection.Capture(text);
            GenericMenu menu = new GenericMenu();
            foreach ((string label, string opening, string closing, string example) in new[]
                { ("Shake", "[shake]", "[/shake]", "word"), ("Wave", "[wave]", "[/wave]", "word"), ("Slam", "[slam]", "[/slam]", "word"),
                    ("Rotate", "[rotate]", "[/rotate]", "words"), ("Rainbow", "[rainbow]", "[/rainbow]", "word"),
                    ("Speed", "[speed=25]", "[/speed]", "words"), ("Pause", "[pause=0.3]", "", ""), ("Literal [", "[[", "", "") })
                AddToken(menu, label, owner, path, guard, selection, range, opening, closing, example);
            if (colors == null || colors.Length == 0)
                menu.AddDisabledItem(new GUIContent("Color/Add colors in Text Colors"));
            else
                foreach (DialogueNamedColor color in colors)
                    if (!string.IsNullOrWhiteSpace(color.Name))
                        AddToken(menu, "Color/" + color.Name, owner, path, guard, selection, range, "[color=" + color.Name + "]", "[/color]", "word");
            menu.DropDown(rect);
        }

        /// <summary>Uses one guarded insertion path for ordinary tokens and named color submenu items.</summary>
        /// <param name="menu">Native token menu.</param>
        /// <param name="label">Caption or submenu path.</param>
        /// <param name="owner">Detached entry receiving the insertion.</param>
        /// <param name="path">Serialized page text path.</param>
        /// <param name="guard">Original selection lifetime check.</param>
        /// <param name="selection">Native caret and selection manager.</param>
        /// <param name="range">Text selected before the menu opened.</param>
        /// <param name="opening">Opening token.</param>
        /// <param name="closing">Optional matching close.</param>
        /// <param name="example">Initial text used only at an empty caret.</param>
        private static void AddToken(GenericMenu menu, string label, UnityEngine.Object owner, string path, Func<bool> guard,
            DialogueTextSelection selection, DialogueTextSelection.Range range, string opening, string closing, string example)
        {
            menu.AddItem(new GUIContent(label), false, () =>
            {
                if (!guard())
                    return;
                using SerializedObject data = new SerializedObject(owner);
                selection.Insert(data.FindProperty(path), range, opening, closing, example);
                data.ApplyModifiedProperties();
                StudioFieldMenu.Notify(owner);
            });
        }

        /// <summary>Checks page text without constructing per-frame token lists.</summary>
        /// <param name="pages">Entry's explicit pages.</param>
        /// <param name="token">Effect name.</param>
        /// <param name="includeArguments">Include explicit-argument tokens as well as the bare form.</param>
        /// <returns>True when a page enables this effect.</returns>
        private static bool Uses(DialogueLine[] pages, string token, bool includeArguments = true)
        {
            return pages != null && Array.Exists(pages, page => page?.Text != null
                && (page.Text.Contains("[" + token + "]", StringComparison.Ordinal)
                    || includeArguments && page.Text.Contains("[" + token + "=", StringComparison.Ordinal)));
        }

        #endregion

        #endregion
    }
}
