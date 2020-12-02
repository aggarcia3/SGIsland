using UnityEngine;
using System;

namespace Scripts.Util
{
    /// <summary>
    /// Provides some of the operations of <see cref="Mathf"/>, but with higher performance.
    /// </summary>
    public sealed class FastMathf
    {
        /// <summary>
        /// Two times PI.
        /// </summary>
        public static readonly float PI_2 = (float)(Math.PI * 2);
        /// <summary>
        /// Half PI.
        /// </summary>
        public static readonly float HALF_PI = (float)(Math.PI / 2);

        /// <summary>
        /// Computes the absolute value of a floating point number.
        /// </summary>
        /// <param name="x">The floating point to get its absolute value.</param>
        /// <returns>The absolute value of <paramref name="x"/>.</returns>
        public static float Abs(float x)
        {
            // Discard sign bit of IEEE 754 representation
            return (float)BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(x) & 0x7FFFFFFFFFFFFFFF);
        }

        /// <summary>
        /// Computes the floor function for a double value, and casts the result to an integer.
        /// </summary>
        /// <param name="x">The double value to compute its floor function result.</param>
        /// <returns>The result of the floor function for the double value
        /// (i.e. the next integer that is less or equal than the specified double value).</returns>
        public static int FloorToInt(double x)
        {
            // Shift negative values to positive so we get the rounding behavior
            // we want for negative values
            return (int)((uint)(x + 2147483648) - 2147483648);
        }
    }
}
