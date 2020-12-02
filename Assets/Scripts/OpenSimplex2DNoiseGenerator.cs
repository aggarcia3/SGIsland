using Scripts.Util;
using System;
using UnityEngine;

namespace NoiseGenerator.OpenSimplex
{
    /// Computes OpenSimplex2 2D coherent noise. This is an improved version of Perlin noise,
    /// which is useful for synthesizing natural-looking terrain and textures.
    ///
    /// The code of this class was adapted from <see href="https://github.com/KdotJPG/OpenSimplex2"/>,
    /// modified to improve performance and code quality.
    [CreateAssetMenu(fileName = "OpenSimplex2DNoiseGenerator", menuName = "Noise Generators/2D/OpenSimplex")]
    public sealed class OpenSimplex2DNoiseGenerator : Abstract2DCoherentNoiseGenerator
    {
        private const short NumberOfPermutationsMask = 2047; // Actual number of permutations is one plus this

        private static readonly SimplexLattice2DPoint[] latticePointLUT = new SimplexLattice2DPoint[8 * 4];
        private static readonly SimplexPoint2DGradients[] gradients = new SimplexPoint2DGradients[NumberOfPermutationsMask + 1];

        private short[] permutations = new short[NumberOfPermutationsMask + 1];
        private SimplexPoint2DGradients[] permutatedGradients = new SimplexPoint2DGradients[NumberOfPermutationsMask + 1];
        private short[] tempPermutationSource = new short[NumberOfPermutationsMask + 1];

        private long? currentSeed = null;

        static OpenSimplex2DNoiseGenerator()
        {
            // Initialize seed-independent data

            // Gradients
            SimplexPoint2DGradients[] baseGradients = {
            new SimplexPoint2DGradients{ Dx = 0.130526192220052f, Dy = 0.99144486137381f },
            new SimplexPoint2DGradients{ Dx = 0.38268343236509f, Dy = 0.923879532511287f },
            new SimplexPoint2DGradients{ Dx = 0.608761429008721f, Dy = 0.793353340291235f },
            new SimplexPoint2DGradients{ Dx = 0.793353340291235f, Dy = 0.608761429008721f },
            new SimplexPoint2DGradients{ Dx = 0.923879532511287f, Dy = 0.38268343236509f },
            new SimplexPoint2DGradients{ Dx = 0.99144486137381f, Dy = 0.130526192220051f },
            new SimplexPoint2DGradients{ Dx = 0.99144486137381f, Dy = -0.130526192220051f },
            new SimplexPoint2DGradients{ Dx = 0.923879532511287f, Dy = -0.38268343236509f },
            new SimplexPoint2DGradients{ Dx = 0.793353340291235f, Dy = -0.60876142900872f },
            new SimplexPoint2DGradients{ Dx = 0.608761429008721f, Dy = -0.793353340291235f },
            new SimplexPoint2DGradients{ Dx = 0.38268343236509f, Dy = -0.923879532511287f },
            new SimplexPoint2DGradients{ Dx = 0.130526192220052f, Dy = -0.99144486137381f },
            new SimplexPoint2DGradients{ Dx = -0.130526192220052f, Dy = -0.99144486137381f },
            new SimplexPoint2DGradients{ Dx = -0.38268343236509f, Dy = -0.923879532511287f },
            new SimplexPoint2DGradients{ Dx = -0.608761429008721f, Dy = -0.793353340291235f },
            new SimplexPoint2DGradients{ Dx = -0.793353340291235f, Dy = -0.608761429008721f },
            new SimplexPoint2DGradients{ Dx = -0.923879532511287f, Dy = -0.38268343236509f },
            new SimplexPoint2DGradients{ Dx = -0.99144486137381f, Dy = -0.130526192220052f },
            new SimplexPoint2DGradients{ Dx = -0.99144486137381f, Dy = 0.130526192220051f },
            new SimplexPoint2DGradients{ Dx = -0.923879532511287f, Dy = 0.38268343236509f },
            new SimplexPoint2DGradients{ Dx = -0.793353340291235f, Dy = 0.608761429008721f },
            new SimplexPoint2DGradients{ Dx = -0.608761429008721f, Dy = 0.793353340291235f },
            new SimplexPoint2DGradients{ Dx = -0.38268343236509f, Dy = 0.923879532511287f },
            new SimplexPoint2DGradients{ Dx = -0.130526192220052f, Dy = 0.99144486137381f }
        };
            for (int i = 0; i < baseGradients.Length; ++i)
            {
                baseGradients[i].Dx /= 0.05481866495625118f;
                baseGradients[i].Dy /= 0.05481866495625118f;
            }
            for (int i = 0; i < gradients.Length; i++)
            {
                gradients[i] = baseGradients[i % baseGradients.Length];
            }

            // Lookup table for lattice points
            for (int i = 0; i < 8; ++i)
            {
                int i1, j1, i2, j2;

                if ((i & 1) == 0)
                {
                    if ((i & 2) == 0) { i1 = -1; j1 = 0; } else { i1 = 1; j1 = 0; }
                    if ((i & 4) == 0) { i2 = 0; j2 = -1; } else { i2 = 0; j2 = 1; }
                }
                else
                {
                    if ((i & 2) != 0) { i1 = 2; j1 = 1; } else { i1 = 0; j1 = 1; }
                    if ((i & 4) != 0) { i2 = 1; j2 = 2; } else { i2 = 1; j2 = 0; }
                }

                latticePointLUT[i * 4 + 0] = new SimplexLattice2DPoint(0, 0);
                latticePointLUT[i * 4 + 1] = new SimplexLattice2DPoint(1, 1);
                latticePointLUT[i * 4 + 2] = new SimplexLattice2DPoint(i1, j1);
                latticePointLUT[i * 4 + 3] = new SimplexLattice2DPoint(i2, j2);
            }
        }

        public override float Noise2D(long seed, float x, float y)
        {
            float value = 0;

            // Make sure the data dependent on the seed is okay
            if (!currentSeed.HasValue || currentSeed != seed)
                UpdateSeedPermutations(seed);

            float s = 0.3660254180431365966796875f * (x + y);
            float xs = x + s, ys = y + s;
            int xsb = FastMathf.FloorToInt(x + s), ysb = FastMathf.FloorToInt(y + s);
            float xsi = xs - xsb, ysi = ys - ysb;

            // Index to point list
            int a = (int)(xsi + ysi);
            int index =
                (a << 2) |
                (int)(xsi - ysi / 2 + 1 - a / 2.0) << 3 |
                (int)(ysi - xsi / 2 + 1 - a / 2.0) << 4;

            float ssi = (xsi + ysi) * -0.211324870586395263671875f;
            float xi = xsi + ssi, yi = ysi + ssi;

            // Point contributions
            for (int i = 0; i < 4; ++i)
            {
                try
                {
                    var c = latticePointLUT[index + i];

                    float dx = xi + c.gradient.Dx, dy = yi + c.gradient.Dy;
                    float attn = 2.0f / 3.0f - dx * dx - dy * dy;

                    int pxm = (xsb + c.Xsv) & NumberOfPermutationsMask;
                    int pym = (ysb + c.Ysv) & NumberOfPermutationsMask;
                    var grad = gradients[permutations[pxm] ^ pym];
                    float extrapolation = grad.Dx * dx + grad.Dy * dy;

                    attn *= attn;
                    value += attn * attn * extrapolation;
                }
                catch (IndexOutOfRangeException)
                {
                    // Assume that invalid indexes signal points with
                    // negligible contributions. This happens if attn is < 0
                    // and for very high coordinates
                }
            }

            return value;
        }

        /// <summary>
        /// Updates the OpenSimplex noise permutation tables for the specified seed value.
        /// This method doesn't check whether the permutation tables were already computed for the seed.
        /// <param name="seed">The seed value to generate permutations of.</paramref>
        /// </summary>
        private void UpdateSeedPermutations(long seed)
        {
            for (short i = 0; i < tempPermutationSource.Length; i++)
                tempPermutationSource[i] = i;

            long newSeed = seed;
            for (int i = NumberOfPermutationsMask; i >= 0; i--)
            {
                // Linear congruential PRNG
                newSeed = newSeed * 6364136223846793005L + 1442695040888963407L;

                int r = (int)((newSeed + 31) % (i + 1));
                if (r < 0)
                    r += i + 1;

                permutations[i] = tempPermutationSource[r];
                permutatedGradients[i] = gradients[permutations[i]];
                tempPermutationSource[r] = tempPermutationSource[i];
            }

            currentSeed = seed;
        }

        /// <summary>
        /// Represents a point in a OpenSimplex 2D noise lattice.
        /// </summary>
        private struct SimplexLattice2DPoint
        {
            public int Xsv { get; }
            public int Ysv { get; }
            public SimplexPoint2DGradients gradient { get; }

            public SimplexLattice2DPoint(int xsv, int ysv)
            {
                float ssv = (xsv + ysv) * -0.211324870586395263671875f;
                Xsv = xsv; Ysv = ysv;
                gradient = new SimplexPoint2DGradients
                {
                    Dx = -xsv - ssv,
                    Dy = -ysv - ssv
                };
            }
        }

        /// <summary>
        /// Contains a pair of numbers, each number being the gradient (i.e. slope) of a
        /// mathematical function in a 2D point.
        /// </summary>
        private struct SimplexPoint2DGradients
        {
            public float Dx { get; set; }
            public float Dy { get; set; }
        }
    }
}
