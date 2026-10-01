using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CatOnASkateboard.AudioStudio;
using CatOnASkateboard.ObjectsLogicStudio;
using CatOnASkateboard.PlayerStudio;
using RoadsideSpaceKebab.Audio;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

/// <summary>Temporary integration checks for flow, audio and shared order ownership.</summary>
public static class GameplayIntegrationChecks
{
    #region State
    private static IEnumerator run;
    private static readonly List<string> results = new List<string>();
    private static double deadline;
    private static bool failed;
    private const string directory = "Library/StudioGameplayAudio20261001/";
    #endregion
    #region Methods
    /// <summary>Opens the configured gameplay scene and starts the checks in Play.</summary>
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Tool Testing Scenes/Test/Programming/Main/SCN_Blockout_LogicTest.unity");
        CheckApplyPreparation();
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDevicesRespectGameViewFocus;
        EditorApplication.playModeStateChanged += State;
        EditorApplication.EnterPlaymode();
    }

    /// <summary>Begins the test routine after runtime startup and exits after the scene returns to Edit.</summary>
    /// <param name="state">Unity Play transition.</param>
    private static void State(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            deadline = EditorApplication.timeSinceStartup + 45d;
            run = Checks();
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            File.WriteAllLines(directory + "checks.txt", results);
            EditorApplication.Exit(failed ? 1 : 0);
        }
    }

    /// <summary>Advances checks without blocking physics, audio mixing or the dialogue observer.</summary>
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("Integration checks timed out.");
            if (run.MoveNext())
                return;
        }
        catch (Exception exception)
        {
            failed = true;
            results.Add("FAIL " + exception);
            Debug.LogError(exception);
        }
        EditorApplication.update -= Tick;
        EditorApplication.ExitPlaymode();
    }

    /// <summary>Checks observable scene behavior and package boundaries.</summary>
    /// <returns>Frame-by-frame test progression.</returns>
    private static IEnumerator Checks()
    {
        PlayerHost player = Object.FindAnyObjectByType<PlayerHost>();
        player.transform.position = new Vector3(40f, 1f, 40f);
        player.GetComponent<PlayerInput>().ActivateInput();
        double wait = EditorApplication.timeSinceStartup + 3d;
        while (EditorApplication.timeSinceStartup < wait)
            yield return null;
        ObjectSpawnManager manager = Object.FindObjectsByType<ObjectSpawnManager>().First(item => item.Settings.Mode == SpawnManagementMode.DayFlow);
        ObjectDialogue arrival = Object.FindObjectsByType<ObjectDialogue>().First(item => item.name.Contains("PF_Customer(Clone)") && item.InteractionName == "Dialogue Test");
        Check(Vector3.Distance(arrival.transform.position, new Vector3(3f, 0.99f, -5.073f)) < 0.01f, "Walk-in uses authored world keyframe position independent of spawn rotation");
        Check(Quaternion.Angle(arrival.transform.rotation, Quaternion.identity) < 0.1f, "Walk-in reaches the authored final rotation");
        Check(arrival.IsSpeaking, "Arrival dialogue opens automatically while the player is outside its ordinary range");
        OrderBoard board = Object.FindAnyObjectByType<OrderBoard>();
        TMP_Text[] texts = board.GetComponentsInChildren<TMP_Text>();
        Check(texts.Count(text => !string.IsNullOrEmpty(text.text)) == 1, "Spawned customer publishes one order on the shared board");
        int countBefore = manager.CompletedSpawns;
        Call(arrival, "Advance");
        Check(arrival.IsSpeaking && manager.CompletedSpawns == countBefore, "Intermediate arrival page does not complete the spawn");
        Call(arrival, "Advance");
        Check(!arrival.IsSpeaking && manager.CompletedSpawns == countBefore, "Separate arrival dialogue completion does not trigger departure");
        manager.enabled = false;
        yield return null;
        Check(texts.All(text => string.IsNullOrEmpty(text.text)), "Stopping Day Flow releases the departed customer's order slot");

        CheckNativeAudio();
        CheckWorldMotion();
        CheckRuleCounters();
        CheckOrders(board, texts);
        CheckEditorDraft();
        CheckAudioHooks(player);
        results.Add("PASS All gameplay/audio/order checks completed.");
    }

    /// <summary>Exercises actual FMOD bank keys, labels and radio configuration.</summary>
    private static void CheckNativeAudio()
    {
        Check(StudioAudio.Backend is GameplayAudio, "Scene installs the typed FMOD backend");
        AudioPreset preset = AssetDatabase.LoadAssetAtPath<AudioPreset>("Assets/ScriptableObjects/Presets_Modules/Audio/AudioPreset.asset");
        Check(preset.Events.Count >= 15, "All requested audio events are linked in Audio Studio");
        foreach (AudioBinding binding in preset.Events)
        {
            FMOD.RESULT result = FMODUnity.RuntimeManager.StudioSystem.getEvent(binding.EventPath, out FMOD.Studio.EventDescription description);
            Check(result == FMOD.RESULT.OK, "Built event resolves: " + binding.Key);
            if (binding.Key is "sfx_footstep" or "sfx_ingredientslice")
            {
                string parameter = binding.Key == "sfx_footstep" ? "Surface" : "Ingredient";
                string[] expected = parameter == "Surface" ? new[] { "Concrete", "ConcreteWet", "Dirt", "Metal" } : new[] { "Bread", "Crunchy", "Full" };
                description.createInstance(out FMOD.Studio.EventInstance voice);
                foreach (string label in expected)
                    Check(voice.setParameterByNameWithLabel(parameter, label) == FMOD.RESULT.OK, parameter + " label resolves: " + label);
                voice.release();
            }
        }
        Check(preset.Events.First(binding => binding.Key == "mus_radio").Parameters.Any(parameter => parameter.Name == "Channels" && parameter.Value == 1f), "Radio starts on the configured audible channel");
    }

    /// <summary>Verifies exact pose sampling and path-space independence without relying on scene placement.</summary>
    private static void CheckWorldMotion()
    {
        GameObject sample = new GameObject("Motion Check");
        sample.transform.SetPositionAndRotation(new Vector3(-8f, 2f, 4f), Quaternion.Euler(0f, 90f, 0f));
        Type type = typeof(SpawnFlowPlan).Assembly.GetType("CatOnASkateboard.ObjectsLogicStudio.SpawnFlowMotion", true);
        object motion = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { sample.transform }, null);
        SpawnFlowAnimation path = new SpawnFlowAnimation
        {
            Enabled = true, Space = SpawnFlowSpace.World,
            Keyframes = new[] { new SpawnFlowKeyframe { Pose = new PlayerToolPose { Position = new Vector3(3f, 1f, -5f), Rotation = new Vector3(0f, 180f, 0f), Scale = Vector3.one }, Duration = 1f } }
        };
        Call(motion, "Begin", path);
        Call(motion, "Tick", 0.5f);
        Check(Vector3.Distance(sample.transform.position, new Vector3(-2.5f, 1.5f, -0.5f)) < 0.001f, "World keyframe interpolates without rotating its translation");
        Call(motion, "Tick", 0.5f);
        Check(Quaternion.Angle(sample.transform.rotation, Quaternion.Euler(0f, 180f, 0f)) < 0.01f, "Rotation is an exact final orientation");
        Object.DestroyImmediate(sample);
    }

    /// <summary>Checks count thresholds, snapshot retention, AND, OR and repeated cycles.</summary>
    private static void CheckRuleCounters()
    {
        GameObject root = new GameObject("Rule Check");
        root.SetActive(false);
        ObjectAmbient target = root.AddComponent<ObjectAmbient>();
        ObjectContactModifier first = root.AddComponent<ObjectContactModifier>();
        ObjectContactModifier second = root.AddComponent<ObjectContactModifier>();
        first.enabled = second.enabled = false;
        ObjectInteractionUnlock rule = root.AddComponent<ObjectInteractionUnlock>();
        rule.Settings.Target = target;
        rule.Settings.Conditions = new[]
        {
            new InteractionUnlockCondition { Source = first, Count = 2 },
            new InteractionUnlockCondition { Source = second, Count = 3 }
        };
        root.SetActive(true);
        Signal(first);
        Check(!rule.IsApplied && target.IsLocked, "First occurrence does not satisfy a count of two");
        int[] progress = (int[])Call(rule, "CaptureStoredProgress");
        Call(rule, "Initialize");
        Call(rule, "RestoreStoredProgress", progress, false, false);
        Signal(first);
        Signal(second);
        Signal(second);
        Check(!rule.IsApplied, "AND waits for every condition's count");
        Signal(second);
        Check(rule.IsApplied && !target.IsLocked, "AND applies after 2 and 3 matching completions, including restored progress");
        rule.Settings.RequireAll = false;
        rule.Settings.Repeat = true;
        Call(rule, "Initialize");
        Signal(first);
        Signal(first);
        Check(rule.IsApplied && !target.IsLocked, "OR applies when one counted condition becomes true");
        int[] cleared = (int[])Call(rule, "CaptureStoredProgress");
        Check(cleared.All(value => value == 0), "Repeat resets all condition counters for the next cycle");
        Object.DestroyImmediate(root);
    }

    /// <summary>Checks slot queuing, exact source completion, strikethrough and ownership during walk paths.</summary>
    /// <param name="board">Authored scene board.</param>
    /// <param name="texts">Actual pre-authored 3D text slots.</param>
    private static void CheckOrders(OrderBoard board, TMP_Text[] texts)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Interactable Items/Test/PF_Customer.prefab");
        List<GameObject> customers = new List<GameObject>();
        GameObject staging = new GameObject("Check Staging");
        staging.SetActive(false);
        for (int index = 0; index < board.Capacity + 2; index++)
        {
            GameObject customer = Object.Instantiate(prefab, staging.transform);
            customer.SetActive(false);
            customer.transform.SetParent(null);
            foreach (ObjectDialogue dialogue in customer.GetComponents<ObjectDialogue>())
                dialogue.enabled = false;
            customer.GetComponent<ObjectMakeOrder>().Settings.Entries[0].Text = "Check order " + index;
            customer.SetActive(true);
            customers.Add(customer);
        }
        Check(texts.Count(text => !string.IsNullOrEmpty(text.text)) == board.Capacity, "Order board capacity is respected; excess requests wait");
        ObjectMakeOrder first = customers[0].GetComponent<ObjectMakeOrder>();
        Signal(customers[0].GetComponent<ObjectDialogue>());
        Check(texts.All(text => (text.fontStyle & FontStyles.Strikethrough) == 0), "An unrelated completion does not fulfil an order");
        Signal(first.Settings.Entries[0].Source);
        Check(texts.Single(text => text.text == "Check order 0").fontStyle.HasFlag(FontStyles.Strikethrough), "Consumed order is visibly crossed out");
        Check(texts.All(text => text.text != "Check order 4"), "Crossing out retains the occupied slot");
        Signal(customers[4].GetComponent<ObjectMakeOrder>().Settings.Entries[0].Source);
        Object.DestroyImmediate(customers[0]);
        Check(texts.Single(text => text.text == "Check order 4").fontStyle.HasFlag(FontStyles.Strikethrough), "Queued order retains completion and fills the next free slot");
        Type state = typeof(ObjectContainer).Assembly.GetType("CatOnASkateboard.ObjectsLogicStudio.SuspendedItemState", true);
        object suspension = Activator.CreateInstance(state, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { customers[1], true }, null);
        Call(suspension, "Suspend");
        Call(suspension, "Reveal");
        Check(texts.Any(text => text.text == "Check order 1"), "Walk-path suspension retains customer slot ownership");
        customers[1].SetActive(false);
        Check(texts.Any(text => text.text == "Check order 5"), "External despawn during a walk path frees its slot immediately");
        foreach (GameObject customer in customers)
            if (customer != null)
                Object.DestroyImmediate(customer);
        Object.DestroyImmediate(staging);
        Check(texts.All(text => string.IsNullOrEmpty(text.text)), "All customer slots are released after destruction");
    }

    /// <summary>Tests pending serialized audio writes without applying to the user's asset.</summary>
    private static void CheckApplyPreparation()
    {
        Type type = Type.GetType("CatOnASkateboard.PlayerStudio.Editor.PlayerLocomotionEditSession, CatOnASkateboard.PlayerStudio.Editor", true);
        object session = Activator.CreateInstance(type);
        PlayerMasterPreset master = AssetDatabase.LoadAssetAtPath<PlayerMasterPreset>("Assets/ScriptableObjects/Player Configuration/Master.asset");
        Call(session, "Refresh", master);
        PlayerAudioSettings audio = (PlayerAudioSettings)type.GetField("audio", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
        audio.Interval += 0.25f;
        object[] arguments = { master, null, null };
        bool valid = (bool)type.GetMethod("TryPrepareApply").Invoke(session, arguments);
        Check(valid, "Player audio Apply can prepare serialized changes: " + arguments[2]);
        using SerializedObject changes = (SerializedObject)arguments[1];
        Check(Mathf.Abs(changes.FindProperty("Audio.Interval").floatValue - audio.Interval) < 0.0001f, "Player audio draft reaches its pending serialized preset without altering the source");
    }

    /// <summary>Checks that the player audio draft remains detached and survives its Apply preparation.</summary>
    private static void CheckEditorDraft()
    {
        Type sessionType = Type.GetType("CatOnASkateboard.PlayerStudio.Editor.PlayerLocomotionEditSession, CatOnASkateboard.PlayerStudio.Editor", true);
        object session = Activator.CreateInstance(sessionType);
        PlayerMasterPreset master = AssetDatabase.LoadAssetAtPath<PlayerMasterPreset>("Assets/ScriptableObjects/Player Configuration/Master.asset");
        Call(session, "Refresh", master);
        PlayerAudioSettings draft = (PlayerAudioSettings)sessionType.GetField("audio", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
        float original = master.LocomotionPreset.Audio.Interval;
        draft.Interval += 0.2f;
        Check((bool)sessionType.GetProperty("HasChanges").GetValue(session) && master.LocomotionPreset.Audio.Interval == original, "Player audio edits remain in the pending locomotion draft");
        Call(session, "Discard");
        Check(!(bool)sessionType.GetProperty("HasChanges").GetValue(session), "Discard restores the original player audio values");
    }

    /// <summary>Checks sound emission timing using a recording backend at actual interaction boundaries.</summary>
    /// <param name="player">Current player and observer context.</param>
    private static void CheckAudioHooks(PlayerHost player)
    {
        IStudioAudioBackend original = StudioAudio.Backend;
        RecordingAudio recording = new RecordingAudio();
        StudioAudio.Backend = recording;
        try
        {
            Type type = typeof(PlayerHost).Assembly.GetType("CatOnASkateboard.PlayerStudio.PlayerAudioMotion", true);
            object motion = Activator.CreateInstance(type);
            PlayerAudioSettings settings = new PlayerAudioSettings { DefaultSurface = FootstepSurface.Metal };
            Call(motion, "Bind", settings, player.transform);
            Call(motion, "Tick", new Vector3(2f, 0f, 0f), true, false);
            Check(recording.Calls.Any(call => call == "sfx_footstep/Surface/Metal"), "Fallback step interval emits the selected FMOD Surface label");
            int before = recording.Calls.Count;
            Call(motion, "Tick", Vector3.zero, true, false);
            Call(motion, "Step");
            Check(recording.Calls.Count == before, "Stationary player emits no head-tilt or interval footstep");
            Call(motion, "Tick", new Vector3(2f, 0f, 0f), true, true);
            Check(recording.Calls.Count == before, "Active head tilt suppresses fallback interval emission");
            Call(motion, "Step");
            Check(recording.Calls.Count == before + 1, "Head-tilt phase emits one synchronized footstep");
            Type audioType = typeof(ObjectDialogue).Assembly.GetType("CatOnASkateboard.ObjectsLogicStudio.DialogueAudioRun", true);
            object dialogue = Activator.CreateInstance(audioType);
            DialogueAudioSettings voices = new DialogueAudioSettings { Voice = DialogueVoice.Fishy, Completion = DialogueResultSound.OrderOK, Interval = 0.01f };
            Call(dialogue, "Begin", voices, player.transform, true);
            Check(recording.Calls.Last() == "sfx_newcustomer//", "Arrival cue takes precedence over dialogue voice");
            recording.Last.Stop();
            audioType.GetField("nextVoice", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(dialogue, 0f);
            Call(dialogue, "Tick", voices, player.transform);
            Check(recording.Calls.Last() == "vo_fishy//", "Selected dialogue voice starts after the arrival cue ends");
            Call(dialogue, "Finish", voices, player.transform);
            Check(recording.Calls.Last() == "sfx_orderOK//", "Completed dialogue emits its selected result cue");
        }
        finally
        {
            StudioAudio.Backend = original;
        }
    }

    /// <summary>Publishes the exact committed interaction boundary under test.</summary>
    /// <param name="source">Selected component identity.</param>
    private static void Signal(ObjectInteraction source) => Call(source, "Signal", InteractionMoment.Completed);

    /// <summary>Calls internal test targets through editor-only reflection, without changing production visibility.</summary>
    /// <param name="target">Runtime or editor object under test.</param>
    /// <param name="name">Known method name.</param>
    /// <param name="arguments">Typed arguments.</param>
    /// <returns>The method result.</returns>
    private static object Call(object target, string name, params object[] arguments)
    {
        Type type = target.GetType();
        while (type != null)
        {
            MethodInfo method = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .FirstOrDefault(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length);
            if (method != null)
                return method.Invoke(target, arguments);
            type = type.BaseType;
        }
        throw new MissingMethodException(target.GetType().Name, name);
    }

    /// <summary>Records an explicit assertion and aborts on failure.</summary>
    /// <param name="condition">Observed result.</param>
    /// <param name="label">Behavior checked.</param>
    private static void Check(bool condition, string label)
    {
        if (!condition)
            throw new InvalidOperationException(label);
        results.Add("PASS " + label);
        Debug.Log("GAMEPLAY CHECK: " + label);
    }
    #endregion

    /// <summary>Records gameplay requests without creating native voices during timing assertions.</summary>
    private sealed class RecordingAudio : IStudioAudioBackend
    {
        internal readonly List<string> Calls = new List<string>();
        internal RecordingVoice Last;
        public IAudioVoice Play(string key, Transform source, Vector3 position, string parameter, string label, float radius)
        {
            Calls.Add(key + "/" + parameter + "/" + label);
            Last = new RecordingVoice();
            return Last;
        }
    }
    /// <summary>Provides explicit sound completion for deterministic dialogue precedence checks.</summary>
    private sealed class RecordingVoice : IAudioVoice
    {
        public bool IsPlaying { get; private set; } = true;
        public void Stop() => IsPlaying = false;
    }
}
