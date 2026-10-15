using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Builds bounded recipe combinations once at extraction and shares names with dialogue selectors.</summary>
    internal static class OrderComboCandidates
    {
        #region Constants

        internal const int MaximumCombinations = 256;

        #endregion
        #region Methods
        #region Enumeration

        /// <summary>Checks the worst-case candidate count before allocating combination arrays.</summary>
        /// <param name="entry">Order with enabled combo settings.</param>
        /// <returns>True when the authored upper bound fits the extraction budget.</returns>
        internal static bool IsBounded(OrderEntry entry)
        {
            long combinations = 1;
            long total = 0;
            int count = entry.Variants.Length;
            for (int size = 1; size <= Mathf.Min(count, entry.Combos.MaximumVariants); size++)
            {
                combinations = combinations * (count - size + 1) / size;
                if (size >= 2)
                    total += combinations;
                if (total > MaximumCombinations)
                    return false;
            }
            return true;
        }

        /// <summary>Enumerates distinct compatible combinations without permutation duplicates.</summary>
        /// <param name="entry">Parent providing variants and maximum combination size.</param>
        /// <returns>Compatible combinations in stable catalog order.</returns>
        internal static IEnumerable<OrderRecipeVariant[]> Enumerate(OrderEntry entry)
        {
            if (!entry.UseRecipeModifiers || entry.Combos == null || !entry.Combos.Enabled || entry.Variants == null || !IsBounded(entry))
                yield break;
            int limit = Mathf.Min(entry.Combos.MaximumVariants, entry.Variants.Length);
            for (int size = 2; size <= limit; size++)
            {
                int[] indices = new int[size];
                for (int index = 0; index < size; index++)
                    indices[index] = index;
                while (true)
                {
                    OrderRecipeVariant[] combo = new OrderRecipeVariant[size];
                    for (int index = 0; index < size; index++)
                        combo[index] = entry.Variants[indices[index]];
                    if (Compatible(combo))
                        yield return combo;
                    int cursor = size - 1;
                    while (cursor >= 0 && indices[cursor] == entry.Variants.Length - size + cursor)
                        cursor--;
                    if (cursor < 0)
                        break;
                    indices[cursor]++;
                    for (int index = cursor + 1; index < size; index++)
                        indices[index] = indices[index - 1] + 1;
                }
            }
        }

        /// <summary>Rejects impossible additive requirements after merging all subtractive filters.</summary>
        /// <param name="combo">Variants included in one proposed recipe.</param>
        /// <returns>True when an ingredient receipt can satisfy every member.</returns>
        private static bool Compatible(OrderRecipeVariant[] combo)
        {
            HashSet<ObjectFlag> excluded = new HashSet<ObjectFlag>();
            foreach (OrderRecipeVariant variant in combo)
            {
                if (variant == null || variant.Flags == null || variant.ExcludedFlags == null)
                    return false;
                excluded.UnionWith(variant.ExcludedFlags);
            }
            foreach (OrderRecipeVariant variant in combo)
            {
                bool possible = variant.Flags.Length == 0;
                foreach (ObjectFlag flag in variant.Flags)
                {
                    if (excluded.Contains(flag) && variant.RequireAllFlags)
                        return false;
                    possible |= !excluded.Contains(flag);
                }
                if (!possible)
                    return false;
            }
            return true;
        }

        #endregion
        #region Policies

        /// <summary>Resolves the effective slot quantity, sampling random policies only for a real spawn.</summary>
        /// <param name="entry">Parent and combination settings.</param>
        /// <param name="combo">Members with optional independent quantities.</param>
        /// <param name="sample">False validates the highest possible quantity without consuming randomness.</param>
        /// <returns>Positive whole-draw slot cost.</returns>
        internal static int Quantity(OrderEntry entry, OrderRecipeVariant[] combo, bool sample)
        {
            if (entry.Combos.QuantityPolicy == OrderComboPolicy.Fixed)
                return entry.Combos.Quantity;
            if (entry.Combos.QuantityPolicy == OrderComboPolicy.RandomVariant && sample)
                return new OrderCandidate(entry, combo[UnityEngine.Random.Range(0, combo.Length)]).Quantity;
            int value = new OrderCandidate(entry, combo[0]).Quantity;
            foreach (OrderRecipeVariant variant in combo)
                value = entry.Combos.QuantityPolicy == OrderComboPolicy.Lowest
                    ? Mathf.Min(value, new OrderCandidate(entry, variant).Quantity) : Mathf.Max(value, new OrderCandidate(entry, variant).Quantity);
            return value;
        }

        /// <summary>Resolves lower required, upper required and maximum draw counts as one consistent policy.</summary>
        /// <param name="entry">Parent defaults and combo override.</param>
        /// <param name="combo">Members supplying effective limits.</param>
        /// <param name="sample">False validates worst-case random choices without sampling.</param>
        /// <returns>Required range followed by the total draw allowance.</returns>
        internal static Vector3Int Limits(OrderEntry entry, OrderRecipeVariant[] combo, bool sample)
        {
            OrderComboSettings settings = entry.Combos;
            if (settings.DrawLimitsPolicy == OrderComboPolicy.Fixed)
                return new Vector3Int(settings.RandomRequiredDraws ? settings.RequiredDrawRange.x : settings.MinimumDraws,
                    settings.RandomRequiredDraws ? settings.RequiredDrawRange.y : settings.MinimumDraws, settings.MaximumDraws);
            if (settings.DrawLimitsPolicy == OrderComboPolicy.RandomVariant && sample)
                return Limits(entry, combo[UnityEngine.Random.Range(0, combo.Length)]);
            Vector3Int result = Limits(entry, combo[0]);
            foreach (OrderRecipeVariant variant in combo)
                result = settings.DrawLimitsPolicy == OrderComboPolicy.Lowest
                    ? Vector3Int.Min(result, Limits(entry, variant)) : Vector3Int.Max(result, Limits(entry, variant));
            return result;
        }

        /// <summary>Reads one member's effective limits, respecting independent variant overrides.</summary>
        /// <param name="entry">Parent allowance for members without an override.</param>
        /// <param name="variant">Combination member.</param>
        /// <returns>Required minimum, required maximum and total maximum.</returns>
        private static Vector3Int Limits(OrderEntry entry, OrderRecipeVariant variant)
        {
            if (variant.UseDrawLimits)
                return new Vector3Int(variant.RandomRequiredDraws ? variant.RequiredDrawRange.x : variant.MinimumDraws,
                    variant.RandomRequiredDraws ? variant.RequiredDrawRange.y : variant.MinimumDraws, variant.MaximumDraws);
            return new Vector3Int(entry.RandomRequiredDraws ? entry.RequiredDrawRange.x : entry.MinimumDraws,
                entry.RandomRequiredDraws ? entry.RequiredDrawRange.y : entry.MinimumDraws, entry.MaximumDraws);
        }

        #endregion
        #region Presentation

        /// <summary>Creates a stable selectable combo name without displaying internal identifiers.</summary>
        /// <param name="entry">Base order name.</param>
        /// <param name="combo">Variants in catalog order.</param>
        /// <returns>Distinct catalog key for this exact combination.</returns>
        internal static string Name(OrderEntry entry, OrderRecipeVariant[] combo)
        {
            string[] names = new string[combo.Length];
            for (int index = 0; index < combo.Length; index++)
                names[index] = combo[index].Name;
            return entry.Name + " [" + string.Join(" + ", names) + "]";
        }

        /// <summary>Formats base and variant text with literal newline support and no orphan separators.</summary>
        /// <param name="entry">Base board text.</param>
        /// <param name="variant">Individual variant, or null.</param>
        /// <param name="combo">Combination members, or null for ordinary candidates.</param>
        /// <returns>Final text frozen into the extracted ticket.</returns>
        internal static string Text(OrderEntry entry, OrderRecipeVariant variant, OrderRecipeVariant[] combo)
        {
            string text = (entry.Text ?? entry.Name).Replace("\\n", "\n");
            if (combo != null)
            {
                foreach (OrderRecipeVariant member in combo)
                    text = Append(text, member.Text);
                return text;
            }
            if (variant == null || string.IsNullOrWhiteSpace(variant.Text))
                return text;
            return variant.AppendToBaseText ? Append(text, variant.Text) : variant.Text.Replace("\\n", "\n");
        }

        /// <summary>Preserves authored leading line breaks when adding a recipe suffix.</summary>
        /// <param name="text">Text already accumulated.</param>
        /// <param name="suffix">Optional variant text, including escaped newlines.</param>
        /// <returns>Combined text without adding a space before a newline.</returns>
        private static string Append(string text, string suffix)
        {
            if (string.IsNullOrWhiteSpace(suffix))
                return text;
            suffix = suffix.Replace("\\n", "\n");
            return text + (suffix.StartsWith("\n", StringComparison.Ordinal) || suffix.StartsWith("\r", StringComparison.Ordinal) ? string.Empty : " ") + suffix;
        }

        #endregion
        #endregion
    }
}
