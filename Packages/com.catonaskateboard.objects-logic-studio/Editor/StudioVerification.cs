using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Temporary editor and real Play-mode verification; removed after execution.</summary>
    [InitializeOnLoad]
    internal static class StudioVerification
    {
        #region State
        private const string report = "Logs/StudioVerification/Results.txt";
        private static SceneSetup[] scenes;
        private static string workspace;
        private static int checks;
        private static bool failed;
        private static Keyboard keyboard;
        private static HoverObserver observer;
        private static InputActionAsset actions;
        private static InputActionAsset runtimeActions;
        private static PlayerInput playerInput;
        private static GameObject player;
        private static GameObject item;
        private static ObjectGrab grab;
        private static ObjectDrop drop;
        private static ObjectThrow launch;
        private static InputActionReference grabAction;
        private static InputActionReference throwAction;
        private static SimulationMode simulation;
        private static InputSettings.UpdateMode inputMode;
        private static InputSettings.EditorInputBehaviorInPlayMode inputFocus;
        private static InputSettings.BackgroundBehavior background;
        #endregion

        #region Methods
        #region Entry
        static StudioVerification()
        {
            EditorApplication.playModeStateChanged += PlayChanged;
        }

        public static void Run()
        {
            File.WriteAllText(report, "Objects Logic Studio verification\n");
            scenes = EditorSceneManager.GetSceneManagerSetup();
            workspace = EditorJsonUtility.ToJson(ObjectWorkspace.instance);
            try
            {
                EditChecks();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                SessionState.SetBool("StudioVerification.Active", true);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                Fail(exception);
                Finish();
            }
        }

        private static void PlayChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool("StudioVerification.Active", false))
                return;
            switch (change)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    EditorApplication.delayCall += PlayChecks;
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    Finish();
                    break;
            }
        }

        private static void Check(bool condition, string description)
        {
            if (!condition)
                throw new InvalidOperationException(description);
            checks++;
            File.AppendAllText(report, "PASS " + description + "\n");
        }

        private static void Fail(Exception exception)
        {
            failed = true;
            File.AppendAllText(report, "FAIL " + exception + "\n");
            Debug.LogException(exception);
        }

        private static void Finish()
        {
            SessionState.EraseBool("StudioVerification.Active");
            if (scenes != null && scenes.Any(scene => scene.isLoaded && scene.isActive))
                EditorSceneManager.RestoreSceneManagerSetup(scenes);
            if (!string.IsNullOrEmpty(workspace))
            {
                EditorJsonUtility.FromJsonOverwrite(workspace, ObjectWorkspace.instance);
                ObjectWorkspace.instance.Persist();
            }
            File.AppendAllText(report, (failed ? "FAILED" : "ALL CHECKS PASSED") + " · " + checks + " checks\n");
            EditorApplication.Exit(failed ? 1 : 0);
        }
        #endregion

        #region Editor Checks
        private static void EditChecks()
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(root, preview);
            ObjectWorkspace state = ObjectWorkspace.instance;
            SingleInteractionView view = new SingleInteractionView();
            InputActionReference[] references = AssetDatabase.LoadAllAssetsAtPath("Assets/ScriptableObjects/Player Configuration/Controls.inputactions")
                .OfType<InputActionReference>().Where(reference => reference.action.type == InputActionType.Button).ToArray();
            try
            {
                ObjectWorkspaceSession.Discard(state);
                Check(ObjectWorkspaceSession.Select(state, root, 0, out _), "Select an empty object");
                view.Refresh(root);
                view.Add(state, SingleInteractionKind.Drop);
                Check(root.GetComponent<ObjectDrop>() == null, "Drop is unavailable without Grab");
                view.Add(state, SingleInteractionKind.Grab);
                Check(root.GetComponent<ObjectGrab>() != null && root.GetComponent<Rigidbody>() != null, "Grab authors a Rigidbody in Edit mode");
                state.Single.Draft.Action = references[0];
                state.Single.Draft.Grab.Offset = new Vector3(0.4f, -0.2f, 1.8f);
                state.Category = ObjectInteractionCategory.MultipleInteraction;
                state.Single.Expanded = false;
                Check(state.HasChanges && !ObjectWorkspaceSession.Select(state, null, 0, out _), "Tabs and collapsed cards preserve drafts and guard selection");
                Check(ObjectWorkspaceSession.Apply(state, out string warning), "Apply valid Grab: " + warning);
                Check(root.GetComponent<ObjectGrab>().Settings.Offset.z == 1.8f && !state.HasChanges, "Grab Apply writes settings and clears pending state");
                state.Single.Draft.Grab.Distance = -2f;
                Check(!ObjectWorkspaceSession.TryValidate(state, out _) && state.Single.Draft.Grab.Distance == -2f, "Invalid distances warn without snapping values");
                ObjectWorkspaceSession.Discard(state);
                Check(state.Single.Draft.Grab.Distance == 3f, "Discard restores applied Grab values");
                state.Single.Draft.Name = "Proposed";
                using (SerializedObject edited = new SerializedObject(root.GetComponent<ObjectGrab>()))
                {
                    edited.FindProperty("interactionName").stringValue = "External";
                    edited.ApplyModifiedProperties();
                }
                Check(!ObjectWorkspaceSession.TryValidate(state, out _), "Outside edits block stale draft Apply");
                ObjectWorkspaceSession.Discard(state);
                view.Add(state, SingleInteractionKind.Drop);
                state.Single.Draft.Action = references[0];
                state.Single.Draft.Release.Bounciness = 0.65f;
                Check(ObjectWorkspaceSession.Apply(state, out warning), "Apply Drop with same action as Grab: " + warning);
                view.Add(state, SingleInteractionKind.Throw);
                state.Single.Draft.Action = references[0];
                Check(!ObjectWorkspaceSession.TryValidate(state, out _), "Drop and Throw reject an ambiguous shared action");
                state.Single.Draft.Action = references[1];
                state.Single.Draft.Throw.Strength = 12f;
                Check(ObjectWorkspaceSession.Apply(state, out warning), "Apply independently bound Throw: " + warning);
                Check(root.GetComponent<ObjectDrop>().PhysicsSettings.Bounciness == 0.65f
                    && root.GetComponent<ObjectThrow>().PhysicsSettings.Bounciness == 0.2f, "Drop and Throw retain separate physics profiles");
                view.Remove(state, root.GetComponent<ObjectGrab>());
                Check(root.GetComponent<ObjectGrab>() != null, "Removing Grab cannot orphan release features");
                view.Remove(state, root.GetComponent<ObjectThrow>());
                Undo.PerformUndo();
                Check(root.GetComponent<ObjectThrow>() != null && root.GetComponent<ObjectThrow>().Trajectory.Strength == 12f, "Undo restores a removed feature and settings");
                state.Single.Read(root);
                view.Refresh(root);
                state.Single.Kind = SingleInteractionKind.Grab;
                state.Single.Read(root);
                state.Single.Draft.Enabled = false;
                Check(!ObjectWorkspaceSession.TryValidate(state, out _), "Grab cannot be disabled while releases remain enabled");
                ObjectWorkspaceSession.Discard(state);
                Check(new ThrowSettings { Strength = float.NaN }.TryValidate(out _) == false, "NaN launch values are rejected");
                Check(new ReleaseSettings { Bounciness = 1.1f }.TryValidate(out _) == false, "Invalid restitution is rejected");
                Check(new GrabSettings { Instant = true, TransitionDuration = -1f }.TryValidate(out _), "Inactive transition settings do not block instant pickup");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                EditorJsonUtility.FromJsonOverwrite(workspace, state);
                state.Persist();
            }
        }
        #endregion

        #region Play Checks
        private static void PlayChecks()
        {
            simulation = Physics.simulationMode;
            inputMode = InputSystem.settings.updateMode;
            inputFocus = InputSystem.settings.editorInputBehaviorInPlayMode;
            background = InputSystem.settings.backgroundBehavior;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                SetupRuntime();
                Check(playerInput.actions != actions && playerInput.actions.FindAction(grabAction.action.id.ToString()).enabled,
                    "Binding uses the player's enabled private action asset");
                Press(Key.E);
                File.AppendAllText(report, "DIAG held=" + grab.IsHeld + " ready=" + grab.TryValidate(out string detail) + " warning=" + detail + " ctx=" + observer.Player + " rect=" + observer.View.pixelRect + " projected=" + observer.View.WorldToScreenPoint(grab.WorldTarget) + " input=" + playerInput.inputIsActive + " timescale=" + Time.timeScale + " registered=" + typeof(ObjectGrab).Assembly.GetType("CatOnASkateboard.ObjectsLogicStudio.SingleInteractionRegistry").GetProperty("Items", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) + "\n");
                Check(grab.IsHeld && grab.Body.isKinematic && !grab.Body.detectCollisions, "Grab input acquires the object with world collisions disabled");
                Check(Physics.GetIgnoreCollision(item.GetComponent<Collider>(), player.GetComponent<Collider>()), "Carrying ignores the player collision pair");
                Check((bool)typeof(ObjectHover).GetField("carrySuppressed", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(item.GetComponent<ObjectHover>()), "Grab suppresses existing hover labels when configured");
                Check(Vector3.Distance(grab.Body.position, new Vector3(0.35f, -0.2f, 1.5f)) < 0.001f, "Instant pickup uses camera-relative metre offsets");
                Press(Key.E);
                Check(!grab.IsHeld && !grab.Body.isKinematic && grab.Body.detectCollisions && grab.Body.linearVelocity == Vector3.zero,
                    "The same button toggles Drop without re-grabbing in that event");
                Check(grab.Body.mass == 2f && grab.Body.linearDamping == 0.3f && !grab.Body.useGravity, "Drop applies configured mass, air resistance and gravity");
                PhysicsMaterial dropMaterial = item.GetComponent<Collider>().sharedMaterial;
                Check(dropMaterial != null && dropMaterial.bounciness == 0.7f && dropMaterial.dynamicFriction == 0.25f, "Drop applies friction and bounce without modifying an asset");
                Check(!item.GetComponent<ObjectHover>().enabled && !(bool)typeof(ObjectHover).GetField("carrySuppressed", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(item.GetComponent<ObjectHover>()), "Release restores hover suppression without enabling a disabled hover");
                ResetItem();
                Press(Key.E);
                Press(Key.E);
                Check(item.GetComponent<Collider>().sharedMaterial == dropMaterial, "Repeated drops reuse their cached surface material");
                ResetItem();
                Press(Key.E);
                Press(Key.T);
                Physics.Simulate(0.02f);
                Check(!grab.IsHeld && Vector3.Distance(grab.Body.linearVelocity, launch.Trajectory.Direction(Quaternion.identity) * 8f) < 0.05f,
                    "Throw applies the configured speed and elevated trajectory");
                Check(Vector3.Distance(grab.Body.angularVelocity, new Vector3(0f, 2f, 0f)) < 0.05f, "Throw applies configured camera-relative spin");
                launch.Trajectory.Mode = ThrowStrengthMode.Impulse;
                ResetItem();
                Press(Key.E);
                Press(Key.T);
                Physics.Simulate(0.02f);
                Check(Mathf.Abs(grab.Body.linearVelocity.magnitude - 4f) < 0.05f, "Impulse launch respects the released body's mass");
                ResetItem();
                playerInput.DeactivateInput();
                Press(Key.E);
                Check(!grab.IsHeld, "Disabled PlayerInput maps cannot trigger Grab");
                playerInput.ActivateInput();
                observer.SendMessage("LateUpdate");
                Check(!grab.IsHeld, "Re-enabling input does not replay a stale press");
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = new Vector3(0f, 0f, 1f);
                wall.transform.localScale = new Vector3(4f, 4f, 0.2f);
                Physics.SyncTransforms();
                Press(Key.E);
                Check(!grab.IsHeld, "A solid occluder blocks Grab");
                Object.DestroyImmediate(wall);
                grab.Body.position = new Vector3(0f, 0f, 8f);
                Physics.SyncTransforms();
                Press(Key.E);
                Check(!grab.IsHeld, "Objects outside player range cannot be grabbed");
                ResetItem();
                grab.Settings.Instant = false;
                grab.Settings.TransitionDuration = 0.2f;
                Press(Key.E);
                Check(grab.IsHeld && Vector3.Distance(grab.Body.position, new Vector3(0f, 0f, 2f)) < 0.001f, "Animated pickup starts at the original object pose");
                typeof(ObjectGrab).GetField("started", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(grab, Time.time - 0.1f);
                grab.SendMessage("FixedUpdate");
                Physics.Simulate(0.02f);
                Check(grab.Body.position.z < 2f && grab.Body.position.z > 1.5f, "Pickup interpolation visits an intermediate pose");
                observer.enabled = false;
                Check(!grab.IsHeld && !grab.Body.isKinematic && grab.Body.detectCollisions, "Observer disable restores the held body's physics");
                observer.enabled = true;
                observer.SendMessage("LateUpdate");
                grab.Settings.Instant = true;
                ResetItem();
                Press(Key.E);
                grab.Body.position = Vector3.zero;
                Physics.SyncTransforms();
                Press(Key.E);
                grab.SendMessage("FixedUpdate");
                Check(Physics.GetIgnoreCollision(item.GetComponent<Collider>(), player.GetComponent<Collider>()), "Release retains player ignore while colliders overlap");
                grab.Body.position = new Vector3(0f, 0f, 4f);
                Physics.SyncTransforms();
                grab.SendMessage("FixedUpdate");
                Check(!Physics.GetIgnoreCollision(item.GetComponent<Collider>(), player.GetComponent<Collider>()), "Player collisions restore after safe separation");
                ResetItem();
                grab.Settings.WorldCollisions = true;
                grab.Settings.Instant = false;
                grab.Settings.Offset = new Vector3(0f, 0f, 0.3f);
                Press(Key.E);
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = new Vector3(0f, 0f, 1f);
                wall.transform.localScale = new Vector3(4f, 4f, 0.2f);
                Physics.SyncTransforms();
                typeof(ObjectGrab).GetField("started", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(grab, Time.time - 1f);
                for (int step = 0; step < 40; step++)
                {
                    grab.SendMessage("FixedUpdate");
                    Physics.Simulate(0.02f);
                }
                Check(grab.IsHeld && !grab.Body.isKinematic && grab.Body.detectCollisions && grab.Body.position.z > 1.4f,
                    "Physical carrying is stopped by a wall instead of tunnelling to the carry pose");
                Object.DestroyImmediate(wall);
                grab.enabled = false;
                Check(!grab.IsHeld && grab.Body.useGravity == false && item.GetComponent<Collider>().sharedMaterial == null,
                    "Disabling Grab restores carry state and original collider material references");
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
            finally
            {
                if (observer != null)
                    Object.DestroyImmediate(observer.gameObject);
                if (item != null)
                    Object.DestroyImmediate(item);
                if (player != null)
                    Object.DestroyImmediate(player);
                if (keyboard != null && keyboard.added)
                    InputSystem.RemoveDevice(keyboard);
                if (grabAction != null)
                    Object.DestroyImmediate(grabAction);
                if (throwAction != null)
                    Object.DestroyImmediate(throwAction);
                if (actions != null)
                    Object.DestroyImmediate(actions);
                if (runtimeActions != null)
                    Object.DestroyImmediate(runtimeActions);
                Physics.simulationMode = simulation;
                InputSystem.settings.updateMode = inputMode;
                InputSystem.settings.editorInputBehaviorInPlayMode = inputFocus;
                InputSystem.settings.backgroundBehavior = background;
                EditorApplication.ExitPlaymode();
            }
        }

        private static void SetupRuntime()
        {
            keyboard = InputSystem.AddDevice<Keyboard>();
            actions = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = actions.AddActionMap("ObjectTests");
            grabAction = InputActionReference.Create(map.AddAction("Grab", InputActionType.Button, "<Keyboard>/e"));
            throwAction = InputActionReference.Create(map.AddAction("Throw", InputActionType.Button, "<Keyboard>/t"));
            runtimeActions = Object.Instantiate(actions);
            player = new GameObject("Verification Player");
            player.SetActive(false);
            player.tag = "Player";
            player.AddComponent<CapsuleCollider>();
            playerInput = player.AddComponent<PlayerInput>();
            playerInput.actions = runtimeActions;
            playerInput.defaultActionMap = "ObjectTests";
            playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            playerInput.neverAutoSwitchControlSchemes = true;
            Camera camera = new GameObject("Verification Camera").AddComponent<Camera>();
            camera.transform.SetParent(player.transform, false);
            camera.tag = "MainCamera";
            camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
            player.SetActive(true);
            playerInput.user.UnpairDevices();
            InputUser.PerformPairingWithDevice(keyboard, playerInput.user);
            playerInput.actions.devices = new InputDevice[] { keyboard };
            item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = "Verification Item";
            item.SetActive(false);
            item.transform.position = new Vector3(0f, 0f, 2f);
            grab = item.AddComponent<ObjectGrab>();
            grab.Settings.Instant = true;
            grab.Settings.WorldCollisions = false;
            grab.Settings.ShowHover = false;
            SetAction(grab, grabAction);
            drop = item.AddComponent<ObjectDrop>();
            SetAction(drop, grabAction);
            drop.PhysicsSettings.Mass = 2f;
            drop.PhysicsSettings.LinearDamping = 0.3f;
            drop.PhysicsSettings.Gravity = false;
            drop.PhysicsSettings.Bounciness = 0.7f;
            drop.PhysicsSettings.DynamicFriction = 0.25f;
            launch = item.AddComponent<ObjectThrow>();
            SetAction(launch, throwAction);
            launch.PhysicsSettings.Mass = 2f;
            launch.PhysicsSettings.Gravity = false;
            launch.PhysicsSettings.LinearDamping = 0f;
            launch.PhysicsSettings.AngularDamping = 0f;
            launch.Trajectory.Spin = new Vector3(0f, 2f, 0f);
            item.AddComponent<ObjectHover>().enabled = false;
            item.SetActive(true);
            observer = new GameObject("Verification Observer").AddComponent<HoverObserver>();
            using (SerializedObject serialized = new SerializedObject(observer))
            {
                serialized.FindProperty("view").objectReferenceValue = camera;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Physics.SyncTransforms();
            observer.RefreshContext();
            observer.SendMessage("LateUpdate");
        }

        private static void SetAction(ObjectSingleInteraction feature, InputActionReference reference)
        {
            using SerializedObject serialized = new SerializedObject(feature);
            serialized.FindProperty("action").objectReferenceValue = reference;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Press(Key key)
        {
            InputState.Change(keyboard, new KeyboardState(), InputUpdateType.Manual);
            typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(InputUpdateType) }, null).Invoke(null, new object[] { InputUpdateType.Manual });
            InputState.Change(keyboard, new KeyboardState(key), InputUpdateType.Manual);
            typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(InputUpdateType) }, null).Invoke(null, new object[] { InputUpdateType.Manual });
            object driver = typeof(HoverObserver).GetField("singles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(observer);
            System.Collections.IDictionary bound = (System.Collections.IDictionary)driver.GetType().GetField("bindings", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);
            object[] eligibility = { observer, grab, 0f };
            bool eligible = (bool)driver.GetType().GetMethod("Eligible", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(driver, eligibility);
            InputAction resolved = playerInput.actions.FindAction(grabAction.action.id.ToString());
            File.AppendAllText(report, "INPUT key=" + key + " pressed=" + keyboard[key].isPressed + " performed=" + resolved.WasPerformedThisFrame() + " phase=" + resolved.phase + " controls=" + resolved.controls.Count + " device=" + (resolved.controls.Count > 0 ? resolved.controls[0].device.deviceId : -1) + " expected=" + keyboard.deviceId + " bindings=" + bound.Count + " point=" + grab.WorldTarget + " body=" + grab.Body.position + " eligible=" + eligible + "\n");
            foreach (System.Collections.DictionaryEntry entry in bound)
                File.AppendAllText(report, "BOUND " + ((ObjectSingleInteraction)entry.Key).Kind + " pending=" + entry.Value.GetType().GetProperty("Pending", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(entry.Value) + "\n");
            observer.SendMessage("LateUpdate");
            InputState.Change(keyboard, new KeyboardState(), InputUpdateType.Manual);
            typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(InputUpdateType) }, null).Invoke(null, new object[] { InputUpdateType.Manual });
        }

        private static void ResetItem()
        {
            grab.Body.position = new Vector3(0f, 0f, 2f);
            grab.Body.rotation = Quaternion.identity;
            item.transform.SetPositionAndRotation(new Vector3(0f, 0f, 2f), Quaternion.identity);
            if (!grab.Body.isKinematic)
            {
                grab.Body.linearVelocity = Vector3.zero;
                grab.Body.angularVelocity = Vector3.zero;
            }
            Physics.SyncTransforms();
            grab.SendMessage("FixedUpdate");
        }
        #endregion
        #endregion
    }
}





