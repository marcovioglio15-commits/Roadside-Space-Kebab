using CatOnASkateboard.StudioIdentity;
using CatOnASkateboard.StudioIdentity.Editor;
using UnityEditor;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Connects project flag management to portable preset values and dedicated object editors.</summary>
    [InitializeOnLoad]
    internal static class ObjectFlagProjectBridge
    {
        #region Methods
        #region Registration

        /// <summary>Registers editor-only integration after each assembly reload.</summary>
        static ObjectFlagProjectBridge()
        {
            ObjectFlagProject.PortableValueFactory = CreateValue;
            ObjectFlagProject.PortableArrayPredicate = ObjectFlagPortableArrays.IsFlagArray;
            ObjectFlagProject.OpenEditor += Open;
        }

        /// <summary>Builds one complete portable flag element without exposing preset storage to the flag package.</summary>
        /// <param name="flag">Selected persistent definition.</param>
        /// <param name="index">Zero-based position inside the flag array.</param>
        /// <returns>A serializable preset value with a normal asset link.</returns>
        private static object CreateValue(ObjectFlag flag, int index)
        {
            return new ObjectPresetValue
            {
                Name = "data[" + index + "]",
                Kind = SerializedPropertyType.ObjectReference,
                TypeName = "PPtr<$ObjectFlag>",
                Link = ObjectPresetLink.Capture(flag, null)
            };
        }

        #endregion
        #region Navigation

        /// <summary>Opens the existing editor belonging to the exact serialized owner.</summary>
        /// <param name="owner">Asset or component behind a flag selection.</param>
        /// <param name="path">Selection path retained by the project manager.</param>
        /// <returns>True when an object-specific editor handled the owner.</returns>
        private static bool Open(UnityEngine.Object owner, string path)
        {
            switch (owner)
            {
                case OrderCatalog catalog:
                    OrderCatalogWindow.Open(catalog);
                    break;
                case ContactModificationDefinition modification:
                    ContactModificationWindow.Open(modification);
                    break;
                case DialogueTriggerPreset trigger:
                    DialogueTriggerWindow.Open(trigger);
                    break;
                case DialogueEntry entry:
                    DialogueEntryWindow.Open(entry);
                    break;
                case DialogueTree tree:
                    DialogueTreeWindow.Open(tree);
                    break;
                case ObjectIdentity identity:
                    ObjectIdentityWindow.Open(identity);
                    break;
                case ObjectHover hover:
                    ObjectsLogicStudioWindow.Open(hover);
                    break;
                case ObjectSingleInteraction single:
                    ObjectsLogicStudioWindow.Open(single);
                    break;
                case ObjectExtendedInteraction extended:
                    ObjectsLogicStudioWindow.Open(extended);
                    break;
                case ObjectSharedPreset shared:
                    ObjectSharedPresetWindow.Open(shared);
                    break;
                case ObjectGroupPreset group:
                    ObjectGroupWindow.OpenPreset(group);
                    break;
                default:
                    return false;
            }
            // Asset editors can reveal the exact nested selection after opening their session.
            if (owner is UnityEngine.ScriptableObject asset)
                StudioAssetWindow.FocusSetting(asset, path, ObjectNames.NicifyVariableName(path.Substring(path.LastIndexOf('.') + 1)));
            return true;
        }

        #endregion
        #endregion
    }
}
