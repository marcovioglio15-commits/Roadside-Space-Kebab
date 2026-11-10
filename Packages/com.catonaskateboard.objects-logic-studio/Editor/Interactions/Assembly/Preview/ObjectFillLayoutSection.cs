using System;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Addresses one Actual Item Fill layout by its unique count, even after step reordering.</summary>
    internal static class ObjectFillLayoutSection
    {
        #region State

        private const string Steps = "Single.Draft.Container.FillSteps";
        private const string Baseline = "Single.Baseline.Container.FillSteps";
        private const string Magnets = "ItemFill.Magnets";

        #endregion

        #region Methods

        #region Transactions

        /// <summary>Merges a layout without replacing the other steps or unrelated flags on this step.</summary>
        /// <param name="proposal">Detached fill preview settings and applied baseline.</param>
        /// <param name="current">Fresh container transaction.</param>
        /// <param name="threshold">Unique step count.</param>
        internal static void Merge(SerializedObject proposal, SerializedObject current, int threshold)
        {
            using SerializedProperty steps = current.FindProperty(Steps);
            using SerializedProperty original = Find(proposal.FindProperty(Baseline), threshold);
            using SerializedProperty edit = Find(proposal.FindProperty(Steps), threshold);
            using SerializedProperty live = Find(steps, threshold);
            // Insert only the requested step. A competing addition or deletion needs review.
            if (original == null && live == null)
            {
                Insert(steps, edit);
                return;
            }
            if (original == null || live == null)
            {
                if (live != null && live.contentHash == edit.contentHash)
                    return;
                throw new InvalidOperationException("This fill step was added or removed elsewhere. Reload the container step.");
            }
            using SerializedProperty mode = live.FindPropertyRelative("Mode");
            ObjectSectionChanges.Merge(original.FindPropertyRelative("Mode"), edit.FindPropertyRelative("Mode"), mode);
            ObjectSectionChanges.Merge(original.FindPropertyRelative(Magnets), edit.FindPropertyRelative(Magnets), live.FindPropertyRelative(Magnets));
            if (original.FindPropertyRelative("Mode").enumValueIndex != (int)InventoryFillMode.ActualItemFill)
                ObjectSectionChanges.Merge(original.FindPropertyRelative("ItemFill.VisualPrefab"), edit.FindPropertyRelative("ItemFill.VisualPrefab"),
                    live.FindPropertyRelative("ItemFill.VisualPrefab"));
        }

        /// <summary>Rebases one saved layout while preserving other local step changes.</summary>
        /// <param name="applied">Document containing the applied fill step.</param>
        /// <param name="destination">Pending proposal receiving the new layout.</param>
        /// <param name="threshold">Unique step count.</param>
        /// <param name="updateBaseline">Whether to update the corresponding applied baseline too.</param>
        internal static void Transfer(SerializedObject applied, SerializedObject destination, int threshold, bool updateBaseline)
        {
            using SerializedProperty step = Find(applied.FindProperty(Steps), threshold);
            Transfer(step, destination.FindProperty(Steps), threshold);
            if (updateBaseline)
                Transfer(step, destination.FindProperty(Baseline), threshold);
        }

        /// <summary>Copies placement and any prerequisite fill mode into a matching step.</summary>
        /// <param name="source">Saved step with the new layout.</param>
        /// <param name="steps">Destination array.</param>
        /// <param name="threshold">Unique step count.</param>
        private static void Transfer(SerializedProperty source, SerializedProperty steps, int threshold)
        {
            using SerializedProperty target = Find(steps, threshold);
            if (target == null)
            {
                Insert(steps, source);
                return;
            }
            using SerializedProperty mode = target.FindPropertyRelative("Mode");
            if (mode.enumValueIndex != (int)InventoryFillMode.ActualItemFill)
            {
                mode.enumValueIndex = (int)InventoryFillMode.ActualItemFill;
                Copy(source, target, "ItemFill.VisualPrefab");
            }
            Copy(source, target, Magnets);
        }

        #endregion

        #region Properties

        /// <summary>Locates the requested threshold in a serialized container array.</summary>
        /// <param name="steps">Container step array.</param>
        /// <param name="threshold">Unique step count.</param>
        /// <returns>The matching step, or null if it has not been created.</returns>
        private static SerializedProperty Find(SerializedProperty steps, int threshold)
        {
            for (int index = 0; index < steps.arraySize; index++)
            {
                using SerializedProperty candidate = steps.GetArrayElementAtIndex(index);
                if (candidate.FindPropertyRelative("Count").intValue == threshold)
                    return candidate.Copy();
            }
            return null;
        }

        /// <summary>Appends a complete new step without modifying adjacent layouts.</summary>
        /// <param name="steps">Destination array.</param>
        /// <param name="step">New threshold settings.</param>
        private static void Insert(SerializedProperty steps, SerializedProperty step)
        {
            int index = steps.arraySize;
            steps.arraySize++;
            new StudioPropertyValue(step).Apply(steps.GetArrayElementAtIndex(index));
        }

        /// <summary>Copies one named value between two corresponding steps.</summary>
        /// <param name="source">Saved step.</param>
        /// <param name="target">Step receiving the value.</param>
        /// <param name="path">Relative serialized value path.</param>
        private static void Copy(SerializedProperty source, SerializedProperty target, string path)
        {
            new StudioPropertyValue(source.FindPropertyRelative(path)).Apply(target.FindPropertyRelative(path));
        }

        #endregion

        #endregion
    }
}
