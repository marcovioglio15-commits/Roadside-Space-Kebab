using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    internal static class StudioCleanupPlayCheck
    {
        private const string folder = "Assets/CodexStudioCleanupPlayTemporary";
        private static GameObject root;
        private static Transform first;
        private static Transform cameraAnchor;
        private static PlayerTools tools;
        private static PlayerTool one;
        private static PlayerTool two;
        private static PlayerToolsPreset preset;
        private static PlayerMasterPreset master;
        private static PlayerInputPreset inputPreset;
        private static PlayerBodyPreset body;
        private static Keyboard keyboard;
        private static int phase;
        private static int session;
        private static double deadline;
        private static string failure;
        private static bool originalOptionsEnabled;
        private static EnterPlayModeOptions originalOptions;

        public static void Run()
        {
            if (AssetDatabase.IsValidFolder(folder))
                throw new InvalidOperationException("Temporary play folder already exists.");
            AssetDatabase.CreateFolder("Assets", "CodexStudioCleanupPlayTemporary");
            originalOptions = EditorSettings.enterPlayModeOptions;
            originalOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PlayerQuickPlayInputScope.Acquire();
            keyboard = InputSystem.AddDevice<Keyboard>();
            root = new GameObject("Runtime Tools Test");
            root.SetActive(false);
            CharacterController controller = root.AddComponent<CharacterController>();
            PlayerHost host = root.AddComponent<PlayerHost>();
            PlayerInput input = root.AddComponent<PlayerInput>();
            tools = root.AddComponent<PlayerTools>();
            first = new GameObject("Tool One").transform;
            first.SetParent(root.transform, false);
            first.localPosition = Vector3.left * 3f;
            Transform second = new GameObject("Tool Two").transform;
            second.SetParent(root.transform, false);
            cameraAnchor = new GameObject("Unrelated Camera Anchor").transform;
            cameraAnchor.SetParent(root.transform, false);
            body = ScriptableObject.CreateInstance<PlayerBodyPreset>();
            master = ScriptableObject.CreateInstance<PlayerMasterPreset>();
            inputPreset = ScriptableObject.CreateInstance<PlayerInputPreset>();
            one = ScriptableObject.CreateInstance<PlayerTool>();
            two = ScriptableObject.CreateInstance<PlayerTool>();
            preset = ScriptableObject.CreateInstance<PlayerToolsPreset>();
            preset.Tools = new[]
            {
                new PlayerToolEntry { Tool = one, MoveVisual = true, Path = "Tool One", PassivePose = PlayerToolPose.Identity },
                new PlayerToolEntry { Tool = two, MoveVisual = true, Path = "Tool Two", PassivePose = PlayerToolPose.Identity }
            };
            preset.ActivePose = new PlayerToolPose { Position = Vector3.up, Scale = Vector3.one };
            preset.SwitchDuration = 0.08f;
            InputActionAsset controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/ScriptableObjects/Player Configuration/Controls.inputactions");
            InputActionReference[] references = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controls)).OfType<InputActionReference>().ToArray();
            PlayerCreationUtility.SetReferences(inputPreset,
                ("movementAction", references.First(r => r.action == controls.FindAction("Player/Move"))),
                ("tools.UseTool", references.First(r => r.action == controls.FindAction("Player/UseTool"))));
            PlayerCreationUtility.SetReferences(master, ("bodyPreset", body), ("inputPreset", inputPreset), ("toolsPreset", preset));
            PlayerCreationUtility.SetReferences(host, ("masterPreset", master), ("bodyController", controller));
            AssetDatabase.CreateAsset(body, folder + "/Body.asset");
            AssetDatabase.CreateAsset(inputPreset, folder + "/Input.asset");
            AssetDatabase.CreateAsset(one, folder + "/One.asset");
            AssetDatabase.CreateAsset(two, folder + "/Two.asset");
            AssetDatabase.CreateAsset(preset, folder + "/Tools.asset");
            AssetDatabase.CreateAsset(master, folder + "/Master.asset");
            AssetDatabase.SaveAssets();
            input.actions = controls;
            input.defaultActionMap = "Player";
            input.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            root.SetActive(true);
            deadline = EditorApplication.timeSinceStartup + 45d;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new InvalidOperationException("Runtime test timed out in phase " + phase + " ready=" + tools.IsReady + " switching=" + tools.IsSwitching + " input=" + root.GetComponent<PlayerInput>().inputIsActive + " action=" + root.GetComponent<PlayerInput>().actions.FindAction("Player/UseTool").enabled + " controls=" + root.GetComponent<PlayerInput>().actions.FindAction("Player/UseTool").controls.Count + " keyboard=" + keyboard.added + " toolsEnabled=" + tools.enabled + " master=" + (root.GetComponent<PlayerHost>().MasterPreset != null) + " preset=" + (root.GetComponent<PlayerHost>().MasterPreset != null && root.GetComponent<PlayerHost>().MasterPreset.ToolsPreset != null));
                switch (phase)
                {
                    case 0 when EditorApplication.isPlaying && tools != null && tools.IsReady:
                        Debug.Log("PLAY_INPUT_CHECK " + root.GetComponent<PlayerInput>().actions.FindAction("Player/UseTool").enabled + " " + keyboard.added);
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
                        InputSystem.Update();
                        phase = 1;
                        break;
                    case 1 when tools.ActiveTool == one:
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        InputSystem.Update();
                        Require((first.localPosition - Vector3.up).sqrMagnitude < 0.00001f, "input-driven active slot");
                        tools.Select(two);
                        phase = 2;
                        break;
                    case 2 when tools.ActiveTool == two:
                        cameraAnchor.localPosition = Vector3.one * 8f;
                        root.transform.position = Vector3.one * 4f;
                        tools.enabled = false;
                        Require((first.localPosition - Vector3.left * 3f).sqrMagnitude < 0.00001f, "OnDisable restores target");
                        Require(cameraAnchor.localPosition == Vector3.one * 8f && root.transform.position == Vector3.one * 4f,
                            "OnDisable preserves unrelated transforms");
                        tools.enabled = true;
                        phase = 3;
                        break;
                    case 3 when tools.IsReady && tools.ActiveTool == null:
                        phase = 4;
                        EditorApplication.ExitPlaymode();
                        break;
                    case 4 when !EditorApplication.isPlayingOrWillChangePlaymode:
                        if (++session < 2)
                        {
                            phase = 0;
                            EditorApplication.EnterPlaymode();
                        }
                        else
                            Finish();
                        break;
                }
            }
            catch (Exception exception)
            {
                failure = exception.ToString();
                Debug.LogError(failure);
                EditorApplication.update -= Tick;
                EditorApplication.update += FinishAfterExit;
                EditorApplication.ExitPlaymode();
            }
        }

        private static void FinishAfterExit()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.update -= FinishAfterExit;
                Finish();
            }
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            PlayerQuickPlayInputScope.Release();
            EditorSettings.enterPlayModeOptions = originalOptions;
            EditorSettings.enterPlayModeOptionsEnabled = originalOptionsEnabled;
            if (root != null)
                Object.DestroyImmediate(root);
            AssetDatabase.DeleteAsset(folder);
            if (keyboard != null && keyboard.added)
                InputSystem.RemoveDevice(keyboard);
            File.WriteAllText("Library/CodexStudioCleanupCheck/play-checks.txt", failure ??
                "PASS two Play sessions without domain/scene reload: real UseTool keyboard input, slot transitions, disable/enable, unrelated transforms preserved.");
            Debug.Log(failure == null ? "STUDIO_PLAY_CHECK_OK" : "STUDIO_PLAY_CHECK_FAILED");
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
