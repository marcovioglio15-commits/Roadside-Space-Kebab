using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Builds native group-only JSON fragments from the existing serialized group values.</summary>
    internal sealed class InteractionGroupWriter
    {
        #region Types

        /// <summary>Combines selected child paths without filling unrelated component settings.</summary>
        private sealed class Node
        {
            internal readonly Dictionary<string, Node> Children = new Dictionary<string, Node>();
            internal ObjectPresetValue Value;
        }

        #endregion

        #region State

        private readonly StringBuilder text = new StringBuilder();
        private readonly List<string> fragments = new List<string>();
        private readonly List<InteractionGroupReference> references = new List<InteractionGroupReference>();

        #endregion

        #region Methods

        #region Preparation

        /// <summary>Compiles only the fields explicitly included in one group.</summary>
        /// <param name="preset">Existing group preset used by the normal tool.</param>
        /// <param name="kind">Matching runtime component family.</param>
        /// <returns>A patch that preserves all unselected instance fields.</returns>
        internal static InteractionGroupPatch Compile(ObjectGroupPreset preset, InteractionRuntimeKind kind)
        {
            if (preset.Values == null || preset.Group.Paths == null || preset.Values.Length != preset.Group.Paths.Length)
                throw new InvalidOperationException(preset.name + ": group values and fields do not match.");
            Node root = new Node();
            // Input bindings, enabled state and local names remain owned by the contact operation.
            for (int index = 0; index < preset.Values.Length; index++)
            {
                string path = ObjectPresetPaths.Component(preset.Group.Paths[index], kind);
                if (string.IsNullOrEmpty(path) || path is "interactionName" or "m_Enabled" or "action" or "startAction" or "advanceAction" or "drawGizmos")
                    continue;
                Node current = root;
                foreach (string field in path.Split('.'))
                {
                    if (!current.Children.TryGetValue(field, out Node child))
                    {
                        child = new Node();
                        current.Children.Add(field, child);
                    }
                    current = child;
                }
                current.Value = preset.Values[index];
            }
            if (root.Children.Count == 0)
                throw new InvalidOperationException(preset.name + ": this group contains only local interaction bindings.");
            InteractionGroupWriter writer = new InteractionGroupWriter();
            writer.Write(root);
            writer.fragments.Add(writer.text.ToString());
            return new InteractionGroupPatch { Kind = kind, Name = preset.name, Fragments = writer.fragments.ToArray(), References = writer.references.ToArray() };
        }

        #endregion

        #region Values

        /// <summary>Writes selected nested fields while preserving the native component field structure.</summary>
        /// <param name="node">Selected component or settings subtree.</param>
        private void Write(Node node)
        {
            if (node.Value != null)
            {
                Write(node.Value);
                return;
            }
            text.Append('{');
            bool separator = false;
            // Dictionary order is used consistently for both fragments and reference slots.
            foreach (KeyValuePair<string, Node> child in node.Children)
            {
                if (separator)
                    text.Append(',');
                Quote(child.Key);
                text.Append(':');
                Write(child.Value);
                separator = true;
            }
            text.Append('}');
        }

        /// <summary>Writes one native serialized value, replacing object IDs with portable slots.</summary>
        /// <param name="value">Value captured by the existing group preset system.</param>
        private void Write(ObjectPresetValue value)
        {
            // Portable links can also represent local interaction IDs and hover anchor paths.
            if (value.Link != null && (value.Link.Local || value.Link.External || value.Link.Asset != null))
            {
                fragments.Add(text.ToString());
                text.Clear();
                references.Add(InteractionGroupCompiler.Reference(value.Link));
                return;
            }
            if (value.Array || value.Kind is SerializedPropertyType.Generic or SerializedPropertyType.ManagedReference)
            {
                text.Append(value.Array ? '[' : '{');
                bool separator = false;
                foreach (ObjectPresetValue child in value.Children)
                {
                    if (child.Name is "GroupPresets" or "RequiresAction" or "GroupWarning" or "CompiledKind")
                        continue;
                    if (separator)
                        text.Append(',');
                    if (!value.Array)
                    {
                        Quote(child.Name);
                        text.Append(':');
                    }
                    Write(child);
                    separator = true;
                }
                text.Append(value.Array ? ']' : '}');
                return;
            }
            switch (value.Kind)
            {
                case SerializedPropertyType.Boolean:
                    text.Append(value.Integer != 0 ? "true" : "false");
                    break;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.RenderingLayerMask:
                    text.Append(value.Integer.ToString(CultureInfo.InvariantCulture));
                    break;
                case SerializedPropertyType.Float:
                    text.Append(value.Number.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case SerializedPropertyType.String:
                case SerializedPropertyType.Hash128:
                    Quote(value.Text ?? string.Empty);
                    break;
                case SerializedPropertyType.ObjectReference:
                case SerializedPropertyType.ExposedReference:
                    text.Append("{\"instanceID\":0}");
                    break;
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Vector4:
                    text.Append(JsonUtility.ToJson(value.Vector));
                    break;
                case SerializedPropertyType.Quaternion:
                    text.Append(JsonUtility.ToJson(new Quaternion(value.Vector.x, value.Vector.y, value.Vector.z, value.Vector.w)));
                    break;
                case SerializedPropertyType.Vector2Int:
                case SerializedPropertyType.Vector3Int:
                    text.Append(JsonUtility.ToJson(value.IntegerVector));
                    break;
                case SerializedPropertyType.Color:
                    text.Append(JsonUtility.ToJson(value.Color));
                    break;
                case SerializedPropertyType.Rect:
                    text.Append(JsonUtility.ToJson(value.Rectangle));
                    break;
                case SerializedPropertyType.RectInt:
                    text.Append(JsonUtility.ToJson(value.IntegerRectangle));
                    break;
                case SerializedPropertyType.Bounds:
                    text.Append(JsonUtility.ToJson(value.Bounds));
                    break;
                case SerializedPropertyType.BoundsInt:
                    text.Append(JsonUtility.ToJson(value.IntegerBounds));
                    break;
                case SerializedPropertyType.AnimationCurve:
                    text.Append(NativeField(value, "Curve"));
                    break;
                case SerializedPropertyType.Gradient:
                    text.Append(NativeField(value, "Gradient"));
                    break;
                default:
                    throw new InvalidOperationException("Unsupported group value: " + value.Name);
            }
        }

        /// <summary>Escapes field names and text without introducing a JSON library dependency.</summary>
        /// <param name="value">Text copied from a group field.</param>
        private void Quote(string value)
        {
            text.Append('"');
            // Control characters must remain valid JSON when a page contains line breaks or tabs.
            foreach (char character in value)
                switch (character)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\t': text.Append("\\t"); break;
                    default:
                        if (character < 32)
                            text.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            text.Append(character);
                        break;
                }
            text.Append('"');
        }

        /// <summary>Extracts native curve or gradient data without reproducing Unity's serialization format.</summary>
        /// <param name="value">Container holding the native serialized value.</param>
        /// <param name="field">Native field to extract.</param>
        /// <returns>The complete native JSON value.</returns>
        private static string NativeField(ObjectPresetValue value, string field)
        {
            string json = JsonUtility.ToJson(value);
            int start = json.IndexOf("\"" + field + "\":", StringComparison.Ordinal) + field.Length + 3;
            int depth = 0;
            bool quoted = false;
            bool escaped = false;
            // Balanced delimiters preserve nested native curve and gradient arrays.
            for (int index = start; index < json.Length; index++)
            {
                char character = json[index];
                if (quoted)
                {
                    if (escaped)
                        escaped = false;
                    else if (character == '\\')
                        escaped = true;
                    else if (character == '"')
                        quoted = false;
                    continue;
                }
                if (character == '"')
                    quoted = true;
                else if (character is '{' or '[')
                    depth++;
                else if (character is '}' or ']')
                {
                    if (depth == 0)
                        return json.Substring(start, index - start);
                    depth--;
                }
                else if (character == ',' && depth == 0)
                    return json.Substring(start, index - start);
            }
            throw new InvalidOperationException("Cannot compile native group field " + field + ".");
        }

        #endregion

        #endregion
    }
}
