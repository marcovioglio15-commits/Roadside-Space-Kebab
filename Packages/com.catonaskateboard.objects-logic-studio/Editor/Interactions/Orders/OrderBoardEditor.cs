using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits board slots individually in both the inspector and the scene-board window.</summary>
    [CustomEditor(typeof(OrderBoard))]
    internal sealed class OrderBoardEditor : UnityEditor.Editor
    {
        #region Methods
        #region Drawing

        /// <summary>Uses safe array controls for existing authored text references.</summary>
        public override void OnInspectorGUI()
        {
            // Reuse numeric sliders in both the component inspector and the scene-board window.
            using ObjectStudioFieldLayout layout = new ObjectStudioFieldLayout(160f);
            using StudioFieldRenderScope render = new StudioFieldRenderScope(ObjectNumericFields.Draw);
            serializedObject.Update();
            SerializedProperty property = serializedObject.GetIterator();
            // The shared field renderer keeps array resizing behind explicit add/remove commands.
            if (property.NextVisible(true))
                while (property.NextVisible(false))
                {
                    bool matching = serializedObject.FindProperty("mode").enumValueIndex == (int)OrderBoardMode.MatchingOrders;
                    bool objects = serializedObject.FindProperty("showSlotObjects").boolValue;
                    bool tint = objects && serializedObject.FindProperty("tintByState").boolValue;
                    bool stagger = objects && serializedObject.FindProperty("staggerSlotObjects").boolValue;
                    if (property.name == "slotObjectDelays")
                    {
                        if (stagger)
                            DrawDelays(property);
                        continue;
                    }
                    if (property.name switch
                        {
                            "orderNames" => matching,
                            "slotObjects" or "tintByState" or "staggerSlotObjects" => objects,
                            "slotObjectDelay" => stagger,
                            "pendingTint" or "completedTint" or "incorrectTint" => tint,
                            _ => true
                        })
                        StudioFieldGUI.PropertyField(property, true);
                }
            serializedObject.ApplyModifiedProperties();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (StudioButton.Draw(new UnityEngine.GUIContent("Prepare Order Paragraphs", "Prepare existing slots for variant paragraphs and optional custom typography before Play.")))
                    OrderBoardTextAuthoring.Prepare((OrderBoard)target);
        }

        /// <summary>Offers one optional delay per existing slot without exposing unused override fields.</summary>
        /// <param name="overrides">Optional serialized intervals in text-slot order.</param>
        private void DrawDelays(SerializedProperty overrides)
        {
            // Create override records only after an explicit user edit.
            SerializedProperty slots = serializedObject.FindProperty("slots");
            for (int index = 0; index < slots.arraySize; index++)
            {
                SerializedProperty entry = index < overrides.arraySize ? overrides.GetArrayElementAtIndex(index) : null;
                bool current = entry != null && entry.FindPropertyRelative("Override").boolValue;
                bool selected = StudioFieldGUI.Toggle(new UnityEngine.GUIContent("Slot " + (index + 1) + " Override",
                    "Use a separate interval before this slot appears."), current);
                if (selected != current)
                {
                    while (overrides.arraySize <= index)
                    {
                        overrides.arraySize++;
                        overrides.GetArrayElementAtIndex(overrides.arraySize - 1).boxedValue = new OrderSlotRevealDelay();
                    }
                    entry = overrides.GetArrayElementAtIndex(index);
                    entry.FindPropertyRelative("Override").boolValue = selected;
                }
                if (selected)
                    using (new StudioIndentScope())
                        StudioFieldGUI.PropertyField(entry.FindPropertyRelative("Delay"));
            }
        }

        #endregion
        #endregion
    }
}
