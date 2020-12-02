using Scripts.Util;
using UnityEngine;

namespace NoiseGenerator.Worley
{
    /// <summary>
    /// Computes Worley noise, as defined in the "Texturing & Modeling – A Procedural Approach" (3rd Edition) book by Steven Worley,
    /// but repeating points to provide a seamless result, with a period of 1.
    /// </summary>
    [CreateAssetMenu(fileName = "PeriodicWorley2DNoiseGenerator", menuName = "Noise Generators/2D/Periodic Worley")]
    public class PeriodicWorley2DNoiseGenerator : Abstract2DCoherentPeriodicNoiseGenerator
    {
        /// <summary>
        /// Constant from "Texturing & Modeling – A Procedural Approach" (3rd Edition), p. 151,
        /// which makes the mean of the Poisson distribution at F0 equal to 1.
        /// </summary>
        private const float DensityAdjustment = 0.398150f;

        /// <summary>
        /// LUT table copied from "Texturing & Modeling – A Procedural Approach" (3rd Edition), p. 150.
        /// See the book for more insight about how it was computed.
        /// </summary>
        private static readonly uint[] CellPointCount = {
            4, 3, 1, 1, 1, 2, 4, 2, 2, 2, 5, 1, 0, 2, 1, 2,
            2, 0, 4, 3, 2, 1, 2, 1, 3, 2, 2, 4, 2, 2, 5, 1,
            2, 3, 2, 2, 2, 2, 2, 3, 2, 4, 2, 5, 3, 2, 2, 2,
            5, 3, 3, 5, 2, 1, 3, 3, 4, 4, 2, 3, 0, 4, 2, 2,
            2, 1, 3, 2, 2, 2, 3, 3, 3, 1, 2, 0, 2, 1, 1, 2,
            2, 2, 2, 5, 3, 2, 3, 2, 3, 2, 2, 1, 0, 2, 1, 1,
            2, 1, 2, 2, 1, 3, 4, 2, 2, 2, 5, 4, 2, 4, 2, 2,
            5, 4, 3, 2, 2, 5, 4, 3, 3, 3, 5, 2, 2, 2, 2, 2,
            3, 1, 1, 4, 2, 1, 3, 3, 4, 3, 2, 4, 3, 3, 3, 4,
            5, 1, 4, 2, 4, 3, 1, 2, 3, 5, 3, 2, 1, 3, 1, 3,
            3, 3, 2, 3, 1, 5, 5, 4, 2, 2, 4, 1, 3, 4, 1, 5,
            3, 3, 5, 3, 4, 3, 2, 2, 1, 1, 1, 1, 1, 2, 4, 5,
            4, 5, 4, 2, 1, 5, 1, 1, 2, 3, 3, 3, 2, 5, 2, 3,
            3, 2, 0, 2, 1, 1, 4, 2, 1, 3, 2, 1, 2, 2, 3, 2,
            5, 5, 3, 4, 5, 5, 2, 4, 4, 5, 3, 2, 2, 2, 1, 4,
            2, 3, 3, 4, 2, 5, 4, 2, 4, 2, 2, 2, 4, 5, 3, 2
        };

        /// <inheritdoc/>
        /// <remarks>The returned value is in the range [0, 1].</remarks>
        public override float Noise2D(long seed, float x, float y)
        {
            // This is an implementation of a simpler version of the algorithm
            // explained in the book, that just returns the distance to the
            // closest feature point (F1)
            float smallestFeaturePointDistance = float.PositiveInfinity;

            // The seed is intentionally initialized with the same value for every cell
            // to achieve the desired seamless effect. It makes the noise periodic, with
            // period 1
            uint initialCellSeed = (uint)seed;
            uint featurePointCount = CellPointCount[initialCellSeed >> 24];

            float adjustedX = DensityAdjustment * x, adjustedY = DensityAdjustment * y;
            int cellX = FastMathf.FloorToInt(adjustedX), cellY = FastMathf.FloorToInt(adjustedY);

            for (int cellOffsetX = -1; cellOffsetX <= 1; ++cellOffsetX)
            {
                for (int cellOffsetY = -1; cellOffsetY <= 1; ++cellOffsetY)
                {
                    uint cellSeed = initialCellSeed;

                    for (uint i = 0; i < featurePointCount; ++i)
                    {
                        // Calculate delta of feature point from the square position
                        float dx = (cellSeed + 0.5f) * (1.0f / 4294967296.0f);
                        cellSeed = 1402024253 * cellSeed + 586950981; // Move to the next PRNG state

                        float dy = (cellSeed + 0.5f) * (1.0f / 4294967296.0f);
                        cellSeed = 1402024253 * cellSeed + 586950981;

                        float distance = Distance2D(cellX + cellOffsetX + dx - adjustedX, cellY + cellOffsetY + dy - adjustedY);
                        if (distance < smallestFeaturePointDistance)
                        {
                            smallestFeaturePointDistance = distance;
                        }

                        cellSeed = 1402024253 * cellSeed + 586950981;
                    }
                }
            }

            if (float.IsInfinity(smallestFeaturePointDistance))
            {
                // This may happen if there are no feature points
                return 0;
            }
            else
            {
                // Normal case. Due to the period of this noise,
                // this distance is at most 0.5, so we multiply by 2
                return StandarizeDistance2D(smallestFeaturePointDistance) * 2;
            }
        }

        /// <summary>
        /// Computes a distance metric between two 2D points from the individual Euclidean distances on each axis.
        /// This metric is only meaningful when comparing distances between points; when the actual magnitude
        /// </summary>
        /// <param name="xDistance">The Euclidean distance in the X axis.</param>
        /// <param name="yDistance">The Euclidean distance in the Y axis.</param>
        /// <returns>The described distance metric.</returns>
        /// <remarks>This implementation returns the squared Euclidean distance.</remarks>
        protected float Distance2D(float xDistance, float yDistance)
        {
            return xDistance * xDistance + yDistance * yDistance;
        }

        /// <summary>
        /// Standarizes a distance returned by <see cref="Distance2D(float, float)"/>, such that the actual magnitude
        /// of the distance is returned.
        /// </summary>
        /// <param name="distance">The distance returned by the mentioned method.</param>
        /// <returns>The standarized distance metric.</returns>
        /// <remarks>This implementation returns the square root of the specified squared Euclidean distance.</remarks>
        protected float StandarizeDistance2D(float distance)
        {
            return Mathf.Sqrt(distance);
        }
    }
}
