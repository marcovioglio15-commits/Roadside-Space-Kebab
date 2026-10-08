using System;
using System.Collections.Generic;
using System.IO;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    public static class FieldVerificationEditor
    {
        private const string Folder = "Assets/__FieldVerification20261004";
        public static void Run()
        {
            ObjectWorkspace recovered = ObjectWorkspace.instance;
            if (recovered.Target.ObjectName == "A" && recovered.Prefab != null)
            {
                ObjectObserverSession observer = recovered.Observer;
                recovered.Observer = new ObjectObserverSession();
                recovered.Target.Capture(recovered.Prefab, 0);
                recovered.Extended.ComponentId = 0;
                ObjectWorkspaceSession.Discard(recovered);
                recovered.Observer = observer;
                recovered.Persist();
            }
            string original = EditorJsonUtility.ToJson(ObjectWorkspace.instance);
            bool failed = false;
            try
            {
                File.WriteAllText("Library/InteractionFields20261004/result.txt", "Editor checks\n");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (!AssetDatabase.IsValidFolder(Folder))
                    AssetDatabase.CreateFolder("Assets", "__FieldVerification20261004");
                OutlinePreset first = ScriptableObject.CreateInstance<OutlinePreset>();
                OutlinePreset second = ScriptableObject.CreateInstance<OutlinePreset>();
                AssetDatabase.CreateAsset(first, Folder + "/First.asset");
                AssetDatabase.CreateAsset(second, Folder + "/Second.asset");
                GameObject a = Prefab("A", first, 2f);
                GameObject b = Prefab("B", first, 3f);
                GameObject c = Prefab("C", second, 4f);
                ObjectOutline source = a.GetComponent<ObjectOutline>();
                ObjectWorkspace state = ObjectWorkspace.instance;
                state.Target.Capture(a, 0);
                state.Extended = new ExtendedInteractionSession();
                state.Extended.Select(source);
                state.Single = new SingleInteractionSession();
                state.HasBinding = false;
                state.Extended.Draft.Outline.Thickness = 9f;
                state.Extended.Draft.Outline.Intensity = 2f;
                ObjectFieldTarget target;
                using (SerializedObject data = new SerializedObject(state))
                    target = ObjectFieldTarget.Create(data.FindProperty("Extended.Draft.Outline.Thickness"));
                Check(target != null && target.Path == "settings.Thickness", "Workspace maps clicked field");
                List<string> skipped = new List<string>();
                string[] guids = { AssetDatabase.AssetPathToGUID(Folder + "/A.prefab"), AssetDatabase.AssetPathToGUID(Folder + "/B.prefab"), AssetDatabase.AssetPathToGUID(Folder + "/C.prefab") };
                Check(ObjectFieldBatch.Apply(target, true, guids, skipped) == 2 && skipped.Count == 0, "Same preset updates two matching prefabs");
                Check(a.GetComponent<ObjectOutline>().Settings.Thickness == 9f && b.GetComponent<ObjectOutline>().Settings.Thickness == 9f && c.GetComponent<ObjectOutline>().Settings.Thickness == 4f, "Other preset untouched");
                Check(a.GetComponent<ObjectOutline>().Settings.Intensity == 1f, "Unrelated pending field untouched");
                Check(File.ReadAllText(Folder + "/B.prefab").Contains("Thickness: 9"), "Changed field saved to disk");
                Undo.PerformUndo();
                Check(a.GetComponent<ObjectOutline>().Settings.Thickness == 2f && b.GetComponent<ObjectOutline>().Settings.Thickness == 3f, "Bulk prefab fields support native Undo");
                Check(ObjectFieldBatch.Apply(target, false, guids, skipped) == 3 && c.GetComponent<ObjectOutline>().Settings.Thickness == 9f, "Update All ignores preset identity");
                ObjectFieldWorkspace.Rebase(state, target);
                Check(state.Extended.Baseline.Outline.Thickness == 9f && state.Extended.Draft.Outline.Intensity == 2f && state.Extended.Baseline.Outline.Intensity == 1f, "Rebase preserves unrelated pending values");
                state.Extended.Draft.Outline.Thickness = -1f;
                using (SerializedObject data = new SerializedObject(state))
                    target = ObjectFieldTarget.Create(data.FindProperty("Extended.Draft.Outline.Thickness"));
                skipped.Clear();
                Check(ObjectFieldBatch.Apply(target, false, guids, skipped) == 0 && skipped.Count == 3, "Invalid values are rejected per prefab");
                Check(b.GetComponent<ObjectOutline>().Settings.Thickness == 9f, "Rejected trial restores saved values");
                Clipboard();
                Outline();
                PrepareGrandpa();
                HoverAssets();
                Check(new DialogueSettings().PreferSingleActions, "Dialogue priority defaults on");
                foreach (string guid in AssetDatabase.FindAssets("t:DialoguePreset"))
                {
                    DialoguePreset preset = AssetDatabase.LoadAssetAtPath<DialoguePreset>(AssetDatabase.GUIDToAssetPath(guid));
                    Check(preset.Settings.PreferSingleActions, "Existing dialogue preset defaults on");
                }
                EditorJsonUtility.FromJsonOverwrite(original, state);
                FieldVerificationRuntime.Begin();
            }
            catch (Exception exception)
            {
                File.AppendAllText("Library/InteractionFields20261004/result.txt", "FAIL " + exception + "\n");
                Debug.LogException(exception);
                failed = true;
            }
            finally
            {
                EditorJsonUtility.FromJsonOverwrite(original, ObjectWorkspace.instance);
                ObjectWorkspace.instance.Persist();
                AssetDatabase.DeleteAsset(Folder);
                if (failed)
                    EditorApplication.Exit(1);
            }
        }

        private static GameObject Prefab(string name, OutlinePreset preset, float thickness)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.SetActive(false);
            ObjectOutline outline = item.AddComponent<ObjectOutline>();
            outline.Settings.Thickness = thickness;
            outline.Settings.Intensity = 1f;
            using (SerializedObject data = new SerializedObject(outline))
            {
                data.FindProperty("settingsPreset").objectReferenceValue = preset;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            OutlineAuthoring.Rebuild(outline);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(item, Folder + "/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(item);
            return prefab;
        }

        private static void Clipboard()
        {
            DialoguePreset a = ScriptableObject.CreateInstance<DialoguePreset>();
            DialoguePreset b = ScriptableObject.CreateInstance<DialoguePreset>();
            DialogueEntry first = ScriptableObject.CreateInstance<DialogueEntry>();
            first.Name = "A";
            first.Lines = new[] { new DialogueLine { Text = "Copied", Speaker = "Guest" } };
            DialogueEntry second = ScriptableObject.CreateInstance<DialogueEntry>();
            second.Name = "B";
            a.Settings.Entries = new[] { first, second };
            using SerializedObject from = new SerializedObject(a);
            using SerializedObject to = new SerializedObject(b);
            StudioPropertyValue copied = new StudioPropertyValue(from.FindProperty("Settings.Entries"));
            a.Settings.Entries[0].Lines[0].Text = "Changed after Copy";
            copied.Apply(to.FindProperty("Settings.Entries"));
            to.ApplyModifiedPropertiesWithoutUndo();
            Check(b.Settings.Entries.Length == 2 && b.Settings.Entries[0] == first && b.Settings.Entries[1] == second,
                "Entry arrays preserve shared preset references");
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
            StudioPropertyValue number = new StudioPropertyValue(from.FindProperty("Settings.Distance"));
            Check(!number.Accepts(to.FindProperty("Settings.Priority")), "Incompatible numeric types rejected");
            StudioPropertyValue mode = new StudioPropertyValue(from.FindProperty("Settings.Trigger"));
            Check(!mode.Accepts(to.FindProperty("Settings.Selection")), "Different enum types rejected");
            mode.Apply(to.FindProperty("Settings.Trigger"));
            to.ApplyModifiedPropertiesWithoutUndo();
            Check(a.Settings.Trigger == b.Settings.Trigger, "Matching enum copied");
            StudioPropertyValue raw = new StudioPropertyValue(7.5f, typeof(float));
            Check(raw.Accepts(to.FindProperty("Settings.Distance")), "Raw scalar accepted by serialized field");
            raw.Apply(to.FindProperty("Settings.Distance"));
            to.ApplyModifiedPropertiesWithoutUndo();
            StudioPropertyValue restored = new StudioPropertyValue(to.FindProperty("Settings.Distance"));
            Check(restored.TryRead(out float distance) && distance == 7.5f, "Serialized scalar read by raw widget");
            StudioPropertyValue reference = new StudioPropertyValue(a, typeof(UnityEngine.Object));
            using SerializedObject workspace = new SerializedObject(ObjectWorkspace.instance);
            Check(!reference.Accepts(workspace.FindProperty("Source")), "Incompatible asset types rejected");
            HoverPreset hover = ScriptableObject.CreateInstance<HoverPreset>();
            reference = new StudioPropertyValue(hover, typeof(UnityEngine.Object));
            Check(reference.Accepts(workspace.FindProperty("Source")), "Base-typed object picker accepts compatible asset");
            UnityEngine.Object.DestroyImmediate(hover);
            UnityEngine.Object.DestroyImmediate(a);
            UnityEngine.Object.DestroyImmediate(b);
        }

        private static void Outline()
        {
            GameObject item = new GameObject("Outline test");
            item.SetActive(false);
            ObjectOutline outline = item.AddComponent<ObjectOutline>();
            outline.Refresh();
            Check(outline.TryValidate(out string warning) && outline.Renderers.Length == 0, "Meshless utility outline is valid");
            GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.transform.SetParent(item.transform);
            GameObject nested = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nested.transform.SetParent(item.transform);
            nested.AddComponent<ObjectItem>();
            outline.Refresh();
            Check(outline.Renderers.Length == 1 && outline.Renderers[0] == mesh.GetComponent<Renderer>(), "Refresh finds owned geometry only");
            UnityEngine.Object.DestroyImmediate(mesh);
            outline.Refresh();
            Check(outline.TryValidate(out warning) && outline.Renderers.Length == 0, "Removed geometry leaves no stale binding warning");
            UnityEngine.Object.DestroyImmediate(item);
        }

        private static void HoverAssets()
        {
            int components = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (ObjectHover hover in prefab.GetComponentsInChildren<ObjectHover>(true))
                {
                    Check(hover.Settings.TargetMode == HoverDetectionMode.CenterCollider, "Collider mode: " + prefab.name);
                    Check(hover.GetComponentInChildren<Collider>(true) != null || hover.GetComponent<ObjectAssemblyProduct>() != null,
                        "Target geometry exists or arrives with assembly: " + prefab.name);
                    components++;
                }
            }
            int presets = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:HoverPreset", new[] { "Assets" }))
            {
                HoverPreset preset = AssetDatabase.LoadAssetAtPath<HoverPreset>(AssetDatabase.GUIDToAssetPath(guid));
                Check(preset.Configuration.Settings.TargetMode == HoverDetectionMode.CenterCollider, "Preset collider mode: " + preset.name);
                presets++;
            }
            File.AppendAllText("Library/InteractionFields20261004/result.txt", "Hover assets: " + components + " components, " + presets + " presets\n");
        }

        private static void PrepareGrandpa()
        {
            string path = "Assets/Prefabs/Interactable Items/PF_Grandpa.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ObjectHover hover = root.GetComponentInChildren<ObjectHover>(true);
                if (hover.GetComponentInChildren<Collider>(true) != null)
                    return;
                Bounds bounds = default;
                bool found = false;
                foreach (Renderer renderer in hover.GetComponentsInChildren<Renderer>(true))
                    if (renderer is MeshRenderer or SkinnedMeshRenderer)
                        for (int corner = 0; corner < 8; corner++)
                        {
                            Bounds shape = renderer.bounds;
                            Vector3 point = hover.transform.InverseTransformPoint(shape.center + Vector3.Scale(shape.extents,
                                new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f)));
                            if (!found)
                                bounds = new Bounds(point, Vector3.zero);
                            else
                                bounds.Encapsulate(point);
                            found = true;
                        }
                Check(found && bounds.size.sqrMagnitude > 0f, "Grandpa mesh bounds available for hover collider");
                GameObject shapeObject = new GameObject("Hover Collider");
                shapeObject.transform.SetParent(hover.transform, false);
                shapeObject.layer = hover.gameObject.layer;
                BoxCollider collider = shapeObject.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.center = bounds.center;
                collider.size = bounds.size;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                File.AppendAllText("Library/InteractionFields20261004/result.txt", "Grandpa collider bounds " + bounds + "\n");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void Check(bool value, string message)
        {
            if (!value)
                throw new InvalidOperationException(message);
            File.AppendAllText("Library/InteractionFields20261004/result.txt", "PASS " + message + "\n");
        }
    }
}
