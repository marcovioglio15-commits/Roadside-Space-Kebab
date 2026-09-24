using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Explains the shared outline material parameters in the material inspector.</summary>
    public sealed class OutlineShaderEditor : ShaderGUI
    {
        #region Methods

        #region Inspector

        /// <summary>Draws shader fields with the same units and depth behavior used by the interaction tool.</summary>
        /// <param name="editor">Material inspector handling Undo and multi-object changes.</param>
        /// <param name="properties">Shader properties supplied by Unity.</param>
        public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties)
        {
            // The interaction overrides color and thickness per renderer without cloning this material.
            editor.ShaderProperty(FindProperty("_OutlineColor", properties),
                new GUIContent("Outline Color", "Shell color and opacity before per-object interaction overrides."));
            editor.ShaderProperty(FindProperty("_OutlineThickness", properties),
                new GUIContent("Thickness (Metres)", "World-space extrusion. The tool converts its thickness value to metres by dividing it by 250."));
            editor.ShaderProperty(FindProperty("_DepthTest", properties),
                new GUIContent("Depth Test", "LessEqual respects scene occlusion; Always displays the outline through other geometry."));
        }

        #endregion

        #endregion
    }
}
