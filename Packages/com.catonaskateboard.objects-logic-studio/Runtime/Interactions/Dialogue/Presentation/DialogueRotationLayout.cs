using System;
using TMPro;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Rotates token spans within each visual line and reserves their final vertical bounds.</summary>
    internal sealed class DialogueRotationLayout
    {
        #region State

        private float[] tops = Array.Empty<float>();
        private float[] bottoms = Array.Empty<float>();
        private float[] shifts = Array.Empty<float>();

        #endregion

        #region Methods

        #region Layout

        /// <summary>Changes captured page geometry once per TMP layout, before reveal and animated effects.</summary>
        /// <param name="info">Complete clean TMP character and line layout.</param>
        /// <param name="document">Parsed token styles indexed by source character.</param>
        /// <param name="vertices">Reusable clean mesh buffers receiving the adjusted layout.</param>
        internal void Apply(TMP_TextInfo info, DialogueTextDocument document, Vector3[][] vertices)
        {
            if (info.characterCount == 0 || info.lineCount == 0
                || !Array.Exists(document.Styles, style => style.Rotation != 0f))
                return;
            if (tops.Length < info.lineCount)
            {
                tops = new float[info.lineCount];
                bottoms = new float[info.lineCount];
                shifts = new float[info.lineCount];
            }
            // Original ascenders and descenders keep ordinary line spacing and inline rich text intact.
            for (int line = 0; line < info.lineCount; line++)
            {
                tops[line] = info.lineInfo[line].ascender;
                bottoms[line] = info.lineInfo[line].descender;
            }
            for (int index = 0; index < info.characterCount;)
            {
                TMP_CharacterInfo character = info.characterInfo[index];
                DialogueGlyphStyle style = document.Styles[character.index];
                if (style.Rotation == 0f)
                {
                    index++;
                    continue;
                }
                int end = index + 1;
                // Wrapped fragments rotate independently, keeping each fragment on its own visual line.
                while (end < info.characterCount && info.characterInfo[end].lineNumber == character.lineNumber
                    && document.Styles[info.characterInfo[end].index].RotationGroup == style.RotationGroup)
                    end++;
                Rotate(info, vertices, index, end, style.Rotation);
                index = end;
            }
            // Include all quads, including SDF padding on neighboring unrotated text.
            for (int index = 0; index < info.characterCount; index++)
            {
                TMP_CharacterInfo character = info.characterInfo[index];
                if (!character.isVisible)
                    continue;
                Vector3[] mesh = vertices[character.materialReferenceIndex];
                for (int corner = 0; corner < 4; corner++)
                {
                    tops[character.lineNumber] = Mathf.Max(tops[character.lineNumber], mesh[character.vertexIndex + corner].y);
                    bottoms[character.lineNumber] = Mathf.Min(bottoms[character.lineNumber], mesh[character.vertexIndex + corner].y);
                }
            }
            shifts[0] = Mathf.Min(0f, info.lineInfo[0].ascender - tops[0]);
            // Expanded line envelopes move following lines down, preserving their original minimum gap.
            for (int line = 1; line < info.lineCount; line++)
            {
                float gap = Mathf.Max(0f, info.lineInfo[line - 1].descender - info.lineInfo[line].ascender);
                shifts[line] = Mathf.Min(shifts[line - 1], bottoms[line - 1] + shifts[line - 1] - gap - tops[line]);
            }
            for (int index = 0; index < info.characterCount; index++)
            {
                TMP_CharacterInfo character = info.characterInfo[index];
                if (!character.isVisible)
                    continue;
                Vector3[] mesh = vertices[character.materialReferenceIndex];
                for (int corner = 0; corner < 4; corner++)
                    mesh[character.vertexIndex + corner].y += shifts[character.lineNumber];
            }
        }

        /// <summary>Rotates one contiguous span around its combined center rather than rotating letters separately.</summary>
        /// <param name="info">Clean page character indices.</param>
        /// <param name="vertices">Captured mesh buffers.</param>
        /// <param name="start">First TMP character in this fragment.</param>
        /// <param name="end">Exclusive final character.</param>
        /// <param name="degrees">Counterclockwise angle in degrees.</param>
        private void Rotate(TMP_TextInfo info, Vector3[][] vertices, int start, int end, float degrees)
        {
            Vector3 minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity);
            Vector3 maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity);
            bool visible = false;
            // Whitespace belongs to the span but does not contribute an invisible quad at the origin.
            for (int index = start; index < end; index++)
            {
                TMP_CharacterInfo character = info.characterInfo[index];
                if (!character.isVisible)
                    continue;
                visible = true;
                Vector3[] mesh = vertices[character.materialReferenceIndex];
                for (int corner = 0; corner < 4; corner++)
                {
                    minimum = Vector3.Min(minimum, mesh[character.vertexIndex + corner]);
                    maximum = Vector3.Max(maximum, mesh[character.vertexIndex + corner]);
                }
            }
            if (!visible)
                return;
            Vector3 center = (minimum + maximum) * 0.5f;
            Quaternion rotation = Quaternion.Euler(0f, 0f, degrees);
            for (int index = start; index < end; index++)
            {
                TMP_CharacterInfo character = info.characterInfo[index];
                if (!character.isVisible)
                    continue;
                Vector3[] mesh = vertices[character.materialReferenceIndex];
                for (int corner = 0; corner < 4; corner++)
                {
                    int vertex = character.vertexIndex + corner;
                    mesh[vertex] = center + rotation * (mesh[vertex] - center);
                    tops[character.lineNumber] = Mathf.Max(tops[character.lineNumber], mesh[vertex].y);
                    bottoms[character.lineNumber] = Mathf.Min(bottoms[character.lineNumber], mesh[vertex].y);
                }
            }
        }

        #endregion

        #endregion
    }
}
