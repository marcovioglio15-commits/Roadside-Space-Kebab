using System;
using System.Collections.Generic;
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
using UnityEngine.SceneManagement;

/// <summary>Temporary authored-asset setup, removed once scene configuration has been verified.</summary>
public static class GameplayAudioSetup
{
    #region Methods
    /// <summary>Links existing FMOD events and authors scene-owned audio and order presentation.</summary>
    public static void Run()
    {
        // Preserve sound project names while giving gameplay a correctly spelled stable arrival key.
        AudioPreset audio = AssetDatabase.LoadAssetAtPath<AudioPreset>("Assets/ScriptableObjects/Presets_Modules/Audio/AudioPreset.asset");
        string[] keys = { "sfx_footstep", "sfx_throw", "sfx_pickup", "sfx_playercollision", "sfx_ingredientslice", "sfx_orderOK", "sfx_orderNO",
            "vo_hellfury", "vo_mothman", "vo_fishy", "sfx_ingredientcollision", "amb_neonsign", "amb_traffic", "sfx_newcustomer", "mus_radio" };
        foreach (string key in keys)
        {
            string name = key == "sfx_newcustomer" ? "sfx_newcostumer" : key;
            FMODUnity.EditorEventRef entry = FMODUnity.EventManager.Events.FirstOrDefault(candidate => candidate.Path.EndsWith("/" + name, StringComparison.Ordinal));
            if (entry == null)
                throw new InvalidOperationException("Missing built event: " + name);
            AudioBinding binding = audio.Events.FirstOrDefault(candidate => candidate.Key == key);
            if (binding == null)
            {
                binding = new AudioBinding { Key = key };
                audio.Events.Add(binding);
            }
            binding.DisplayName = key;
            binding.EventPath = entry.Path;
            binding.EventGuid = entry.Guid.ToString();
            binding.Spatialize = key != "mus_radio" && entry.Is3D;
            binding.SingleInstance = key == "mus_radio";
            if (key == "mus_radio" && !binding.Parameters.Exists(parameter => parameter.Name == "Channels"))
                binding.Parameters.Add(new AudioParameter { Name = "Channels", Value = 1f });
        }
        if (!audio.Music.Exists(context => context.Context == "Gameplay"))
        {
            AudioBinding radio = audio.Events.Find(binding => binding.Key == "mus_radio");
            audio.Music.Add(new AudioMusic { Context = "Gameplay", EventPath = radio.EventPath, EventGuid = radio.EventGuid, Bank = "MUS" });
        }
        EditorUtility.SetDirty(audio);
        AssetDatabase.SaveAssetIfDirty(audio);

        // Surface layers are explicit mappings, leaving all existing collision policies intact.
        UnityEngine.Object tags = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        using (SerializedObject data = new SerializedObject(tags))
        {
            SerializedProperty layers = data.FindProperty("layers");
            PlayerLocomotionPreset locomotion = AssetDatabase.LoadAssetAtPath<PlayerLocomotionPreset>("Assets/ScriptableObjects/Player Configuration/Locomotion.asset");
            List<FootstepLayers> surfaces = new List<FootstepLayers>();
            foreach (FootstepSurface surface in Enum.GetValues(typeof(FootstepSurface)))
            {
                string layer = "Surface " + surface;
                int found = -1;
                for (int index = 8; index < layers.arraySize; index++)
                    if (layers.GetArrayElementAtIndex(index).stringValue == layer)
                        found = index;
                if (found < 0)
                    for (int index = 8; index < layers.arraySize; index++)
                        if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(index).stringValue))
                        {
                            found = index;
                            layers.GetArrayElementAtIndex(index).stringValue = layer;
                            break;
                        }
                if (found < 0)
                    throw new InvalidOperationException("No layer available for " + layer);
                surfaces.Add(new FootstepLayers { Layers = 1 << found, Surface = surface });
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            locomotion.Audio.Surfaces = surfaces.ToArray();
            EditorUtility.SetDirty(locomotion);
            AssetDatabase.SaveAssetIfDirty(locomotion);
        }

        // The customer's existing greeting is the optional automatic arrival dialogue.
        const string customerPath = "Assets/Prefabs/Interactable Items/Test/PF_Customer.prefab";
        GameObject contents = PrefabUtility.LoadPrefabContents(customerPath);
        foreach (ObjectDialogue dialogue in contents.GetComponents<ObjectDialogue>())
        {
            if (dialogue.InteractionName == "Dialogue Test")
                dialogue.Settings.Trigger = DialogueTrigger.SpawnArrival;
            if (dialogue.InteractionName.EndsWith("Right", StringComparison.Ordinal))
                dialogue.Settings.Audio.Completion = DialogueResultSound.OrderOK;
            else if (dialogue.InteractionName.EndsWith("Wrong", StringComparison.Ordinal))
                dialogue.Settings.Audio.Completion = DialogueResultSound.OrderNO;
        }
        ObjectContactModifier consume = contents.GetComponents<ObjectContactModifier>().FirstOrDefault(candidate => OrderSettings.Eligible(candidate, contents));
        if (consume != null && contents.GetComponent<ObjectMakeOrder>() == null)
        {
            ObjectMakeOrder orders = contents.AddComponent<ObjectMakeOrder>();
            orders.Settings.Entries = new[] { new OrderEntry { Source = consume, Text = "Kebab" } };
            using SerializedObject data = new SerializedObject(orders);
            data.FindProperty("interactionName").stringValue = "Make an Order";
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        PrefabUtility.SaveAsPrefabAsset(contents, customerPath);
        PrefabUtility.UnloadPrefabContents(contents);
        GameObject customer = AssetDatabase.LoadAssetAtPath<GameObject>(customerPath);
        ObjectDialogue greeting = customer.GetComponents<ObjectDialogue>().First(dialogue => dialogue.InteractionName == "Dialogue Test");
        SpawnFlowPlan plan = AssetDatabase.LoadAssetAtPath<SpawnFlowPlan>("Assets/ScriptableObjects/Objects Logic Studio/Day Flow/DayFlowPlan.asset");
        foreach (SpawnFlowDay day in plan.Days)
            foreach (SpawnFlowStep step in day.Steps)
            {
                step.WalkIn.Space = step.WalkOut.Space = SpawnFlowSpace.World;
                if (step.Prefab == customer)
                {
                    step.ArrivalDialogueEnabled = true;
                    step.ArrivalDialogue = greeting;
                    step.NewCustomerSound = true;
                }
            }
        EditorUtility.SetDirty(plan);
        AssetDatabase.SaveAssetIfDirty(plan);

        // Import Unity's installed TMP resources once to support actual world-space text and strikethrough.
        if (Resources.Load<TMP_Settings>("TMP Settings") == null)
        {
            AssetDatabase.importPackageCompleted += Imported;
            AssetDatabase.ImportPackage("Library/PackageCache/com.unity.ugui@23caec89ae27/Package Resources/TMP Essential Resources.unitypackage", false);
            return;
        }
        ConfigureScene();
    }

    /// <summary>Resumes scene authoring after Unity completes its asynchronous resource import.</summary>
    /// <param name="package">Imported resource package.</param>
    private static void Imported(string package)
    {
        AssetDatabase.importPackageCompleted -= Imported;
        EditorApplication.delayCall += ConfigureScene;
    }

    /// <summary>Creates scene-owned audio and board components after TMP resources are available.</summary>
    private static void ConfigureScene()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Tool Testing Scenes/Test/Programming/Main/SCN_Blockout_LogicTest.unity", OpenSceneMode.Single);
        AudioPreset audio = AssetDatabase.LoadAssetAtPath<AudioPreset>("Assets/ScriptableObjects/Presets_Modules/Audio/AudioPreset.asset");
        if (audio == null)
            throw new InvalidOperationException("Audio preset did not reload after opening the gameplay scene.");
        GameplayAudio backend = UnityEngine.Object.FindFirstObjectByType<GameplayAudio>();
        if (backend == null)
            backend = new GameObject("Gameplay Audio").AddComponent<GameplayAudio>();
        using (SerializedObject data = new SerializedObject(backend))
        {
            data.FindProperty("preset").objectReferenceValue = audio;
            data.FindProperty("music").stringValue = "mus_radio";
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        PlayerCameraRig rig = UnityEngine.Object.FindFirstObjectByType<PlayerCameraRig>();
        if (rig != null && rig.View != null && rig.View.GetComponent<FMODUnity.StudioListener>() == null)
            rig.View.gameObject.AddComponent<FMODUnity.StudioListener>();
        if (UnityEngine.Object.FindFirstObjectByType<OrderBoard>() == null)
        {
            Type authoring = Type.GetType("CatOnASkateboard.ObjectsLogicStudio.Editor.OrderBoardWindow, CatOnASkateboard.ObjectsLogicStudio.Editor", true);
            OrderBoard board = (OrderBoard)authoring.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { scene });
            if (rig != null)
            {
                Vector3 origin = rig.transform.position;
                Quaternion heading = Quaternion.Euler(0f, rig.transform.eulerAngles.y, 0f);
                board.transform.SetPositionAndRotation(origin + heading * new Vector3(1.8f, 2.2f, 2.5f), heading);
            }
        }
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("GAMEPLAY SETUP: catalog, surfaces, arrival dialogue, order board and scene audio saved.");
        EditorApplication.Exit(0);
    }
    #endregion
}
