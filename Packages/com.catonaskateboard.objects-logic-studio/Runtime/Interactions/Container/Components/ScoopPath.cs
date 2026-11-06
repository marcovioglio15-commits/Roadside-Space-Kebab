using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Builds fixed scoop keyframes and a preview of the return to Grab.</summary>
    internal sealed class ScoopPath
    {
        #region State

        internal readonly Pose[] Targets;
        internal readonly ScoopKeyframe[] Frames;
        internal readonly Vector3[] Scales;
        internal readonly int CommitStep;

        #endregion

        #region Methods

        #region Preparation

        /// <summary>Resolves every relative offset once before the first movement segment.</summary>
        /// <param name="settings">Validated optional sequence.</param>
        /// <param name="initial">Held object's starting world pose.</param>
        /// <param name="dispenser">Supply pivot used by the first relative target.</param>
        /// <param name="carry">Current grab pose for independent keyframes and the return preview.</param>
        /// <param name="initialScale">Visual branch's original local scale.</param>
        internal ScoopPath(ScoopAnimationSettings settings, Pose initial, Pose dispenser, Pose carry, Vector3 initialScale)
        {
            int approach = settings.Mode == ScoopAnimationMode.RelativeChain ? 1 : 0;
            Frames = new ScoopKeyframe[settings.Enabled ? settings.Keyframes.Length + approach : 0];
            if (settings.Enabled)
            {
                // The automatic approach precedes every optional relative keyframe.
                if (approach > 0)
                    Frames[0] = settings.Approach;
                System.Array.Copy(settings.Keyframes, 0, Frames, approach, settings.Keyframes.Length);
            }
            CommitStep = Frames.Length - 1;
            Targets = new Pose[settings.Enabled ? Frames.Length + (settings.ReturnToStart ? 1 : 0) : 0];
            Scales = new Vector3[Targets.Length];
            Pose previous = initial;
            Vector3 scale = initialScale;
            // Later chain offsets follow the complete preceding target, including its optional rotation.
            for (int index = 0; index < Frames.Length; index++)
            {
                ScoopKeyframe frame = Frames[index];
                Pose reference = settings.Mode == ScoopAnimationMode.RelativeChain ? index == 0 ? dispenser : previous
                    : frame.Space switch { ScoopPoseSpace.Initial => initial, ScoopPoseSpace.Carry => carry, _ => dispenser };
                previous = new Pose(reference.position + reference.rotation * frame.Position,
                    frame.Rotate ? reference.rotation * Quaternion.Euler(frame.Rotation) : previous.rotation);
                if (frame.Resize)
                    scale = frame.Scale;
                Targets[index] = previous;
                Scales[index] = scale;
                if (frame.Id == settings.FillAfter)
                    CommitStep = index;
            }
            if (settings.Enabled && settings.ReturnToStart)
            {
                Targets[Targets.Length - 1] = carry;
                Scales[Scales.Length - 1] = initialScale;
            }
        }

        #endregion

        #endregion
    }
}
