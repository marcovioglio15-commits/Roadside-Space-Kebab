using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Stores editor preset fields with explicit serialized types and portable object links.</summary>
    [Serializable]
    internal sealed class ObjectPresetValue
    {
        #region Fields

        [Header("Value")]
        [Tooltip("Field name or array index.")]
        public string Name;
        [Tooltip("Serialized field type.")]
        public SerializedPropertyType Kind;
        [Tooltip("Concrete serialized type name.")]
        public string TypeName;
        [Tooltip("Whether this field is an array.")]
        public bool Array;
        [Tooltip("Whole-number value.")]
        public long Integer;
        [Tooltip("Decimal value.")]
        public double Number;
        [Tooltip("Text or managed type name.")]
        public string Text;
        [Tooltip("Vector or quaternion coordinates.")]
        public Vector4 Vector;
        [Tooltip("Whole-number vector coordinates.")]
        public Vector3Int IntegerVector;
        [Tooltip("Color value.")]
        public Color Color;
        [Tooltip("Rectangle value.")]
        public Rect Rectangle;
        [Tooltip("Whole-number rectangle value.")]
        public RectInt IntegerRectangle;
        [Tooltip("Bounds value.")]
        public Bounds Bounds;
        [Tooltip("Whole-number bounds value.")]
        public BoundsInt IntegerBounds;
        [Tooltip("Animation curve.")]
        public AnimationCurve Curve;
        [Tooltip("Color gradient.")]
        public Gradient Gradient;
        [Tooltip("Asset or object slot used by this field.")]
        public ObjectPresetLink Link;
        [Tooltip("Fields or elements inside this value.")]
        [SerializeReference]
        public List<ObjectPresetValue> Children = new List<ObjectPresetValue>();

        #endregion

        #region Methods

        #region Capture

        /// <summary>Copies a field and its nested values without keeping a live SerializedProperty.</summary>
        /// <param name="property">Source field.</param>
        /// <param name="root">Configuration root used to describe local object links.</param>
        /// <returns>A durable editor preset value.</returns>
        internal static ObjectPresetValue Capture(SerializedProperty property, Transform root)
        {
            ObjectPresetValue value = new ObjectPresetValue
            {
                Name = property.name, Kind = property.propertyType, TypeName = property.type,
                Array = property.isArray && property.propertyType != SerializedPropertyType.String
            };
            if (value.Array)
            {
                for (int index = 0; index < property.arraySize; index++)
                    value.Children.Add(Capture(property.GetArrayElementAtIndex(index), root));
                return value;
            }
            switch (value.Kind)
            {
                case SerializedPropertyType.Generic:
                case SerializedPropertyType.ManagedReference:
                    value.Text = value.Kind == SerializedPropertyType.ManagedReference ? property.managedReferenceFullTypename : string.Empty;
                    using (SerializedProperty child = property.Copy())
                    using (SerializedProperty end = property.GetEndProperty())
                        if (child.Next(true))
                            do
                            {
                                if (child.depth <= property.depth || SerializedProperty.EqualContents(child, end))
                                    break;
                                value.Children.Add(Capture(child, root));
                            }
                            while (child.Next(false));
                    break;
                case SerializedPropertyType.Boolean:
                    value.Integer = property.boolValue ? 1 : 0;
                    break;
                case SerializedPropertyType.Integer:
                    value.Integer = property.longValue;
                    if (root != null && value.Integer != 0 && IsInteractionId(property.propertyPath))
                    {
                        foreach (ObjectInteraction candidate in root.GetComponentsInChildren<ObjectInteraction>(true))
                            if (ObjectWorkspaceTarget.FileId(candidate) == value.Integer)
                            {
                                value.Link = ObjectPresetLink.Capture(candidate, root);
                                break;
                            }
                        value.Link ??= new ObjectPresetLink { External = true, Label = property.displayName,
                            SourceGuid = "interaction:" + value.Integer, TypeName = typeof(ObjectInteraction).AssemblyQualifiedName };
                    }
                    break;
                case SerializedPropertyType.Character:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.RenderingLayerMask:
                    value.Integer = property.intValue;
                    break;
                case SerializedPropertyType.Float:
                    value.Number = property.doubleValue;
                    break;
                case SerializedPropertyType.String:
                    value.Text = property.stringValue;
                    if (property.propertyPath == "Binding.AnchorPath" && property.serializedObject.targetObject is ObjectWorkspace workspace)
                        value.Link = ObjectPresetLink.Capture(HoverHierarchy.Resolve(workspace.Target.Resolve()?.transform, value.Text), root);
                    break;
                case SerializedPropertyType.ObjectReference:
                    value.Link = ObjectPresetLink.Capture(property.objectReferenceValue, root);
                    if (property.objectReferenceValue == null && EntityId.ToULong(property.objectReferenceEntityIdValue) != 0UL)
                        value.Link = new ObjectPresetLink { External = true, Label = "Missing " + property.displayName,
                            SourceGuid = "missing:" + EntityId.ToULong(property.objectReferenceEntityIdValue), TypeName = typeof(UnityEngine.Object).AssemblyQualifiedName };
                    break;
                case SerializedPropertyType.ExposedReference:
                    value.Link = ObjectPresetLink.Capture(property.exposedReferenceValue, root);
                    break;
                case SerializedPropertyType.Vector2:
                    value.Vector = property.vector2Value;
                    break;
                case SerializedPropertyType.Vector3:
                    value.Vector = property.vector3Value;
                    break;
                case SerializedPropertyType.Vector4:
                    value.Vector = property.vector4Value;
                    break;
                case SerializedPropertyType.Quaternion:
                    Quaternion rotation = property.quaternionValue;
                    value.Vector = new Vector4(rotation.x, rotation.y, rotation.z, rotation.w);
                    break;
                case SerializedPropertyType.Vector2Int:
                    Vector2Int vector = property.vector2IntValue;
                    value.IntegerVector = new Vector3Int(vector.x, vector.y, 0);
                    break;
                case SerializedPropertyType.Vector3Int:
                    value.IntegerVector = property.vector3IntValue;
                    break;
                case SerializedPropertyType.Color:
                    value.Color = property.colorValue;
                    break;
                case SerializedPropertyType.Rect:
                    value.Rectangle = property.rectValue;
                    break;
                case SerializedPropertyType.RectInt:
                    value.IntegerRectangle = property.rectIntValue;
                    break;
                case SerializedPropertyType.Bounds:
                    value.Bounds = property.boundsValue;
                    break;
                case SerializedPropertyType.BoundsInt:
                    value.IntegerBounds = property.boundsIntValue;
                    break;
                case SerializedPropertyType.AnimationCurve:
                    value.Curve = property.animationCurveValue;
                    break;
                case SerializedPropertyType.Gradient:
                    value.Gradient = property.gradientValue;
                    break;
                case SerializedPropertyType.Hash128:
                    value.Text = property.hash128Value.ToString();
                    break;
                default:
                    throw new InvalidOperationException("This field cannot be saved in a preset: " + property.propertyPath);
            }
            return value;
        }

        #endregion

        #region Transfer

        /// <summary>Writes a saved field after checking its serialized type.</summary>
        /// <param name="property">Destination field.</param>
        /// <param name="root">Destination configuration root.</param>
        /// <param name="resolve">Optional import-slot resolver.</param>
        /// <param name="allowMissing">Allows an isolated preview to collect all missing links before validation.</param>
        /// <param name="overrides">Component paths supplied by group presets instead of this baseline.</param>
        internal void Apply(SerializedProperty property, Transform root, Func<ObjectPresetLink, UnityEngine.Object> resolve = null, bool allowMissing = false, ISet<string> overrides = null)
        {
            if (property != null && overrides != null && overrides.Contains(property.propertyPath))
                return;
            if (property == null || property.propertyType != Kind || Kind != SerializedPropertyType.ManagedReference && property.type != TypeName)
                throw new InvalidOperationException("This preset uses different settings for " + (property != null ? property.displayName : Name) + ". Choose a matching preset.");
            if (Array)
            {
                property.arraySize = Children.Count;
                for (int index = 0; index < Children.Count; index++)
                    Children[index].Apply(property.GetArrayElementAtIndex(index), root, resolve, allowMissing, overrides);
                return;
            }
            switch (Kind)
            {
                case SerializedPropertyType.Generic:
                case SerializedPropertyType.ManagedReference:
                    if (Kind == SerializedPropertyType.ManagedReference)
                    {
                        if (string.IsNullOrEmpty(Text))
                            property.managedReferenceValue = null;
                        else
                        {
                            int separator = Text.IndexOf(' ');
                            Type type = Type.GetType(Text.Substring(separator + 1) + ", " + Text.Substring(0, separator), true);
                            if (property.managedReferenceValue?.GetType() != type)
                                property.managedReferenceValue = Activator.CreateInstance(type);
                        }
                    }
                    foreach (ObjectPresetValue child in Children)
                        child.Apply(property.FindPropertyRelative(child.Name), root, resolve, allowMissing, overrides);
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = Integer != 0;
                    break;
                case SerializedPropertyType.Integer:
                    property.longValue = Link != null ? ObjectWorkspaceTarget.FileId(Resolve(root, resolve, allowMissing)) : Integer;
                    break;
                case SerializedPropertyType.Character:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.RenderingLayerMask:
                    property.intValue = (int)Integer;
                    break;
                case SerializedPropertyType.Float:
                    property.doubleValue = Number;
                    break;
                case SerializedPropertyType.String:
                    if (Link is { Local: true } or { External: true })
                    {
                        Transform anchor = Resolve(root, resolve, allowMissing) as Transform;
                        Transform owner = (property.serializedObject.targetObject as ObjectWorkspace)?.Target.Resolve()?.transform;
                        if (anchor != null && owner != null && !anchor.IsChildOf(owner))
                            throw new InvalidOperationException("Choose an anchor inside this interaction's object.");
                        property.stringValue = anchor != null && owner != null ? HoverHierarchy.Path(owner, anchor) : "-";
                    }
                    else
                        property.stringValue = Text;
                    break;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = Resolve(root, resolve, allowMissing);
                    break;
                case SerializedPropertyType.ExposedReference:
                    property.exposedReferenceValue = Resolve(root, resolve, allowMissing);
                    break;
                case SerializedPropertyType.Vector2:
                    property.vector2Value = Vector;
                    break;
                case SerializedPropertyType.Vector3:
                    property.vector3Value = Vector;
                    break;
                case SerializedPropertyType.Vector4:
                    property.vector4Value = Vector;
                    break;
                case SerializedPropertyType.Quaternion:
                    property.quaternionValue = new Quaternion(Vector.x, Vector.y, Vector.z, Vector.w);
                    break;
                case SerializedPropertyType.Vector2Int:
                    property.vector2IntValue = new Vector2Int(IntegerVector.x, IntegerVector.y);
                    break;
                case SerializedPropertyType.Vector3Int:
                    property.vector3IntValue = IntegerVector;
                    break;
                case SerializedPropertyType.Color:
                    property.colorValue = Color;
                    break;
                case SerializedPropertyType.Rect:
                    property.rectValue = Rectangle;
                    break;
                case SerializedPropertyType.RectInt:
                    property.rectIntValue = IntegerRectangle;
                    break;
                case SerializedPropertyType.Bounds:
                    property.boundsValue = Bounds;
                    break;
                case SerializedPropertyType.BoundsInt:
                    property.boundsIntValue = IntegerBounds;
                    break;
                case SerializedPropertyType.AnimationCurve:
                    property.animationCurveValue = Curve;
                    break;
                case SerializedPropertyType.Gradient:
                    property.gradientValue = Gradient;
                    break;
                case SerializedPropertyType.Hash128:
                    property.hash128Value = Hash128.Parse(Text);
                    break;
            }
        }

        /// <summary>Rejects missing required slots instead of silently clearing a reference.</summary>
        /// <param name="root">Destination root.</param>
        /// <param name="resolve">Optional import-slot resolver.</param>
        /// <param name="allowMissing">Whether a preview is still collecting missing slots.</param>
        /// <returns>The resolved reference, including a deliberately empty source reference.</returns>
        private UnityEngine.Object Resolve(Transform root, Func<ObjectPresetLink, UnityEngine.Object> resolve, bool allowMissing)
        {
            if (Link == null)
                return null;
            UnityEngine.Object value = resolve != null ? resolve(Link) : Link.Resolve(root);
            if (!allowMissing && value == null && (Link.Local || Link.External))
                throw new InvalidOperationException("Choose a replacement for the object link: " + (Link.Path.Length > 0 ? Link.Path : Link.Label));
            return value;
        }

        /// <summary>Recognizes stable interaction IDs used by the existing typed editor drafts.</summary>
        /// <param name="path">Serialized draft path.</param>
        /// <returns>True for a component-reference ID field or array element.</returns>
        private static bool IsInteractionId(string path)
        {
            return path.EndsWith(".TargetId", StringComparison.Ordinal) || path.EndsWith(".ReplacementId", StringComparison.Ordinal)
                || path.EndsWith(".OrdersCompletionId", StringComparison.Ordinal) || path.Contains(".SourceIds.Array.data[") || path.Contains(".TargetIds.Array.data[");
        }

        /// <summary>Lists referenced slots and assets, including nested arrays.</summary>
        /// <param name="links">List receiving each reference description.</param>
        internal void CollectLinks(List<ObjectPresetLink> links)
        {
            if (Link != null)
                links.Add(Link);
            foreach (ObjectPresetValue child in Children)
                child.CollectLinks(links);
        }

        #endregion

        #endregion
    }
}
