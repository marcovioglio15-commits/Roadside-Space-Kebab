#if UNITY_EDITOR
using System;
using System.IO;
using CatOnASkateboard.StudioIdentity;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    [InitializeOnLoad]
    public static class FieldVerificationRuntime
    {
        private static Keyboard keyboard;
        private static HoverObserver observer;
        private static SingleInteractionDriver singles;
        private static DialogueDriver dialogues;
        private static Text dialogueText;
        private static InputActionReference grabAction;
        private static InputActionReference sharedAction;
        static FieldVerificationRuntime()
        {
            EditorApplication.update += Tick;
        }
        public static void Begin()
        {
            SessionState.SetBool("FieldVerificationActive", true);
            EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if (!SessionState.GetBool("FieldVerificationActive", false) || !EditorApplication.isPlaying || EditorApplication.isCompiling)
                return;
            SessionState.SetBool("FieldVerificationActive", false);
            try
            {
                Run();
                File.AppendAllText("Library/InteractionFields20261004/result.txt", "ALL CHECKS PASSED\n");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                File.AppendAllText("Library/InteractionFields20261004/result.txt", "FAIL " + exception + "\n");
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
        private static void Run()
        {
            InputSystem.settings = UnityEngine.Object.Instantiate(InputSystem.settings);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputActionAsset actions = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = actions.AddActionMap("Player");
            grabAction = InputActionReference.Create(map.AddAction("Grab", InputActionType.Button, "<Keyboard>/g"));
            sharedAction = InputActionReference.Create(map.AddAction("Shared", InputActionType.Button, "<Keyboard>/f"));
            GameObject player = new GameObject("Player");
            player.SetActive(false);
            ObjectFlag flag = ScriptableObject.CreateInstance<ObjectFlag>();
            ObjectIdentity identity = player.AddComponent<ObjectIdentity>();
            using (SerializedObject data = new SerializedObject(identity))
            {
                data.FindProperty("flags").arraySize = 1;
                data.FindProperty("flags").GetArrayElementAtIndex(0).objectReferenceValue = flag;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            Camera camera = player.AddComponent<Camera>();
            camera.pixelRect = new Rect(0f, 0f, 1280f, 720f);
            PlayerInput input = player.AddComponent<PlayerInput>();
            input.actions = actions;
            input.defaultActionMap = "Player";
            observer = player.AddComponent<HoverObserver>();
            observer.enabled = false;
            Assign(observer, "playerFlag", flag);
            Assign(observer, "view", camera);
            DialogueHud hud = Hud(player.transform);
            Assign(observer, "dialogueHud", hud);
            player.SetActive(true);
            input.ActivateInput();
            input.actions.Enable();
            input.actions.devices = new InputDevice[] { keyboard };
            input.actions.FindAction("Grab").performed += context => File.AppendAllText("Library/InteractionFields20261004/result.txt", "Grab performed\n");
            observer.ResolveContext();
            hud.Bind(observer);
            hud.Prepare(observer);
            Check(observer.Player == player.transform && observer.View == camera, "Observer resolves flagged player and camera");
            Check(hud.IsOwnedBy(observer), "Existing dialogue HUD binds");
            ObjectGrab near = Item("Near unhovered", new Vector3(0.4f, 0f, 1.4f), false);
            ObjectGrab far = Item("Far hovered", new Vector3(0f, 0f, 2.4f), true);
            singles = new SingleInteractionDriver();
            dialogues = new DialogueDriver();
            Pass();
            Check(far.GetComponent<ObjectHover>().IsHovered, "Far object is actually hovered");
            Check(far.Available(InteractionChannels.Grab), "Far pickup is available");
            Check(new InteractionTargeting().Eligible(observer, far.transform, far.Colliders, far.WorldTarget, 5f, HoverTargetMode.ViewCenter, 1f, 0, false, out float score), "Far pickup target is eligible");
            Press(Key.G);
            File.AppendAllText("Library/InteractionFields20261004/result.txt", "Pickup: " + (singles.Held != null ? singles.Held.name : "none") + "; grab enabled=" + input.actions.FindAction("Grab").enabled + "\n");
            Check(singles.Held == far && !near.IsHeld, "Hovered valid pickup wins over nearer nonhovered item");
            ObjectDialogue dialogue = Dialogue();
            Pass();
            Press(Key.F);
            Check(singles.Held == null && !dialogue.IsSpeaking, "Shared Throw consumes press before dialogue starts");
            near.Settings.Distance = far.Settings.Distance = 0.01f;
            Press(Key.F);
            Check(dialogue.IsSpeaking && dialogueText.text == "First", "Dialogue starts when no eligible Single can consume action");
            far.transform.position = new Vector3(0f, 0f, 2.4f);
            far.Settings.Distance = 5f;
            Press(Key.G);
            Check(singles.Held == far && dialogue.IsSpeaking, "Unrelated Grab key remains available during dialogue");
            Press(Key.F);
            Check(singles.Held == null && dialogueText.text == "First", "Shared Throw does not advance active dialogue");
            Press(Key.F);
            Check(dialogueText.text == "Second", "Next independent press advances dialogue normally");
            far.transform.position = new Vector3(0f, 0f, 2.4f);
            Press(Key.G);
            Check(singles.Held == far, "Pickup remains available after dialogue advancement");
            dialogue.Settings.PreferSingleActions = false;
            Press(Key.F);
            Check(singles.Held == far && !dialogue.IsSpeaking, "Toggle off restores dialogue-first completion without throwing");
            singles.Reset();
            dialogues.Reset();
            UnityEngine.Object.DestroyImmediate(dialogue.gameObject);
            far.GetComponent<ObjectHover>().enabled = false;
            near.Settings.Distance = 5f;
            near.transform.position = new Vector3(0.4f, 0f, 1.4f);
            far.transform.position = new Vector3(0f, 0f, 2.4f);
            Pass();
            Press(Key.G);
            Check(singles.Held == near, "Nearest pickup still wins when neither item is hovered");
            singles.Reset();
            far.GetComponent<ObjectHover>().enabled = true;
            far.Settings.Distance = 0.01f;
            Pass();
            Press(Key.G);
            Check(singles.Held == near, "Hovered item outside Grab reach cannot steal priority");
            singles.Reset();
            dialogues.Reset();
            ColliderMode(near, far);
            InputSystem.RemoveDevice(keyboard);
        }
        private static void ColliderMode(ObjectGrab near, ObjectGrab far)
        {
            near.gameObject.SetActive(false);
            far.transform.position = new Vector3(12f, 0f, 2.4f);
            far.transform.localScale = Vector3.one;
            far.GetComponent<Collider>().enabled = false;
            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = "Child collider";
            surface.transform.SetParent(far.transform);
            surface.transform.position = new Vector3(0f, 0f, 2f);
            surface.transform.localScale = Vector3.one * 0.3f;
            ObjectHover hover = far.GetComponent<ObjectHover>();
            using (SerializedObject data = new SerializedObject(hover))
            {
                data.FindProperty("configuration.settings.targetMode").enumValueIndex = (int)HoverDetectionMode.CenterCollider;
                data.FindProperty("configuration.settings.releaseDelay").floatValue = 0f;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            hover.Refresh();
            Cursor.lockState = CursorLockMode.Locked;
            Pass();
            Check(hover.IsHovered, "Centre collider works with locked cursor, offscreen pivot and child surface in reach");
            surface.transform.position = new Vector3(1f, 0f, 2f);
            Pass();
            Check(!hover.IsHovered, "Centre collider requires exact reticle hit without anchor-radius fallback");
            surface.transform.position = new Vector3(0f, 0f, 2f);
            surface.GetComponent<Collider>().isTrigger = true;
            Pass();
            Check(hover.IsHovered, "Centre collider includes target trigger colliders");
            ObjectGrab behind = Item("Behind trigger", new Vector3(0f, 0f, 3f), true);
            ObjectHover other = behind.GetComponent<ObjectHover>();
            using (SerializedObject data = new SerializedObject(other))
            {
                data.FindProperty("configuration.settings.targetMode").enumValueIndex = (int)HoverDetectionMode.CenterCollider;
                data.FindProperty("configuration.settings.releaseDelay").floatValue = 0f;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            other.Refresh();
            Pass();
            Check(hover.IsHovered && !other.IsHovered, "Only frontmost centre collider hover activates among overlapping triggers");
            surface.SetActive(false);
            Pass();
            Check(!hover.IsHovered && other.IsHovered, "Inactive front collider yields to next visible centre hit");
            surface.SetActive(true);
            surface.layer = 7;
            observer.View.cullingMask &= ~(1 << 7);
            Pass();
            Check(!hover.IsHovered, "Child collider camera layer is respected independently of root layer");
            surface.layer = 0;
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = 8;
            wall.transform.position = new Vector3(0f, 0f, 1f);
            wall.transform.localScale = new Vector3(2f, 2f, 0.1f);
            Pass();
            Check(!hover.IsHovered && !other.IsHovered, "Solid obstacle blocks collider hover");
            wall.SetActive(false);
            surface.transform.position = Vector3.forward * 9f;
            behind.gameObject.SetActive(false);
            Pass();
            Check(!hover.IsHovered, "Hit surface outside player reach cannot activate hover");
            Cursor.lockState = CursorLockMode.None;
        }

        private static void Pass()
        {
            Physics.SyncTransforms();
            HoverRegistry.Tick(observer);
            singles.Prepare(observer);
            singles.Tick(observer, dialogues.Tick(observer, singles));
        }
        private static void Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            Pass();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }
        private static ObjectGrab Item(string name, Vector3 position, bool hovered)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.SetActive(false);
            item.transform.position = position;
            item.transform.localScale = Vector3.one * 0.2f;
            Rigidbody body = item.AddComponent<Rigidbody>();
            body.useGravity = false;
            ObjectGrab grab = item.AddComponent<ObjectGrab>();
            Assign(grab, "action", grabAction);
            grab.Settings.PickupSound = false;
            grab.Settings.CollisionAudio.Enabled = false;
            grab.Settings.ObstacleMask = 0;
            grab.Settings.CenterRadius = 1f;
            grab.Settings.Distance = 5f;
            grab.Settings.WorldCollisions = false;
            grab.Settings.Instant = true;
            ObjectThrow throwing = item.AddComponent<ObjectThrow>();
            Assign(throwing, "action", sharedAction);
            throwing.Trajectory.Sound = false;
            if (hovered)
            {
                ObjectHover hover = item.AddComponent<ObjectHover>();
                GameObject canvasObject = new GameObject("Hover", typeof(RectTransform), typeof(Canvas), typeof(HoverLabel));
                canvasObject.transform.SetParent(item.transform);
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                GameObject panelObject = new GameObject("Panel", typeof(RectTransform));
                panelObject.transform.SetParent(canvasObject.transform);
                RectTransform panel = panelObject.GetComponent<RectTransform>();
                panel.sizeDelta = new Vector2(280f, 64f);
                GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
                textObject.transform.SetParent(panelObject.transform);
                Text text = textObject.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.raycastTarget = false;
                HoverLabel label = canvasObject.GetComponent<HoverLabel>();
                Assign(label, "canvas", canvas);
                Assign(label, "panel", panel);
                Assign(label, "text", text);
                Assign(hover, "label", label);
                using SerializedObject data = new SerializedObject(hover);
                data.FindProperty("configuration.settings.obstacleMask").intValue = 1 << 8;
                data.FindProperty("configuration.settings.queryInterval").floatValue = 0f;
                data.FindProperty("configuration.settings.playerDistance").floatValue = 5f;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            item.SetActive(true);
            return grab;
        }
        private static DialogueHud Hud(Transform parent)
        {
            GameObject canvasObject = new GameObject("Dialogue HUD", typeof(RectTransform), typeof(Canvas), typeof(DialogueHud));
            canvasObject.transform.SetParent(parent);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(canvasObject.transform);
            dialogueText = textObject.GetComponent<Text>();
            dialogueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            DialogueHud hud = canvasObject.GetComponent<DialogueHud>();
            Assign(hud, "canvas", canvas);
            Assign(hud, "body", dialogueText);
            return hud;
        }
        private static ObjectDialogue Dialogue()
        {
            GameObject item = new GameObject("Dialogue");
            item.SetActive(false);
            item.transform.position = Vector3.forward * 2f;
            ObjectDialogue dialogue = item.AddComponent<ObjectDialogue>();
            Assign(dialogue, "startAction", sharedAction);
            Assign(dialogue, "advanceAction", sharedAction);
            dialogue.Settings.Trigger = DialogueTrigger.InputAction;
            DialogueEntry entry = ScriptableObject.CreateInstance<DialogueEntry>();
            entry.Lines = new[] { new DialogueLine { Text = "First" }, new DialogueLine { Text = "Second" } };
            dialogue.Settings.Entries = new[] { entry };
            item.SetActive(true);
            Check(dialogue.Ready, "Dialogue is valid and ready");
            return dialogue;
        }
        private static void Assign(UnityEngine.Object owner, string path, UnityEngine.Object value)
        {
            using SerializedObject data = new SerializedObject(owner);
            data.FindProperty(path).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Check(bool value, string message)
        {
            if (!value)
                throw new InvalidOperationException(message);
            File.AppendAllText("Library/InteractionFields20261004/result.txt", "PASS " + message + "\n");
        }
    }
}
#endif
