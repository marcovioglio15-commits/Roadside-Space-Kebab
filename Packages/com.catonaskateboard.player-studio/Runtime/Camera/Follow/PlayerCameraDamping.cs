using System;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Computes exponential damping weights for a target moving between two frame samples.</summary>
    internal static class PlayerCameraDamping
    {
        #region Methods

        #region Integration

        /// <summary>Integrates a linear target trajectory without making the response depend on frame partitioning.</summary>
        /// <param name="interval">Positive elapsed time multiplied by response, or divided by response time.</param>
        /// <param name="targetWeight">Receives the weight of the previous target relative to the current output.</param>
        /// <param name="motionWeight">Receives the weight of target displacement during this interval.</param>
        public static void GetWeights(double interval, out float targetWeight, out float motionWeight)
        {
            // The series preserves small changes when subtracting nearly equal exponential terms.
            if (interval < 0.001d)
            {
                targetWeight = (float)(interval * (1d - interval * (0.5d - interval / 6d)));
                motionWeight = (float)(interval * (0.5d - interval * (1d / 6d - interval / 24d)));
                return;
            }

            // Evaluate in double precision before returning weights for Unity's float vectors.
            double response = 1d - Math.Exp(-interval);
            targetWeight = (float)response;
            motionWeight = (float)(1d - response / interval);
        }

        #endregion

        #endregion
    }
}
