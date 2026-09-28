using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    internal static class StudioCleanupFlagCheck
    {
        public static void Run()
        {
            const string path = "Assets/CodexStudioCleanupFlag.asset";
            if (File.Exists(path))
                throw new InvalidOperationException("Temporary flag already exists.");
            ObjectFlag flag = ScriptableObject.CreateInstance<ObjectFlag>();
            flag.DisplayName = "Temporary Flag Check";
            AssetDatabase.CreateAsset(flag, path);
            try
            {
                ObjectFlagEditWindow.Open(flag);
                ObjectFlagEditWindow window = Resources.FindObjectsOfTypeAll<ObjectFlagEditWindow>().Single();
                using SerializedObject state = new SerializedObject(window);
                if (state.FindProperty("flag").objectReferenceValue != flag)
                    throw new InvalidOperationException("Edit window did not retain the chosen flag.");
                using SerializedObject data = new SerializedObject(flag);
                data.FindProperty("DisplayName").stringValue = "Edited Flag Check";
                data.ApplyModifiedProperties();
                window.Close();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                if (AssetDatabase.LoadAssetAtPath<ObjectFlag>(path).DisplayName != "Edited Flag Check")
                    throw new InvalidOperationException("Closing the editor failed to save the changed flag.");
                if (!AssetDatabase.MoveAssetToTrash(path) || AssetDatabase.LoadAssetAtPath<ObjectFlag>(path) != null)
                    throw new InvalidOperationException("Flag deletion did not remove the definition.");
                File.WriteAllText("Library/CodexStudioCleanupCheck/flag-checks.txt", "PASS Edit opens selected flag, edits persist on close, Delete removes definition through AssetDatabase.");
                Debug.Log("STUDIO_FLAG_CHECK_OK");
            }
            finally
            {
                foreach (ObjectFlagEditWindow window in Resources.FindObjectsOfTypeAll<ObjectFlagEditWindow>())
                    window.Close();
                if (File.Exists(path))
                    AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
