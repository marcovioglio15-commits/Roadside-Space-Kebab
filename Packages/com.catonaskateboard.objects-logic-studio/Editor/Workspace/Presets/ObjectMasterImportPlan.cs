using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioIdentity;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Validates master imports on an isolated copy before changing the destination prefab.</summary>
    internal sealed class ObjectMasterImportPlan : IDisposable
    {
        #region State

        private readonly ObjectMasterPreset master;
        private readonly GameObject destination;
        private readonly ObjectImportChoices.Interaction[] choices;
        private readonly List<ObjectImportChoices.Node> nodes;
        private readonly List<ObjectImportChoices.Link> replacements;
        private readonly bool replace;
        private readonly bool shared;
        private readonly Dictionary<long, ObjectInteraction> sources = new Dictionary<long, ObjectInteraction>();
        private readonly Dictionary<string, UnityEngine.Object> resolved = new Dictionary<string, UnityEngine.Object>();
        private readonly List<ObjectPresetLink> missing = new List<ObjectPresetLink>();
        private readonly List<string> removedNames = new List<string>();
        private GameObject preview;
        private GameObject working;
        private bool commit;

        #endregion

        #region Properties

        /// <summary>Slots that still need an explicit replacement.</summary>
        internal IReadOnlyList<ObjectPresetLink> Missing => missing;
        /// <summary>Exact interactions removed by the previewed Replace operation.</summary>
        internal IReadOnlyList<string> RemovedNames => removedNames;

        #endregion

        #region Methods

        #region Lifetime

        /// <summary>Retains the concrete import choices shown in the preview window.</summary>
        /// <param name="master">Master asset to import.</param>
        /// <param name="destination">Destination configuration root.</param>
        /// <param name="choices">Selected interactions and update targets.</param>
        /// <param name="nodes">Explicit hierarchy-slot mappings.</param>
        /// <param name="replacements">Explicit reference replacements.</param>
        /// <param name="replace">Whether other interactions in the selected slots are removed.</param>
        /// <param name="shared">Whether identity and shared item settings are imported.</param>
        internal ObjectMasterImportPlan(ObjectMasterPreset master, GameObject destination, ObjectImportChoices.Interaction[] choices,
            List<ObjectImportChoices.Node> nodes, List<ObjectImportChoices.Link> replacements, bool replace, bool shared)
        {
            this.master = master;
            this.destination = destination;
            this.choices = choices;
            this.nodes = nodes;
            this.replacements = replacements;
            this.replace = replace;
            this.shared = shared;
        }

        /// <summary>Releases only the isolated validation copy and temporary editor identities.</summary>
        public void Dispose()
        {
            if (preview != null)
            {
                ObjectPreviewIdentity.Unregister(preview);
                UnityEngine.Object.DestroyImmediate(preview);
                preview = null;
            }
        }

        #endregion

        #region Validation

        /// <summary>Returns one isolated result for a field comparison without exposing a live destination edit.</summary>
        /// <param name="sourceId">Interaction identity stored in the master.</param>
        /// <returns>The corresponding preview component, or null when excluded.</returns>
        internal ObjectInteraction PreviewInteraction(long sourceId)
        {
            return sources.TryGetValue(sourceId, out ObjectInteraction feature) ? feature : null;
        }

        /// <summary>Builds and validates the complete result without saving a prefab or shared asset.</summary>
        /// <param name="warning">Receives unresolved slots or invalid interaction settings.</param>
        /// <returns>True when the exact import result is ready to apply.</returns>
        internal bool Validate(out string warning)
        {
            warning = string.Empty;
            if (master == null || destination == null || master.Format != 1 || choices.Length != master.Interactions.Length)
            {
                warning = "Choose a supported master preset and a destination object.";
                return false;
            }
            Dispose();
            preview = ObjectPresetCapture.Preview(destination);
            working = preview;
            commit = false;
            try
            {
                Build();
                if (missing.Count > 0)
                {
                    warning = "Choose replacements for the missing object links.";
                    return false;
                }
                foreach (ObjectInteraction feature in sources.Values)
                {
                    if (!feature.enabled)
                        continue;
                    bool valid = feature switch
                    {
                        ObjectHover hover => hover.TryValidate(out warning),
                        ObjectSingleInteraction single => single.TryValidate(out warning),
                        ObjectExtendedInteraction extended => extended.TryValidate(out warning),
                        _ => false
                    };
                    if (!valid)
                    {
                        warning = feature.InteractionName + ": " + warning;
                        return false;
                    }
                }
                if (missing.Count > 0)
                {
                    warning = "Choose replacements for the missing object links.";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                warning = exception.Message;
                return false;
            }
        }

        /// <summary>Applies only after isolated validation succeeds, with rollback if saving fails.</summary>
        /// <param name="warning">Receives validation or save failures.</param>
        /// <returns>True when the destination prefab was saved.</returns>
        internal bool Apply(out string warning)
        {
            if (!Validate(out warning) || !ObjectAuthoringSave.TryValidate(destination, out warning) || EditorUtility.IsPersistent(destination))
                return false;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Import object configuration");
            working = destination;
            commit = true;
            try
            {
                Build();
                if (missing.Count > 0)
                    throw new InvalidOperationException("An object link changed. Review the import preview again.");
                foreach (ObjectInteraction feature in sources.Values)
                    ObjectStructureChanges.Prepare(feature);
                ObjectAuthoringSave.Save(destination);
                Undo.CollapseUndoOperations(group);
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                ObjectAuthoringSave.Save(destination);
                warning = "Import could not finish: " + exception.Message;
                return false;
            }
            finally
            {
                ObjectPreviewIdentity.Unregister(destination);
            }
        }

        #endregion

        #region Construction

        /// <summary>Creates all component slots before resolving any links between interactions.</summary>
        private void Build()
        {
            sources.Clear();
            resolved.Clear();
            missing.Clear();
            removedNames.Clear();
            List<(ObjectComponentPreset Preset, Component Target)> components = new List<(ObjectComponentPreset, Component)>();
            if (shared)
                foreach (ObjectMasterPreset.SharedEntry entry in master.Shared)
                {
                    Transform node = Node(entry.Path);
                    if (entry.Preset == null)
                        throw new InvalidOperationException("A shared settings preset is missing.");
                    foreach (ObjectComponentPreset settings in entry.Preset.Components)
                    {
                        Type type = Type.GetType(settings.TypeName, true);
                        if (type != typeof(ObjectIdentity) && type != typeof(ObjectItem))
                            throw new InvalidOperationException("This shared preset uses an unsupported component.");
                        Component target = node.GetComponent(type) ?? Add(node.gameObject, type);
                        components.Add((settings, target));
                    }
                }
            HashSet<ObjectInteraction> retained = new HashSet<ObjectInteraction>();
            List<int> order = new List<int>();
            for (int index = 0; index < choices.Length; index++)
                order.Add(index);
            // Create Grab before release actions so Unity cannot add an untracked required component.
            order.Sort((left, right) => ObjectInteractionCatalog.Priority(Type.GetType(master.Interactions[left].Preset?.Configuration?.TypeName ?? string.Empty, false))
                .CompareTo(ObjectInteractionCatalog.Priority(Type.GetType(master.Interactions[right].Preset?.Configuration?.TypeName ?? string.Empty, false))));
            foreach (int index in order)
            {
                if (!choices[index].Include)
                    continue;
                ObjectMasterPreset.InteractionEntry entry = master.Interactions[index];
                if (entry.SourceId == 0 || sources.ContainsKey(entry.SourceId))
                    throw new InvalidOperationException("This master contains an invalid interaction link. Save it again from the source object.");
                if (entry.Preset?.Configuration == null)
                    throw new InvalidOperationException("An interaction preset is missing.");
                Type type = Type.GetType(entry.Preset.Configuration.TypeName, true);
                if (ObjectInteractionCatalog.Find(type).Type == null)
                    throw new InvalidOperationException("This master uses an interaction that is not installed.");
                Transform node = Node(entry.Path);
                ObjectInteraction target = choices[index].Add ? null : Map(choices[index].Target) as ObjectInteraction;
                if (!choices[index].Add && target == null)
                    throw new InvalidOperationException("Choose which " + entry.Name + " interaction to update, or choose Add new.");
                if (target != null && (target.GetType() != type || target.transform != node || !retained.Add(target)))
                    throw new InvalidOperationException("Each interaction needs its own matching target.");
                if (target == null)
                {
                    if (type.IsDefined(typeof(DisallowMultipleComponent), true) && node.GetComponent(type) != null)
                        throw new InvalidOperationException("This object already has " + ObjectInteractionCatalog.Find(type).Name + ". Choose it as the update target.");
                    target = (ObjectInteraction)Add(node.gameObject, type);
                    target.enabled = false;
                    retained.Add(target);
                }
                sources[entry.SourceId] = target;
                components.Add((entry.Preset.Configuration, target));
            }
            if (replace)
                RemoveOthers(retained);
            foreach ((ObjectComponentPreset preset, Component target) in components)
                PrepareBindings(preset, target);
            ObjectPreviewIdentity.Register(working);
            foreach ((ObjectComponentPreset preset, Component target) in components)
                Write(preset, target);
            // Group references are the reusable source; the complete snapshot preserves other hidden values.
            for (int index = 0; index < choices.Length; index++)
                if (choices[index].Include)
                    ApplyGroups(master.Interactions[index].Preset, sources[master.Interactions[index].SourceId], choices[index]);
        }

        /// <summary>Resolves one hierarchy slot, rejecting missing and duplicate paths.</summary>
        /// <param name="path">Source slot path.</param>
        /// <returns>The chosen destination transform.</returns>
        private Transform Node(string path)
        {
            ObjectImportChoices.Node choice = nodes.Find(node => node.Path == path);
            if (choice?.Target != null && !choice.Target.IsChildOf(destination.transform))
                throw new InvalidOperationException("Choose an object inside the destination hierarchy.");
            Transform node = choice?.Target != null ? Map(choice.Target) as Transform : ObjectPresetHierarchy.Find(working.transform, path);
            return node != null ? node : throw new InvalidOperationException("Choose an object for this slot: " + (string.IsNullOrEmpty(path) ? "Object root" : path));
        }

        /// <summary>Creates components through Undo only when editing the real destination.</summary>
        /// <param name="target">Object receiving the component.</param>
        /// <param name="type">Validated component type.</param>
        /// <returns>The added component.</returns>
        private Component Add(GameObject target, Type type)
        {
            return commit ? Undo.AddComponent(target, type) : target.AddComponent(type);
        }

        /// <summary>Recreates only the staging and transition helpers explicitly owned by imported interactions.</summary>
        /// <param name="preset">Complete source component snapshot.</param>
        /// <param name="target">Matching component already created in the destination.</param>
        private void PrepareBindings(ObjectComponentPreset preset, Component target)
        {
            ObjectMasterPreset.InteractionEntry entry = Array.Find(master.Interactions, candidate => candidate.Preset?.Configuration == preset);
            if (entry == null)
                return;
            foreach (ObjectPresetValue field in preset.Fields)
            {
                if (field.Link is not { Local: true } link || link.Resolve(working.transform) != null)
                    continue;
                string prefix = string.IsNullOrEmpty(entry.Path) ? string.Empty : entry.Path + "/";
                switch (field.Name)
                {
                    case "staging" when target is ObjectAssemblyStation or ObjectSpawnManager:
                        if (!link.Path.StartsWith(prefix, StringComparison.Ordinal) || link.Path.Substring(prefix.Length).Contains('/'))
                            break;
                        string name = link.Path.Substring(prefix.Length);
                        Transform staging = ObjectPresetHierarchy.Find(target.transform, name);
                        if (staging == null)
                        {
                            GameObject created = new GameObject(name);
                            created.SetActive(false);
                            if (commit)
                            {
                                Undo.RegisterCreatedObjectUndo(created, "Import interaction staging");
                                Undo.SetTransformParent(created.transform, target.transform, "Parent interaction staging");
                            }
                            else
                                created.transform.SetParent(target.transform, false);
                            staging = created.transform;
                            staging.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                        }
                        resolved[link.Key] = staging;
                        break;
                    case "transition" when target is ObjectSpawnManager && link.Path == entry.Path:
                        Type type = typeof(CatOnASkateboard.MenuStudio.MenuSceneTransition);
                        resolved[link.Key] = target.GetComponent(type) ?? Add(target.gameObject, type);
                        break;
                }
            }
        }

        /// <summary>Writes a complete settings snapshot with explicit portable reference resolution.</summary>
        /// <param name="preset">Saved component fields.</param>
        /// <param name="target">Matching destination component.</param>
        private void Write(ObjectComponentPreset preset, Component target)
        {
            if (commit)
                Undo.RecordObject(target, "Import component settings");
            using SerializedObject data = new SerializedObject(target);
            ObjectMasterPreset.InteractionEntry entry = Array.Find(master.Interactions, candidate => candidate.Preset?.Configuration == preset);
            HashSet<string> overrides = target is ObjectInteraction feature ? ObjectPresetPaths.Overrides(entry?.Preset, feature) : null;
            foreach (ObjectPresetValue field in preset.Fields)
                field.Apply(data.FindProperty(field.Name), working.transform, Resolve, !commit, overrides);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Applies granular group overrides through the existing typed draft mapping.</summary>
        /// <param name="preset">Main interaction preset.</param>
        /// <param name="target">Resolved interaction component.</param>
        /// <param name="choice">Individual group selection from the import preview.</param>
        private void ApplyGroups(ObjectInteractionPreset preset, ObjectInteraction target, ObjectImportChoices.Interaction choice)
        {
            ObjectWorkspace document = ObjectPresetCapture.Document(target);
            try
            {
                foreach (ObjectGroupPreset group in preset.Groups)
                {
                    if (group == null)
                        throw new InvalidOperationException("A settings group preset is missing.");
                    if (choice.ChooseGroups && !choice.Groups.Contains(group.Group.Key))
                        continue;
                    ObjectGroupPresetStore.Import(group, document, group.Group, Resolve, !commit);
                }
                switch (target)
                {
                    case ObjectHover hover:
                        ObjectWorkspaceSession.ApplyHover(document, hover, commit);
                        break;
                    case ObjectSingleInteraction:
                        document.Single.Apply(target.gameObject, commit, commit);
                        break;
                    case ObjectExtendedInteraction:
                        document.Extended.Apply(target.gameObject, commit, commit);
                        break;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(document);
            }
        }

        /// <summary>Removes only registered interactions on the object slots covered by this import.</summary>
        /// <param name="retained">Interactions explicitly selected by the import rows.</param>
        private void RemoveOthers(HashSet<ObjectInteraction> retained)
        {
            HashSet<Transform> slots = new HashSet<Transform>();
            foreach (ObjectMasterPreset.InteractionEntry entry in master.Interactions)
                slots.Add(Node(entry.Path));
            List<ObjectInteraction> removed = new List<ObjectInteraction>();
            foreach (Transform slot in slots)
                foreach (ObjectInteraction feature in slot.GetComponents<ObjectInteraction>())
                    if (!retained.Contains(feature) && ObjectInteractionCatalog.Find(feature.GetType()).Type != null)
                        removed.Add(feature);
            // Keep references from untouched components valid; selected interactions receive new links below.
            foreach (Component component in working.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is ObjectInteraction interaction && (retained.Contains(interaction) || removed.Contains(interaction)))
                    continue;
                using SerializedObject data = new SerializedObject(component);
                using SerializedProperty property = data.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue is ObjectInteraction referenced && removed.Contains(referenced))
                        throw new InvalidOperationException(component.name + " still uses " + referenced.InteractionName + ". Keep that interaction or change the link first.");
            }
            removed.Sort((left, right) => (left is ObjectGrab ? 1 : 0).CompareTo(right is ObjectGrab ? 1 : 0));
            foreach (ObjectInteraction feature in removed)
            {
                removedNames.Add(feature.gameObject.name + " / " + feature.InteractionName + " · " + ObjectInteractionCatalog.Name(feature));
                if (feature is ObjectGrab && feature.GetComponents<ObjectRelease>().Length > 0)
                    throw new InvalidOperationException("Keep Grab while this object has Drop or Throw.");
                if (commit)
                    Undo.DestroyObjectImmediate(feature);
                else
                    UnityEngine.Object.DestroyImmediate(feature);
            }
        }

        #endregion

        #region References

        /// <summary>Resolves repeated links once and records every missing required slot for the preview.</summary>
        /// <param name="link">Portable source reference.</param>
        /// <returns>The explicit replacement, corresponding interaction, local slot or asset.</returns>
        private UnityEngine.Object Resolve(ObjectPresetLink link)
        {
            if (link == null)
                return null;
            if (resolved.TryGetValue(link.Key, out UnityEngine.Object cached))
                return cached;
            ObjectImportChoices.Link replacement = replacements.Find(choice => choice.Key == link.Key);
            Transform replacementTransform = replacement?.Target is GameObject gameObject ? gameObject.transform
                : replacement?.Target is Component component ? component.transform : null;
            if (replacementTransform != null && !EditorUtility.IsPersistent(replacement.Target) && !replacementTransform.IsChildOf(destination.transform))
                throw new InvalidOperationException("Choose a replacement inside this object, or use an asset.");
            UnityEngine.Object value = replacement?.Target != null ? Map(replacement.Target)
                : link.Local && sources.TryGetValue(link.FileId, out ObjectInteraction feature) ? feature
                : link.Local ? ResolveLocal(link) : link.Resolve(working.transform);
            if (value == null && (link.Local || link.External) && !missing.Exists(slot => slot.Key == link.Key))
                missing.Add(link);
            resolved[link.Key] = value;
            return value;
        }

        /// <summary>Honors explicit object-slot mappings and stable same-prefab identities for anchors and other local links.</summary>
        /// <param name="link">Local source reference.</param>
        /// <returns>The matching real or preview reference, or null when a choice is required.</returns>
        private UnityEngine.Object ResolveLocal(ObjectPresetLink link)
        {
            ObjectImportChoices.Node selected = null;
            foreach (ObjectImportChoices.Node node in nodes)
                if (node.Target != null && (string.IsNullOrEmpty(node.Path) || link.Path == node.Path || link.Path.StartsWith(node.Path + "/", StringComparison.Ordinal))
                    && (selected == null || node.Path.Length > selected.Path.Length))
                    selected = node;
            bool remapped = selected != null && AnimationUtility.CalculateTransformPath(selected.Target, destination.transform) != selected.Path;
            if (!remapped && link.Resolve(destination.transform) is UnityEngine.Object existing)
                return Map(existing);
            if (selected == null)
                return link.Resolve(working.transform);
            string relative = string.IsNullOrEmpty(selected.Path) ? link.Path : link.Path.Length == selected.Path.Length ? string.Empty : link.Path.Substring(selected.Path.Length + 1);
            Transform root = Map(selected.Target) as Transform;
            return root != null ? link.ResolveAt(ObjectPresetHierarchy.Find(root, relative)) : null;
        }

        /// <summary>Maps a real destination reference to its corresponding validation-copy reference.</summary>
        /// <param name="value">Chosen destination object or external asset.</param>
        /// <returns>The preview equivalent, or the original external reference.</returns>
        private UnityEngine.Object Map(UnityEngine.Object value)
        {
            if (commit || value == null)
                return value;
            Transform transform = value is GameObject gameObject ? gameObject.transform : value is Component component ? component.transform : null;
            if (transform == null || !transform.IsChildOf(destination.transform))
                return value;
            Transform node = ObjectPresetHierarchy.Find(working.transform, AnimationUtility.CalculateTransformPath(transform, destination.transform));
            if (node == null)
                return null;
            if (value is GameObject)
                return node.gameObject;
            Component[] original = transform.GetComponents(value.GetType());
            Component[] copied = node.GetComponents(value.GetType());
            int index = Array.IndexOf(original, value);
            return index >= 0 && index < copied.Length ? copied[index] : null;
        }

        #endregion

        #endregion
    }
}
