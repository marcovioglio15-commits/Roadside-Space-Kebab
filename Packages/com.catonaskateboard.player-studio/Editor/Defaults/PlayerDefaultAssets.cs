using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Creates missing defaults in one embedded content package without overwriting existing assets.</summary>
    [InitializeOnLoad]
    internal static class PlayerDefaultAssets
    {
        #region Paths

        internal const string ContentRoot = "Packages/com.catonaskateboard.player-studio.content";
        internal const string DefaultsRoot = ContentRoot + "/Defaults";

        #endregion

        private static bool resolving;
        private static double resolveDeadline;

        #region Methods

        #region Bootstrap

        /// <summary>Defers first-import setup until Unity can safely import newly created assets.</summary>
        static PlayerDefaultAssets()
        {
            // Batch verification invokes Ensure explicitly; ordinary imports prepare defaults once.
            if (!Application.isBatchMode)
                EditorApplication.delayCall += Initialize;
        }

        /// <summary>Prepares a project-owned content package, leaving the installed code package untouched.</summary>
        private static void Initialize()
        {
            // Never create assets during Play, import workers or an active compilation.
            if (EditorApplication.isPlayingOrWillChangePlaymode || AssetDatabase.IsAssetImportWorkerProcess())
                return;
            try
            {
                Ensure();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Player Studio defaults could not be prepared: " + exception.Message);
            }
        }

        /// <summary>Creates missing editable defaults and returns the existing or newly created master.</summary>
        /// <returns>The project-owned default master; existing values are preserved.</returns>
        internal static PlayerMasterPreset Ensure()
        {
            // A separate embedded package remains writable when the code package comes from a registry.
            Directory.CreateDirectory(ContentRoot);
            string manifest = ContentRoot + "/package.json";
            if (!File.Exists(manifest))
                File.WriteAllText(manifest, @"{""name"":""com.catonaskateboard.player-studio.content"",""version"":""1.0.0"",""displayName"":""Player Studio Content"",""description"":""Editable Player Studio defaults owned by this project.""}");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (!AssetDatabase.IsValidFolder(ContentRoot))
            {
                if (!resolving)
                {
                    resolving = true;
                    resolveDeadline = EditorApplication.timeSinceStartup + 60d;
                    Client.Resolve();
                    EditorApplication.update += FinishResolution;
                }
                return null;
            }
            PlayerDefaultRecovery.RestoreMissing();
            PlayerMasterPreset master = CreateMissing(DefaultsRoot);
            PlayerTestScene.Ensure();
            PlayerDefaultRecovery.CaptureMissing();
            return master;
        }

        /// <summary>Builds a complete default collection in an explicit writable package folder.</summary>
        /// <param name="folder">Content folder; distribution preparation also uses this for bundled templates.</param>
        /// <returns>The master connecting this collection.</returns>
        internal static PlayerMasterPreset CreateMissing(string folder)
        {
            // Reuse each asset independently; no personalized preset receives default values again.
            EnsureFolder(folder);
            EnsureFolder(folder + "/Presets");
            EnsureFolder(folder + "/Input");
            EnsureFolder(folder + "/Visual");
            EnsureFolder(folder + "/Player");
            InputActionAsset actions = CreateActions(folder + "/Input/PlayerControls.inputactions");
            PlayerBodyPreset body = Preset<PlayerBodyPreset>(folder, "Body", null);
            PlayerLocomotionPreset movement = Preset<PlayerLocomotionPreset>(folder, "Locomotion", value =>
            {
                using SerializedObject serialized = new SerializedObject(value);
                serialized.FindProperty("useJump").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });
            GameObject capsule = CreateCapsule(folder + "/Visual");
            PlayerVisualPreset visual = Preset<PlayerVisualPreset>(folder, "Visual",
                value => PlayerCreationUtility.SetReferences(value, ("prefab", capsule)));
            PlayerInputPreset input = Preset<PlayerInputPreset>(folder, "Input", value =>
            {
                // Names select only authored default content; runtime always resolves the stored action IDs.
                PlayerCreationUtility.SetReferences(value,
                    ("movementAction", CreateReference(actions, "Move", folder)),
                    ("jumpAction", CreateReference(actions, "Jump", folder)),
                    ("lookDeltaAction", CreateReference(actions, "LookDelta", folder)),
                    ("lookRateAction", CreateReference(actions, "LookRate", folder)),
                    ("cursorToggleAction", CreateReference(actions, "Cursor", folder)));
            });
            PlayerCameraPreset camera = Preset<PlayerCameraPreset>(folder, "Camera", null);
            PlayerMasterPreset master = Preset<PlayerMasterPreset>(folder, "Master",
                value => PlayerCreationUtility.SetReferences(value, ("bodyPreset", body), ("locomotionPreset", movement),
                    ("visualPreset", visual), ("inputPreset", input), ("cameraPreset", camera)));
            string prefabPath = folder + "/Player/Player.prefab";
            if (!File.Exists(prefabPath))
            {
                Scene preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    GameObject root = PlayerCreationUtility.Create(master, preview, "Player");
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(preview);
                }
            }
            return master;
        }

        /// <summary>Continues first-import setup after Unity registers the embedded content package.</summary>
        private static void FinishResolution()
        {
            // Wait through Editor callbacks without blocking Unity's package manager.
            if (!AssetDatabase.IsValidFolder(ContentRoot) && EditorApplication.timeSinceStartup < resolveDeadline)
                return;
            EditorApplication.update -= FinishResolution;
            resolving = false;
            if (!AssetDatabase.IsValidFolder(ContentRoot))
                Debug.LogWarning("Player Studio content package is not registered yet. Reopen the tool after Package Manager finishes.");
            else
                Initialize();
        }

        #endregion

        #region Assets

        /// <summary>Loads an existing preset or initializes a new one before saving it.</summary>
        /// <typeparam name="T">Preset type to create.</typeparam>
        /// <param name="folder">Default collection root.</param>
        /// <param name="name">Stable module file name.</param>
        /// <param name="configure">New-asset initialization only.</param>
        /// <returns>The preserved or newly configured preset.</returns>
        private static T Preset<T>(string folder, string name, Action<T> configure) where T : ScriptableObject
        {
            // A conflicting file is an error, never permission to replace it.
            string path = folder + "/Presets/" + name + ".asset";
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;
            if (File.Exists(path))
                throw new IOException("An incompatible asset already exists at " + path);
            T created = ScriptableObject.CreateInstance<T>();
            configure?.Invoke(created);
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssetIfDirty(created);
            return created;
        }

        /// <summary>Creates an editable dedicated action asset with ordinary keyboard, mouse and gamepad bindings.</summary>
        /// <param name="path">Destination input asset path.</param>
        /// <returns>The imported asset, independent of project-wide actions.</returns>
        private static InputActionAsset CreateActions(string path)
        {
            // Bindings are initial asset content; no runtime script assumes these action or control names.
            if (!File.Exists(path))
            {
                InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
                try
                {
                    InputActionMap map = asset.AddActionMap("Player");
                    InputAction move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
                    move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                        .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
                    move.AddBinding("<Gamepad>/leftStick");
                    InputAction jump = map.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
                    jump.AddBinding("<Gamepad>/buttonSouth");
                    map.AddAction("LookDelta", InputActionType.PassThrough, "<Mouse>/delta", expectedControlLayout: "Vector2");
                    map.AddAction("LookRate", InputActionType.Value, "<Gamepad>/rightStick", expectedControlLayout: "Vector2");
                    map.AddAction("Cursor", InputActionType.Button, "<Keyboard>/escape");
                    File.WriteAllText(path, asset.ToJson());
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }
            }
            InputActionAsset result = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            return result != null ? result : throw new IOException("Cannot load input asset " + path);
        }

        /// <summary>Persists an action reference separately so preset fields retain its GUID identity.</summary>
        /// <param name="asset">Dedicated default actions.</param>
        /// <param name="name">Initial role name in the authored defaults.</param>
        /// <param name="folder">Default collection root.</param>
        /// <returns>Existing or newly created action reference.</returns>
        private static InputActionReference CreateReference(InputActionAsset asset, string name, string folder)
        {
            // A personalized reference is reused even if its target action was renamed.
            string path = folder + "/Input/" + name + ".asset";
            InputActionReference reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(path);
            if (reference != null)
                return reference;
            if (File.Exists(path))
                throw new IOException("An incompatible reference exists at " + path);
            reference = InputActionReference.Create(asset.FindAction(name, true));
            AssetDatabase.CreateAsset(reference, path);
            return reference;
        }

        /// <summary>Creates a renderer-only capsule prefab and URP material when they are absent.</summary>
        /// <param name="folder">Visual content folder.</param>
        /// <returns>The default capsule prefab.</returns>
        private static GameObject CreateCapsule(string folder)
        {
            // Existing geometry and materials are never changed by bootstrap.
            string path = folder + "/Capsule.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return existing;
            if (File.Exists(path))
                throw new IOException("An incompatible prefab exists at " + path);
            string materialPath = folder + "/Capsule.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null || File.Exists(materialPath))
                    throw new IOException("A URP Lit shader and a writable capsule material path are required.");
                material = new Material(shader) { color = new Color(0.15f, 0.6f, 0.8f) };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                SceneManager.MoveGameObjectToScene(capsule, scene);
                UnityEngine.Object.DestroyImmediate(capsule.GetComponent<Collider>());
                capsule.name = "Capsule";
                capsule.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                capsule.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
                capsule.GetComponent<MeshRenderer>().sharedMaterial = material;
                return PrefabUtility.SaveAsPrefabAsset(capsule, path);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        /// <summary>Creates only the missing directory segments through the Asset Database.</summary>
        /// <param name="path">Project-relative package content path.</param>
        private static void EnsureFolder(string path)
        {
            // Native creation makes new folders immediately addressable by subsequent asset imports.
            if (AssetDatabase.IsValidFolder(path))
                return;
            if (string.IsNullOrEmpty(path) || path == "Packages")
                throw new IOException("The content package must be registered before creating defaults.");
            string parent = Path.GetDirectoryName(path).Replace(Path.DirectorySeparatorChar, '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        #endregion

        #endregion
    }
}
