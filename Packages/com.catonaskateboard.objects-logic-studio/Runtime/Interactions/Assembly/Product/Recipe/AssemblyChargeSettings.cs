using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Configures finalization independently from ingredient insertion and product pickup.</summary>
    [Serializable]
    public sealed class AssemblyChargeSettings
    {
        #region Fields

        [Header("Prolonged Assembly")]
        [Tooltip("Button held to finalize a ready recipe. Resolved against the player's private Input Actions asset.")]
        public InputActionReference Action;
        [Tooltip("Reach and aiming required throughout charging, independently of the product's Grab settings.")]
        public TransferTargetSettings Target = new TransferTargetSettings();
        [Tooltip("Scaled seconds of uninterrupted holding required before finalization. Losing the target, input or recipe cancels progress.")]
        [Range(0.1f, 10f)]
        public float Duration = 1.5f;
        [Tooltip("After reaching full charge, wait for button release. An early release cancels the attempt.")]
        public bool CompleteOnRelease;
        [Tooltip("On finalization, also execute an available product Grab using the same action or physical button. Other Grab bindings require a separate press.")]
        public bool GrabOnComplete = true;

        [Header("Charge HUD")]
        [Tooltip("Show the observer's authored charge indicator while this product is being assembled.")]
        public bool ShowHud = true;
        [Tooltip("Optional center icon. An empty reference uses the built-in completion symbol; the progress ring remains visible.")]
        public Sprite Icon;
        [Tooltip("Color of the center icon.")]
        public Color IconColor = Color.white;
        [Tooltip("Color of the moving progress arc.")]
        public Color RingColor = new Color(1f, 0.75f, 0.2f, 1f);
        [Tooltip("Color of the full circular track beneath the progress arc.")]
        public Color TrackColor = new Color(0f, 0f, 0f, 0.65f);
        [Tooltip("Diameter in pixels at the HUD reference resolution.")]
        [Range(24f, 192f)]
        public float Size = 64f;
        [Tooltip("Ring thickness as a fraction of its outer radius.")]
        [Range(0.02f, 0.4f)]
        public float Thickness = 0.12f;
        [Tooltip("Horizontal distance from the center of the gameplay viewport, in reference pixels.")]
        [Range(-600f, 600f)]
        public float OffsetX;
        [Tooltip("Vertical distance from the center of the gameplay viewport, in reference pixels.")]
        [Range(-400f, 400f)]
        public float OffsetY = -80f;
        [Tooltip("Empty the ring while charging instead of filling it.")]
        public bool Drain;
        [Tooltip("Advance the arc clockwise from the top of the circle.")]
        public bool Clockwise = true;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks active charge settings without replacing invalid authored values.</summary>
        /// <param name="warning">Receives the first missing input or invalid active value.</param>
        /// <returns>True when input, timing, targeting and enabled presentation are usable.</returns>
        public bool TryValidate(out string warning)
        {
            // Input maps remain owned by PlayerInput; this configuration only selects a Button.
            warning = "Prolonged Assembly requires a Button action and a positive finite duration.";
            if (Action == null || Action.action == null || Action.action.type != InputActionType.Button
                || !InteractionValues.Positive(Duration))
                return false;
            warning = "Configure prolonged assembly targeting.";
            if (Target == null || !Target.TryValidate(out warning))
                return false;
            warning = "Charge HUD needs a positive size, thickness between zero and one, finite offsets and colors.";
            if (ShowHud && (!InteractionValues.Positive(Size) || !float.IsFinite(Thickness) || Thickness <= 0f || Thickness >= 1f
                || !float.IsFinite(OffsetX) || !float.IsFinite(OffsetY) || !Finite(IconColor) || !Finite(RingColor) || !Finite(TrackColor)))
                return false;
            warning = string.Empty;
            return true;
        }

        /// <summary>Rejects nonfinite UI channels before they reach mesh vertices.</summary>
        /// <param name="color">Authored UI tint.</param>
        /// <returns>True when each color channel is finite.</returns>
        private static bool Finite(Color color)
        {
            return float.IsFinite(color.r) && float.IsFinite(color.g) && float.IsFinite(color.b) && float.IsFinite(color.a);
        }

        #endregion

        #endregion
    }
}
