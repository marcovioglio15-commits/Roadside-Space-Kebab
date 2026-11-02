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

        /// <summary>Inserts a complete editable token example into the selected page.</summary>
        /// <param name="text">Page text receiving the selected example.</param>
        internal static void Tokens(SerializedProperty text)
        {
            Rect rect = EditorGUILayout.GetControlRect();
            if (!GUI.Button(rect, new GUIContent("Insert Text Token", "Wrap words with matching effect tokens. Use [[ for a literal opening bracket."), EditorStyles.popup))
                return;
            UnityEngine.Object owner = text.serializedObject.targetObject;
            string path = text.propertyPath;
            Func<bool> guard = StudioFieldMenu.Guard(owner, path);
            GenericMenu menu = new GenericMenu();
            foreach ((string label, string token) in new[]
                { ("Shake", "[shake]word[/shake]"), ("Wave", "[wave]word[/wave]"), ("Slam", "[slam]word[/slam]"),
                    ("Color", "[color=#ffcc00]word[/color]"), ("Rainbow", "[rainbow]word[/rainbow]"),
                    ("Speed", "[speed=25]words[/speed]"), ("Pause", "[pause=0.3]"), ("Literal [", "[[") })
                menu.AddItem(new GUIContent(label), false, () =>
                {
                    if (!guard())
                        return;
                    using SerializedObject data = new SerializedObject(owner);
                    SerializedProperty destination = data.FindProperty(path);
                    destination.stringValue += (string.IsNullOrEmpty(destination.stringValue) ? string.Empty : " ") + token;
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
