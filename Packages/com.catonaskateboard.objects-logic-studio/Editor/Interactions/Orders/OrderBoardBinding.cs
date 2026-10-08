using System;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Selects a loaded gameplay board from a prefab draft without storing a scene reference.</summary>
    internal static class OrderBoardBinding
    {
        #region Methods
        #region Controls

        /// <summary>Draws the board selector and opens the matched scene board without leaving Prefab Mode.</summary>
        /// <param name="identity">Stable board ID retained by the prefab.</param>
        internal static void Draw(SerializedProperty identity)
        {
            // Discovery is editor-only; runtime routing continues to use the stable board ID.
            OrderBoard[] boards = Resources.FindObjectsOfTypeAll<OrderBoard>();
            OrderBoard matched = Array.Find(boards, board => IsSceneBoard(board) && board.Identity == identity.stringValue);
            Rect rect = EditorGUILayout.GetControlRect();
            StudioFieldMenu.Context(rect, identity);
            using (new StudioFieldColors(rect, StudioFieldColors.Key(identity)))
            {
                rect = EditorGUI.PrefixLabel(rect, new GUIContent("Scene Board", "Select a board in a loaded gameplay scene. Only its ID is saved in the prefab."));
                if (EditorGUI.DropdownButton(rect, new GUIContent(matched != null
                    ? matched.gameObject.scene.name + " / " + matched.name + " (" + matched.Capacity + " slots)"
                    : identity.stringValue + " (not loaded)"), FocusType.Keyboard))
                    Menu(identity, boards).DropDown(rect);
            }
            StudioFieldGUI.PropertyField(identity, new GUIContent("Board ID", "Must match the destination board's ID, including when that scene is loaded later."));
            if (StudioButton.Draw(new GUIContent(matched != null ? "Edit Scene Board" : "Configure Scene Board",
                "Edit the gameplay board and its slots without closing the prefab workspace.")))
                OrderBoardWindow.Open(matched);
        }

        /// <summary>Filters out prefab assets and preview-stage boards.</summary>
        /// <param name="board">Candidate discovered by the editor.</param>
        /// <returns>True for a board belonging to a loaded gameplay scene.</returns>
        internal static bool IsSceneBoard(OrderBoard board)
        {
            return board != null && !EditorUtility.IsPersistent(board) && board.gameObject.scene.IsValid()
                && board.gameObject.scene.isLoaded && !EditorSceneManager.IsPreviewScene(board.gameObject.scene);
        }

        /// <summary>Builds delayed ID assignments guarded against draft navigation.</summary>
        /// <param name="property">Destination board ID.</param>
        /// <param name="boards">Loaded editor candidates.</param>
        /// <returns>A menu of gameplay board routes.</returns>
        private static GenericMenu Menu(SerializedProperty property, OrderBoard[] boards)
        {
            GenericMenu menu = new GenericMenu { allowDuplicateNames = true };
            UnityEngine.Object owner = property.serializedObject.targetObject;
            string path = property.propertyPath;
            Func<bool> guard = StudioFieldMenu.Guard(owner, path);
            foreach (OrderBoard board in boards)
                if (IsSceneBoard(board))
                    menu.AddItem(new GUIContent(board.gameObject.scene.name + "/" + board.name),
                        board.Identity == property.stringValue, () =>
                        {
                            if (!guard() || board == null)
                                return;
                            using SerializedObject data = new SerializedObject(owner);
                            data.FindProperty(path).stringValue = board.Identity;
                            data.ApplyModifiedProperties();
                            StudioFieldMenu.Notify(owner);
                        });
            if (menu.GetItemCount() == 0)
                menu.AddDisabledItem(new GUIContent("No boards in loaded gameplay scenes"));
            return menu;
        }

        #endregion
        #endregion
    }
}
