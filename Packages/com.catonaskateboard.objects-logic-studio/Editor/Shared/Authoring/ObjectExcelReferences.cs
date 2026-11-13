using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioIdentity;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares existing flag selector labels with contextual spreadsheet actions.</summary>
    [InitializeOnLoad]
    internal static class ObjectExcelReferences
    {
        #region Methods

        /// <summary>Registers flag labels once after an editor assembly reload.</summary>
        static ObjectExcelReferences()
        {
            StudioPresetActions.RegisterReferenceLabel<ObjectFlag>(flag => flag.DisplayName);
            StudioPresetActions.ProposalReview += ReviewOrders;
        }

        /// <summary>Reports stored dialogue and open-scene board keys affected by catalog replacement or renaming.</summary>
        /// <param name="source">Exact persistent preset.</param>
        /// <param name="before">Current isolated catalog.</param>
        /// <param name="after">Proposed isolated catalog.</param>
        /// <param name="warnings">Receives concise reference diagnostics.</param>
        private static void ReviewOrders(ScriptableObject source, UnityEngine.Object before, UnityEngine.Object after, List<string> warnings)
        {
            if (source is not OrderCatalog || before is not OrderCatalog previous || after is not OrderCatalog proposed)
                return;
            HashSet<string> removed = new HashSet<string>(previous.CandidateNames(), StringComparer.Ordinal);
            removed.ExceptWith(proposed.CandidateNames());
            if (removed.Count == 0)
                return;
            warnings.Add("Removed order keys: " + string.Join(", ", removed) + ". Review dialogue filters and Matching Orders slots.");
            // Reference discovery runs only for an explicit preview that removes existing keys.
            foreach (string guid in AssetDatabase.FindAssets("t:DialogueTriggerPreset"))
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                    if (asset is DialogueTriggerPreset trigger && trigger.Consumption?.Orders != null
                        && Array.Exists(trigger.Consumption.Orders, removed.Contains))
                        warnings.Add("Dialogue trigger '" + trigger.name + "' contains a removed order key.");
            foreach (OrderBoard board in UnityEngine.Object.FindObjectsByType<OrderBoard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                using SerializedObject data = new SerializedObject(board);
                if (data.FindProperty("mode").enumValueIndex != (int)OrderBoardMode.MatchingOrders)
                    continue;
                SerializedProperty names = data.FindProperty("orderNames");
                for (int index = 0; index < names.arraySize; index++)
                    if (removed.Contains(names.GetArrayElementAtIndex(index).stringValue))
                    {
                        warnings.Add("Order Board '" + board.name + "' contains a removed order key.");
                        break;
                    }
            }
        }

        #endregion
    }
}
