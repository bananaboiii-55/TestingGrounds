// ViewMatrix.cs
using System;
using System.Numerics;

namespace WallhackandAimbotCombinedTest
{
    public struct ViewMatrix
    {
        public float m11, m12, m13, m14;
        public float m21, m22, m23, m24;
        public float m31, m32, m33, m34;
        public float m41, m42, m43, m44;

        // helper to convert to float[16] in the same layout used by Calculate.WorldToScreen
        public float[] ToArray()
        {
            return new float[]
            {
                m11, m12, m13, m14,
                m21, m22, m23, m24,
                m31, m32, m33, m34,
                m41, m42, m43, m44
            };
        }
    }
}
