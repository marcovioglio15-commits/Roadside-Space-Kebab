using System;
using TMPro;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Reveals and animates existing TMP glyphs without rebuilding strings or creating UI each frame.</summary>
    internal sealed class DialogueTextRun
    {
        #region Types

        /// <summary>Retains layout indices and reveal timing for one TMP character.</summary>
        private struct Glyph
        {
            internal DialogueGlyphStyle Style;
            internal int Material;
            internal int Vertex;
            internal float Time;
            internal float Height;
            internal bool Visible;
        }

        #endregion

        #region State

        private TMP_Text text;
        private readonly DialogueRotationLayout rotation = new DialogueRotationLayout();
        private DialogueTextDocument document;
        private DialogueTextSettings settings;
        private Glyph[] glyphs = Array.Empty<Glyph>();
        private Vector3[][] vertices = Array.Empty<Vector3[]>();
        private Color32[][] colors = Array.Empty<Color32[]>();
        private int count;
        private float started;
        private float offset;
        private float frozen;
        private float revealEnd;
        private float arrivalEnd;
        private bool motion;
        private bool playing;

        #endregion

        #region Properties

        /// <summary>Scaled page time retained when its dialogue is interrupted.</summary>
        internal float Elapsed => playing ? Time.time - started + offset : frozen;

        #endregion

        #region Methods

        #region Ownership

        /// <summary>Parses one page, captures its full layout and starts from a retained reveal time.</summary>
        /// <param name="target">Saved TMP text graphic.</param>
        /// <param name="line">Page containing optional effect tokens.</param>
        /// <param name="configuration">Entry-wide effect settings.</param>
        /// <param name="elapsed">Previously retained scaled page time, or zero.</param>
        internal void Begin(TMP_Text target, DialogueLine line, DialogueTextSettings configuration, float elapsed)
        {
            Stop();
            if (!DialogueTextParser.TryParse(line.Text, configuration, out document, out string warning))
            {
                Debug.LogWarning(warning, target);
                return;
            }
            text = target;
            settings = configuration;
            started = Time.time;
            offset = elapsed;
            playing = true;
            text.text = document.Text;
            text.maxVisibleCharacters = int.MaxValue;
            // The complete layout prevents words moving as individual letters appear.
            text.ForceMeshUpdate(true, true);
            Capture(text.textInfo);
            text.OnPreRenderText += Regenerated;
            Apply(Elapsed);
            Upload();
        }

        /// <summary>Freezes reveal and effects while the box fades or the dialogue retains its page.</summary>
        internal void Pause()
        {
            frozen = Elapsed;
            playing = false;
        }

        /// <summary>Releases mesh callbacks before the shared graphic starts a different page.</summary>
        internal void Stop()
        {
            if (text != null)
                text.OnPreRenderText -= Regenerated;
            text = null;
            playing = false;
        }

        /// <summary>Finishes pending letters and landing poses without advancing the page.</summary>
        /// <returns>True when this input completed an unfinished reveal.</returns>
        internal bool RevealAll()
        {
            if (text == null || Elapsed >= Mathf.Max(revealEnd, arrivalEnd))
                return false;
            offset += Mathf.Max(revealEnd, arrivalEnd) - Elapsed;
            Apply(Elapsed);
            Upload();
            return true;
        }

        #endregion

        #region Layout

        /// <summary>Recaptures clean glyph geometry after TMP rebuilds for a font or layout change.</summary>
        /// <param name="info">New full-page TMP geometry.</param>
        private void Regenerated(TMP_TextInfo info)
        {
            Capture(info);
            Apply(Elapsed);
        }

        /// <summary>Reuses buffers and resolves rich-text source indices once per layout change.</summary>
        /// <param name="info">Full page before custom text effects.</param>
        private void Capture(TMP_TextInfo info)
        {
            count = info.characterCount;
            if (glyphs.Length < count)
                glyphs = new Glyph[count];
            if (vertices.Length < info.materialCount)
            {
                Array.Resize(ref vertices, info.materialCount);
                Array.Resize(ref colors, info.materialCount);
            }
            // Mesh buffers grow only at a page or layout boundary, never during ordinary effect ticks.
            for (int material = 0; material < info.materialCount; material++)
            {
                TMP_MeshInfo mesh = info.meshInfo[material];
                if (vertices[material] == null || vertices[material].Length < mesh.vertices.Length)
                {
                    vertices[material] = new Vector3[mesh.vertices.Length];
                    colors[material] = new Color32[mesh.colors32.Length];
                }
                Array.Copy(mesh.vertices, vertices[material], mesh.vertices.Length);
                Array.Copy(mesh.colors32, colors[material], mesh.colors32.Length);
            }
            float time = 0f;
            rotation.Apply(info, document, vertices);
            int previousSource = 0;
            motion = false;
            revealEnd = arrivalEnd = 0f;
            for (int index = 0; index < count; index++)
            {
                TMP_CharacterInfo character = info.characterInfo[index];
                int source = character.index;
                DialogueGlyphStyle style = document.Styles[source];
                // Pause markers inside rich-text tags still delay the next actual TMP character.
                if (settings.Typewriter)
                    for (int marker = previousSource; marker <= source; marker++)
                        time += document.Styles[marker].Pause;
                previousSource = source + 1;
                Glyph glyph = new Glyph { Style = style, Material = character.materialReferenceIndex, Vertex = character.vertexIndex,
                    Visible = character.isVisible, Time = settings.Typewriter ? time : 0f, Height = Mathf.Max(0.001f, character.ascender - character.descender) };
                glyphs[index] = glyph;
                revealEnd = glyph.Time;
                if (glyph.Visible)
                {
                    motion |= style.Shake || style.Wave || style.Rainbow;
                    if (style.Slam)
                        arrivalEnd = Mathf.Max(arrivalEnd, glyph.Time + settings.SlamDuration);
                }
                if (settings.Typewriter)
                {
                    time += 1f / style.Speed;
                    if (character.character is '.' or ',' or '!' or '?' or ':' or ';')
                        time += settings.PunctuationPause;
                }
            }
        }

        #endregion

        #region Animation

        /// <summary>Changes only vertex data while reveal or a moving word effect is active.</summary>
        internal void Tick()
        {
            if (!playing || text == null || Time.deltaTime <= 0f)
                return;
            float elapsed = Elapsed;
            if (!motion && elapsed > Mathf.Max(revealEnd, arrivalEnd) + Time.deltaTime)
                return;
            Apply(elapsed);
            Upload();
        }

        /// <summary>Restores each quad before applying current reveal, landing, word motion and tint.</summary>
        /// <param name="elapsed">Current scaled page time.</param>
        private void Apply(float elapsed)
        {
            for (int index = 0; index < count; index++)
            {
                Glyph glyph = glyphs[index];
                if (!glyph.Visible)
                    continue;
                TMP_MeshInfo mesh = text.textInfo.meshInfo[glyph.Material];
                Vector3 center = (vertices[glyph.Material][glyph.Vertex] + vertices[glyph.Material][glyph.Vertex + 2]) * 0.5f;
                Vector3 displacement = Vector3.zero;
                float scale = 1f;
                if (glyph.Style.Slam)
                {
                    float progress = Mathf.Clamp01((elapsed - glyph.Time) / settings.SlamDuration);
                    float remaining = (1f - progress) * (1f - progress) * (1f - progress);
                    scale += (settings.SlamScale - 1f) * remaining - Mathf.Sin(progress * Mathf.PI) * 0.12f;
                    displacement.y += glyph.Height * settings.SlamHeight * remaining;
                }
                if (glyph.Style.Shake)
                {
                    float phase = Mathf.Floor(elapsed * settings.ShakeRate) + index * 31f;
                    displacement += new Vector3(Noise(phase), Noise(phase + 37f)) * (glyph.Height * settings.ShakeAmount);
                }
                if (glyph.Style.Wave)
                    displacement.y += Mathf.Sin(elapsed * settings.WaveRate * Mathf.PI * 2f + index * 0.55f) * glyph.Height * settings.WaveAmount;
                Color tint = glyph.Style.Rainbow ? Color.HSVToRGB(Mathf.Repeat(elapsed * settings.RainbowRate + index * 0.025f, 1f), 0.75f, 1f)
                    : glyph.Style.Color;
                for (int corner = 0; corner < 4; corner++)
                {
                    int vertex = glyph.Vertex + corner;
                    mesh.vertices[vertex] = center + (vertices[glyph.Material][vertex] - center) * scale + displacement;
                    Color32 original = colors[glyph.Material][vertex];
                    Color32 color = glyph.Style.Colored || glyph.Style.Rainbow ? (Color32)tint : original;
                    color.a = elapsed < glyph.Time ? (byte)0 : glyph.Style.Colored || glyph.Style.Rainbow
                        ? (byte)(original.a * color.a / 255) : original.a;
                    mesh.colors32[vertex] = color;
                }
            }
        }

        /// <summary>Produces repeatable shake without changing Unity's gameplay random sequence.</summary>
        /// <param name="phase">Letter-specific discrete shake step.</param>
        /// <returns>A deterministic value between minus one and one.</returns>
        private static float Noise(float phase)
        {
            return Mathf.Repeat(Mathf.Sin(phase * 12.9898f) * 43758.5453f, 1f) * 2f - 1f;
        }

        /// <summary>Uploads existing mesh buffers without marking text or layout dirty.</summary>
        private void Upload()
        {
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }

        #endregion

        #endregion
    }
}
