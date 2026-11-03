using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Parses nested bracket effects once per page while preserving existing TMP rich text.</summary>
    internal static class DialogueTextParser
    {
        #region Types

        /// <summary>Retains the complete previous style until its matching closing token.</summary>
        private readonly struct Scope
        {
            internal readonly string Name;
            internal readonly DialogueGlyphStyle Style;

            #region Methods

            /// <summary>Captures one nested token boundary.</summary>
            /// <param name="name">Token requiring a matching close.</param>
            /// <param name="style">Style to restore after that close.</param>
            internal Scope(string name, DialogueGlyphStyle style)
            {
                Name = name;
                Style = style;
            }

            #endregion
        }

        #endregion

        #region Methods

        #region Parsing

        /// <summary>Builds source-indexed styles and reports unmatched or invalid effect tokens.</summary>
        /// <param name="source">Complete page, including optional bracket tokens.</param>
        /// <param name="settings">Entry-wide timing and initial letter style.</param>
        /// <param name="document">Receives text and styles when parsing succeeds.</param>
        /// <param name="warning">Receives the first token problem.</param>
        /// <returns>True when tokens are balanced and numeric arguments are valid.</returns>
        internal static bool TryParse(string source, DialogueTextSettings settings, out DialogueTextDocument document, out string warning)
        {
            document = null;
            warning = string.Empty;
            StringBuilder text = new StringBuilder(source.Length);
            List<DialogueGlyphStyle> styles = new List<DialogueGlyphStyle>(source.Length);
            Stack<Scope> scopes = new Stack<Scope>();
            DialogueGlyphStyle current = new DialogueGlyphStyle { Speed = settings.LettersPerSecond, Slam = settings.Slam };
            float pause = 0f;
            int rotationGroup = 0;
            // Tokens alter later characters only; no preset or shared string is changed.
            for (int index = 0; index < source.Length; index++)
            {
                char character = source[index];
                if (character == '[' && index + 1 < source.Length && source[index + 1] == '[')
                    index++;
                else if (character == '[')
                {
                    int end = source.IndexOf(']', index + 1);
                    if (end < 0)
                    {
                        warning = "Close text tokens with ]. Use [[ for a literal opening bracket.";
                        return false;
                    }
                    string token = source.Substring(index + 1, end - index - 1);
                    if (!Apply(token, settings, scopes, ref current, ref pause, ref rotationGroup, out warning))
                        return false;
                    index = end;
                    continue;
                }
                current.Pause = pause;
                styles.Add(current);
                text.Append(character);
                pause = 0f;
            }
            warning = scopes.Count > 0 ? "Close the " + scopes.Peek().Name + " text token before the page ends." : string.Empty;
            if (warning.Length > 0)
                return false;
            document = new DialogueTextDocument(text.ToString(), styles.ToArray());
            return true;
        }

        /// <summary>Applies one explicit token or restores a matching nested style.</summary>
        /// <param name="token">Contents between brackets.</param>
        /// <param name="settings">Palette and default rotation of this entry.</param>
        /// <param name="scopes">Open effect scopes.</param>
        /// <param name="current">Current style inherited by following characters.</param>
        /// <param name="pause">Additional delay for the next character.</param>
        /// <param name="rotationGroup">Unique identity of each rotated span.</param>
        /// <param name="warning">Receives an invalid name, argument or closing order.</param>
        /// <returns>True when this token is complete and supported.</returns>
        private static bool Apply(string token, DialogueTextSettings settings, Stack<Scope> scopes, ref DialogueGlyphStyle current,
            ref float pause, ref int rotationGroup, out string warning)
        {
            warning = "Unknown text token [" + token + "].";
            if (token.StartsWith("/", StringComparison.Ordinal))
            {
                if (scopes.Count == 0 || scopes.Peek().Name != token.Substring(1))
                {
                    warning = "Close text tokens in reverse opening order.";
                    return false;
                }
                current = scopes.Pop().Style;
                return true;
            }
            int separator = token.IndexOf('=');
            string name = separator < 0 ? token : token.Substring(0, separator);
            string argument = separator < 0 ? string.Empty : token.Substring(separator + 1);
            DialogueGlyphStyle next = current;
            switch (name)
            {
                case "shake":
                    next.Shake = true;
                    break;
                case "wave":
                    next.Wave = true;
                    break;
                case "slam":
                    next.Slam = true;
                    break;
                case "rainbow":
                    next.Rainbow = true;
                    break;
                case "color":
                    if (!DialogueNamedColors.TryFind(settings.Colors, argument, out next.Color))
                    {
                        warning = "Choose a named color from Text Colors: " + argument;
                        return false;
                    }
                    next.Colored = true;
                    break;
                case "rotate":
                    float degrees = settings.RotationDegrees;
                    if (separator >= 0 && (!float.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture, out degrees)
                        || !float.IsFinite(degrees) || degrees < -180f || degrees > 180f))
                    {
                        warning = "Rotation needs degrees between -180 and 180. Use a decimal point.";
                        return false;
                    }
                    next.Rotation += degrees;
                    next.RotationGroup = ++rotationGroup;
                    break;
                case "speed":
                case "pause":
                    if (!float.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture, out float number)
                        || !float.IsFinite(number) || number < 0f || name == "speed" && number <= 0f)
                    {
                        warning = "Speed needs a positive number. Pause needs nonnegative seconds. Use a decimal point.";
                        return false;
                    }
                    if (name == "pause")
                    {
                        pause += number;
                        return true;
                    }
                    next.Speed = number;
                    break;
                default:
                    return false;
            }
            if (separator >= 0 && name is not ("color" or "speed" or "rotate"))
                return false;
            scopes.Push(new Scope(name, current));
            current = next;
            return true;
        }

        #endregion

        #endregion
    }
}
