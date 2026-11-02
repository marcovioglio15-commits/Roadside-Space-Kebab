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
        }

        /// <summary>Wraps the selected text or inserts a token example at the retained caret.</summary>
        /// <param name="text">Page text receiving the selected example.</param>
        /// <param name="selection">Native text selection retained while the menu opens.</param>
        internal static void Tokens(SerializedProperty text, DialogueTextSelection selection)
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
                    ("Color", "[color=#ffcc00]", "[/color]", "word"), ("Rainbow", "[rainbow]", "[/rainbow]", "word"),
                    ("Speed", "[speed=25]", "[/speed]", "words"), ("Pause", "[pause=0.3]", "", ""), ("Literal [", "[[", "", "") })
                menu.AddItem(new GUIContent(label), false, () =>
                {
                    if (!guard())
                        return;
                    using SerializedObject data = new SerializedObject(owner);
                    SerializedProperty destination = data.FindProperty(path);
                    selection.Insert(destination, range, opening, closing, example);
                    data.ApplyModifiedProperties();
                    StudioFieldMenu.Notify(owner);
                });
            menu.DropDown(rect);
        }

        /// <summary>Checks page text without constructing per-frame token lists.</summary>
        /// <param name="pages">Entry's explicit pages.</param>
        /// <param name="token">Effect name.</param>
        /// <returns>True when a page enables this effect.</returns>
        private static bool Uses(DialogueLine[] pages, string token)
        {
            return pages != null && Array.Exists(pages, page => page?.Text != null && page.Text.Contains("[" + token + "]", StringComparison.Ordinal));
        }

        #endregion

        #endregion
    }
}
