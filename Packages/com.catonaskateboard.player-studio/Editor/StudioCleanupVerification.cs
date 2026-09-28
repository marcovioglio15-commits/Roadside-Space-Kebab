using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    internal static class StudioCleanupVerification
    {
        private static readonly List<string> results = new List<string>();
        private const string report = "Library/CodexStudioCleanupCheck/";

        public static void Run()
        {
            InspectInputs();
            CheckSlots(PlayerToolLayout.Independent, PlayerToolSlotMotion.Direct, true);
            CheckSlots(PlayerToolLayout.Cyclic, PlayerToolSlotMotion.Direct, true);
            CheckSlots(PlayerToolLayout.Cyclic, PlayerToolSlotMotion.AroundPivot, true);
            CheckSlots(PlayerToolLayout.Cyclic, PlayerToolSlotMotion.AroundPivot, false);
            CheckHierarchy();
            CheckProjectInput();
            CheckCreation();
            CheckAssets();
            File.WriteAllLines(report + "player-checks.txt", results);
            Debug.Log("STUDIO_PLAYER_CHECKS_OK " + results.Count);
        }

        public static void MapMenu()
        {
            InputActionAsset source = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/ScriptableObjects/Menu/MenuInput.asset");
            InputActionAsset controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/ScriptableObjects/Player Configuration/Controls.inputactions");
            List<string> mappings = new List<string>();
            foreach (Object original in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(source)))
            {
                Object replacement;
                if (original is InputActionAsset)
                    replacement = controls;
                else if (original is InputActionReference reference)
                {
                    InputAction action = controls.FindAction(reference.action.id);
                    Require(action != null && action.name == reference.action.name, "menu ID and name preserved");
                    replacement = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controls)).OfType<InputActionReference>()
                        .First(r => r.action.id == action.id);
                }
                else
                    continue;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original, out string oldGuid, out long oldId);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(replacement, out string newGuid, out long newId);
                mappings.Add("fileID: " + oldId + ", guid: " + oldGuid + " => fileID: " + newId + ", guid: " + newGuid);
            }
            Require(mappings.Count == source.Select(a => a).Count() + 1, "every menu action has a matching reference");
            File.WriteAllLines(report + "menu-mapping.txt", mappings);
            Debug.Log("STUDIO_MENU_MAP_OK " + mappings.Count);
        }

        private static void InspectInputs()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:InputActionAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/") && !path.StartsWith("Packages/com.catonaskateboard."))
                    continue;
                InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                results.Add("INPUT " + path + " :: " + string.Join(",", asset.Select(a => a.actionMap.name + "/" + a.name)));
                if (path == "Assets/ScriptableObjects/Menu/MenuInput.asset")
                    File.WriteAllText(report + "menu-input.json", asset.ToJson());
            }
        }

        private static void CheckSlots(PlayerToolLayout layout, PlayerToolSlotMotion motion, bool clockwise)
        {
            GameObject root = new GameObject("Tool Test");
            root.SetActive(false);
            PlayerHost host = root.AddComponent<PlayerHost>();
            PlayerTools tools = root.AddComponent<PlayerTools>();
            PlayerMasterPreset master = ScriptableObject.CreateInstance<PlayerMasterPreset>();
            PlayerToolsPreset preset = ScriptableObject.CreateInstance<PlayerToolsPreset>();
            PlayerTool[] identities = new PlayerTool[3];
            Transform[] children = new Transform[3];
            try
            {
                Transform rig = new GameObject("Rig").transform;
                rig.SetParent(root.transform, false);
                Transform unrelated = new GameObject("Camera").transform;
                unrelated.SetParent(root.transform, false);
                preset.RootPath = "Rig";
                preset.Layout = layout;
                preset.SlotMotion = motion;
                preset.Clockwise = clockwise;
                preset.SwitchDuration = 1f;
                preset.Tools = new PlayerToolEntry[3];
                preset.Slots = new PlayerToolPose[3];
                for (int index = 0; index < 3; index++)
                {
                    identities[index] = ScriptableObject.CreateInstance<PlayerTool>();
                    identities[index].DisplayName = "Tool " + index;
                    children[index] = new GameObject("Part " + index).transform;
                    children[index].SetParent(rig, false);
                    children[index].localPosition = Vector3.one * (10f + index);
                    preset.Tools[index] = new PlayerToolEntry { Tool = identities[index], MoveVisual = true, Path = children[index].name,
                        PassivePose = new PlayerToolPose { Position = Vector3.up * (index + 2), Scale = Vector3.one } };
                    preset.Slots[index] = new PlayerToolPose { Position = Quaternion.Euler(0f, 0f, 120f * index) * Vector3.right,
                        Rotation = Vector3.forward * 120f * index, Scale = Vector3.one };
                }
                preset.ActivePose = new PlayerToolPose { Position = Vector3.right, Scale = Vector3.one };
                PlayerCreationUtility.SetReferences(master, ("toolsPreset", preset));
                PlayerCreationUtility.SetReferences(host, ("masterPreset", master));
                root.SetActive(true);
                Invoke(tools, "Initialize");
                Require(tools.IsReady && tools.ActiveTool == null, "initial unarmed");
                for (int i = 0; i < 3; i++)
                    Near(children[i].localPosition, preset.Tools[i].PassivePose.Position, "initial parked");
                for (int iteration = 0; iteration < 9; iteration++)
                {
                    int selected = iteration % 3;
                    Require(tools.Select(identities[selected]), "select");
                    Invoke(tools, "BeginSwitch");
                    Invoke(tools, "Advance", 0.5f);
                    Require(tools.IsSwitching && tools.ActiveTool == null, "no usable tool during switching");
                    if (motion == PlayerToolSlotMotion.AroundPivot && iteration > 0)
                        Require(Mathf.Abs(children[0].localPosition.magnitude - 1f) < 0.0001f, "wheel maintains radius");
                    Invoke(tools, "Advance", 0.5f);
                    Require(!tools.IsSwitching && tools.ActiveTool == identities[selected], "selection completes");
                    for (int i = 0; i < 3; i++)
                        Near(children[i].localPosition, preset.Destination(i, selected).Position, "slot destination");
                }
                tools.Select(identities[0]);
                Invoke(tools, "BeginSwitch");
                tools.Select(identities[2]);
                Invoke(tools, "Advance", 1f);
                Require(tools.ActiveTool == identities[0], "queued selection cannot redirect a running transition");
                Invoke(tools, "BeginSwitch");
                Invoke(tools, "Advance", 1f);
                Require(tools.ActiveTool == identities[2], "queued selection finishes next");
                tools.Select(null);
                Invoke(tools, "BeginSwitch");
                Invoke(tools, "Advance", 1f);
                for (int i = 0; i < 3; i++)
                    Near(children[i].localPosition, preset.Tools[i].PassivePose.Position, "unarmed parking");
                unrelated.localPosition = Vector3.one * 13f;
                root.transform.position = Vector3.one * 7f;
                tools.enabled = false;
                Invoke(tools, "OnDisable");
                Near(unrelated.localPosition, Vector3.one * 13f, "unrelated camera remains untouched");
                Near(root.transform.position, Vector3.one * 7f, "player root remains untouched");
                for (int i = 0; i < 3; i++)
                    Near(children[i].localPosition, Vector3.one * (10f + i), "disable restores authored targets");
                results.Add("PASS slots " + layout + " " + motion + " clockwise=" + clockwise);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(preset);
                Object.DestroyImmediate(master);
                foreach (PlayerTool identity in identities)
                    if (identity != null)
                        Object.DestroyImmediate(identity);
            }
        }

        private static void CheckHierarchy()
        {
            GameObject root = new GameObject("Hierarchy Test");
            try
            {
                Transform child = new GameObject("Tool").transform;
                child.SetParent(root.transform, false);
                Require(PlayerHierarchy.Resolve(root.transform, "Tool") == child, "unique path");
                Transform duplicate = new GameObject("Tool").transform;
                duplicate.SetParent(root.transform, false);
                Require(PlayerHierarchy.Resolve(root.transform, "Tool") == null, "ambiguous path rejected");
                Require(PlayerHierarchy.Resolve(root.transform, "Missing") == null, "missing path rejected");
                Require(PlayerHierarchy.Resolve(root.transform, "") == root.transform, "explicit root");
                results.Add("PASS hierarchy unique, missing and duplicate-name targets");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void CheckProjectInput()
        {
            PlayerMasterPreset master = AssetDatabase.LoadAssetAtPath<PlayerMasterPreset>("Assets/ScriptableObjects/Player Configuration/Master.asset");
            Require(PlayerCreationUtility.TryValidate(master, out InputActionAsset actions, out string warning), warning);
            Require(actions == InputSystem.actions, "project-wide Controls is accepted as player input");
            foreach (string guid in AssetDatabase.FindAssets("t:InputActionReference"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/") && !path.StartsWith("Packages/com.catonaskateboard."))
                    continue;
                foreach (InputActionReference reference in AssetDatabase.LoadAllAssetsAtPath(path).OfType<InputActionReference>())
                    Require(reference.action != null, "unresolved action: " + path + " " + reference.name);
            }
            results.Add("PASS project-wide Controls accepted and all authored action references resolve");
        }

        private static void CheckCreation()
        {
            const string folder = "Assets/CodexStudioCleanupTemporary";
            Require(!AssetDatabase.IsValidFolder(folder), "test folder must be unique");
            AssetDatabase.CreateFolder("Assets", "CodexStudioCleanupTemporary");
            try
            {
                AssetDatabase.CreateFolder(folder, "Defaults");
                PlayerMasterPreset empty = PlayerDefaultAssets.CreateMissing(folder + "/Defaults");
                Require(empty.InputPreset == null && empty.LocomotionPreset == null, "new defaults leave input unassigned");
                Require(Directory.GetFiles(folder, "*.inputactions", SearchOption.AllDirectories).Length == 0, "bootstrap creates no input asset");
                AssetDatabase.CreateFolder(folder, "Copy");
                PlayerMasterPreset source = AssetDatabase.LoadAssetAtPath<PlayerMasterPreset>("Assets/ScriptableObjects/Player Configuration/Master.asset");
                PlayerMasterPreset copied = PlayerConfigurationCopy.Copy(source, folder + "/Copy");
                Require(copied.InputPreset != source.InputPreset, "copied input preset stays independent");
                using SerializedObject original = new SerializedObject(source.InputPreset);
                using SerializedObject clone = new SerializedObject(copied.InputPreset);
                Require(original.FindProperty("movementAction").objectReferenceValue == clone.FindProperty("movementAction").objectReferenceValue,
                    "copy retains user-owned action reference");
                Require(Directory.GetFiles(folder, "*.inputactions", SearchOption.AllDirectories).Length == 0, "copy creates no input asset");
                results.Add("PASS new defaults and configuration copies create no input action assets");
                GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PF_Player.prefab");
                UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
                GameObject instance = Object.Instantiate(sourcePrefab);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, scene);
                try
                {
                    PlayerStudioState state = new PlayerStudioState { PreviewHost = instance.GetComponent<PlayerHost>() };
                    state.CameraScene.Refresh(state.PreviewHost);
                    GameObject player = (GameObject)typeof(PlayerQuickPlayScene).GetMethod("CreatePlayer", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { state, copied, scene });
                    foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                        Require(PlayerHierarchy.Resolve(player.transform, AnimationUtility.CalculateTransformPath(child, instance.transform)) != null,
                            "Quick Play retains " + child.name);
                    Require(player.GetComponent<PlayerCameraRig>().View.transform.IsChildOf(player.transform), "Quick Play camera reference belongs to copy");
                    results.Add("PASS Quick Play retains actual player hierarchy and local camera references");
                }
                finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }

        private static void CheckAssets()
        {
            int prefabs = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/") && !path.StartsWith("Packages/com.catonaskateboard."))
                    continue;
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0, "missing script: " + path + " " + child.name);
                prefabs++;
            }
            results.Add("PASS no missing scripts in " + prefabs + " authored prefabs");
        }

        private static void Invoke(object target, string method, params object[] args)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        }

        private static void Near(Vector3 actual, Vector3 expected, string description)
        {
            Require((actual - expected).sqrMagnitude < 0.000001f, description + ": " + actual + " vs " + expected);
        }

        private static void Require(bool condition, string description)
        {
            if (!condition)
                throw new InvalidOperationException(description);
        }
    }
}
