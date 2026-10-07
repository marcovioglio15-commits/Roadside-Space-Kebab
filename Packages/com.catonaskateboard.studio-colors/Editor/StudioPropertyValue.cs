using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Retains a typed serialized field without keeping a live SerializedProperty handle.</summary>
    public sealed class StudioPropertyValue
    {
        #region State

        private readonly string type;
        private readonly Type declaredType;
        private readonly bool raw;
        private readonly SerializedPropertyType kind;
        private readonly object value;
        private readonly List<KeyValuePair<string, StudioPropertyValue>> children;
        private readonly int count;

        #endregion

        #region Methods

        #region Capture

        /// <summary>Copies one field, including array contents and references, at the time Copy is chosen.</summary>
        /// <param name="property">Field whose value is retained.</param>
        /// <param name="mapReference">Optional capture-time mapping to persistent local references.</param>
        public StudioPropertyValue(SerializedProperty property, Func<UnityEngine.Object, UnityEngine.Object> mapReference = null)
        {
            // Generic blocks are walked explicitly; boxed arrays are not supported by every Unity serializer.
            type = property.type;
            declaredType = StudioPropertySchema.Resolve(property);
            kind = property.propertyType;
            count = property.isArray && kind != SerializedPropertyType.String ? property.arraySize : -1;
            if (kind != SerializedPropertyType.Generic)
            {
                value = kind switch
                {
                    SerializedPropertyType.Enum or SerializedPropertyType.LayerMask or SerializedPropertyType.ArraySize => property.intValue,
                    SerializedPropertyType.ObjectReference => mapReference != null ? mapReference(property.objectReferenceValue) : property.objectReferenceValue,
                    _ => property.boxedValue
                };
                return;
            }
            children = new List<KeyValuePair<string, StudioPropertyValue>>();
            if (count >= 0)
            {
                for (int index = 0; index < count; index++)
                    children.Add(new KeyValuePair<string, StudioPropertyValue>("Array.data[" + index + "]", new StudioPropertyValue(property.GetArrayElementAtIndex(index), mapReference)));
                return;
            }
            using SerializedProperty child = property.Copy();
            using SerializedProperty end = property.GetEndProperty();
            if (child.Next(true))
                do
                {
                    if (child.depth <= property.depth || SerializedProperty.EqualContents(child, end))
                        break;
                    children.Add(new KeyValuePair<string, StudioPropertyValue>(child.name, new StudioPropertyValue(child, mapReference)));
                }
                while (child.Next(false));
        }

        /// <summary>Retains a transient widget value using the same clipboard as serialized fields.</summary>
        /// <param name="value">Value copied from a native tool widget.</param>
        /// <param name="type">Declared widget type, including the concrete enum type.</param>
        public StudioPropertyValue(object value, Type type)
        {
            // Transient fields have no SerializedProperty but can still exchange compatible scalar values.
            this.value = value;
            declaredType = type;
            raw = true;
        }

        /// <summary>Reads a scalar clipboard value for an ordinary nonserialized tool widget.</summary>
        /// <typeparam name="T">Native widget value type.</typeparam>
        /// <param name="result">Receives the compatible copied value.</param>
        /// <returns>True when the clipboard contains a compatible scalar or reference.</returns>
        public bool TryRead<T>(out T result)
        {
            // Enums retain their own declared type even when Unity stores their serialized value as an integer.
            object scalar = !raw && kind == SerializedPropertyType.Enum && declaredType != null
                ? Enum.ToObject(declaredType, value) : value;
            if (children == null && (scalar is T || scalar == null && !typeof(T).IsValueType
                && declaredType != null && typeof(T).IsAssignableFrom(declaredType)))
            {
                result = (T)scalar;
                return true;
            }
            result = default;
            return false;
        }

        #endregion

        #region Transfer

        /// <summary>Checks the complete destination schema before modifying a field.</summary>
        /// <param name="property">Destination field or null when a corresponding array row is absent.</param>
        /// <returns>True when the destination stores the same serialized type.</returns>
        public bool Accepts(SerializedProperty property)
        {
            // Object pickers may expose a base type; the actual reference must fit the destination field.
            if (property == null)
                return false;
            Type destination = StudioPropertySchema.Resolve(property);
            if (property.propertyType == SerializedPropertyType.ObjectReference
                && (raw ? typeof(UnityEngine.Object).IsAssignableFrom(declaredType) : kind == SerializedPropertyType.ObjectReference))
                return destination != null && (value == null || destination.IsInstanceOfType(value));
            // Enums must retain their exact type even when their underlying numbers coincide.
            return (declaredType == null || declaredType == destination || destination == null && kind != SerializedPropertyType.Enum)
                && (raw || property.propertyType == kind && property.type == type);
        }

        /// <summary>Writes only this field; optional reference mapping keeps prefab-local links local.</summary>
        /// <param name="property">Compatible destination field.</param>
        /// <param name="mapReference">Optional resolver for component and hierarchy references.</param>
        public void Apply(SerializedProperty property, Func<UnityEngine.Object, UnityEngine.Object> mapReference = null)
        {
            // Reject incompatible targets before touching their arrays or child values.
            if (!Accepts(property))
                throw new InvalidOperationException("The copied field and destination have different serialized types: "
                    + (property != null ? property.propertyPath + " (" + property.type + ")" : "missing field") + ", source " + type + ".");
            if (raw)
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference)
                    property.objectReferenceValue = mapReference != null ? mapReference((UnityEngine.Object)value) : (UnityEngine.Object)value;
                else if (declaredType.IsEnum)
                    property.intValue = Convert.ToInt32(value);
                else
                    property.boxedValue = value;
                return;
            }
            if (children != null)
            {
                if (count >= 0)
                    property.arraySize = count;
                foreach (KeyValuePair<string, StudioPropertyValue> child in children)
                    child.Value.Apply(property.FindPropertyRelative(child.Key), mapReference);
                return;
            }
            switch (kind)
            {
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.ArraySize:
                    property.intValue = (int)value;
                    break;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = mapReference != null ? mapReference((UnityEngine.Object)value) : (UnityEngine.Object)value;
                    break;
                default:
                    property.boxedValue = value;
                    break;
            }
        }

        #endregion

        #endregion
    }
}
