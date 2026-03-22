using System;
using System.Collections.Generic;
using System.Numerics;
using Swed64;

public static class Calculate
{
    public static IntPtr ResolveBoneArray(IntPtr sceneNode, Swed swed, int m_modelState)
    {
        if (sceneNode == IntPtr.Zero)
            return IntPtr.Zero;

        IntPtr direct = swed.ReadPointer(sceneNode, m_modelState + 0x80);
        if (direct != IntPtr.Zero)
            return direct;

        IntPtr skeleton = swed.ReadPointer(sceneNode, 0x80);
        if (skeleton != IntPtr.Zero)
        {
            IntPtr arr = swed.ReadPointer(skeleton, m_modelState + 0x80);
            if (arr != IntPtr.Zero)
                return arr;
        }

        return IntPtr.Zero;
    }

    public static Vector2 WorldToScreen(float[] m, Vector3 world, Vector2 screenSize)
    {
        if (m == null || m.Length < 16)
            return new Vector2(-1, -1);

        // Row-major 4×4 (matches typical CS2 client view-projection from ReadMatrix).
        float x = world.X, y = world.Y, z = world.Z;
        float clipX = m[0] * x + m[1] * y + m[2] * z + m[3];
        float clipY = m[4] * x + m[5] * y + m[6] * z + m[7];
        float clipW = m[12] * x + m[13] * y + m[14] * z + m[15];

        // Behind camera or on the plane: w must be positive or you get mirrored / junk screen coords.
        if (clipW < 0.001f)
            return new Vector2(-1, -1);

        float invW = 1f / clipW;
        float ndcX = clipX * invW;
        float ndcY = clipY * invW;

        // Outside view frustum (looking away to the side / not in front of you).
        const float ndcLimit = 1.02f;
        if (ndcX < -ndcLimit || ndcX > ndcLimit || ndcY < -ndcLimit || ndcY > ndcLimit)
            return new Vector2(-1, -1);

        float screenX = (ndcX + 1f) * 0.5f * screenSize.X;
        float screenY = (1f - ndcY) * 0.5f * screenSize.Y;
        return new Vector2(screenX, screenY);
    }

    public static Vector2 CalculateAngles(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float yaw = MathF.Atan2(d.Y, d.X) * (180f / MathF.PI);
        float len2d = MathF.Sqrt(d.X * d.X + d.Y * d.Y);
        float pitch = -MathF.Atan2(d.Z, len2d) * (180f / MathF.PI);
        return new Vector2(yaw, pitch);
    }

    public static float AngleDelta(float fromDeg, float toDeg)
    {
        float d = (toDeg - fromDeg) % 360f;
        if (d > 180f) d -= 360f;
        if (d < -180f) d += 360f;
        return d;
    }

    public static float LerpAngleDegrees(float fromDeg, float toDeg, float t)
    {
        return fromDeg + AngleDelta(fromDeg, toDeg) * t;
    }

    public static float LerpPitch(float fromDeg, float toDeg, float t)
    {
        float n = fromDeg + (toDeg - fromDeg) * t;
        return Math.Clamp(n, -89f, 89f);
    }

    public static List<Vector3> ReadBones(IntPtr boneAddress, Swed swed, int maxBones = 64)
    {
        var bones = new List<Vector3>();

        if (boneAddress == IntPtr.Zero || swed == null)
            return bones;

        const int boneStride = 0x20;

        try
        {
            byte[] raw = swed.ReadBytes(boneAddress, maxBones * boneStride);

            for (int i = 0; i < maxBones; i++)
            {
                int o = i * boneStride;
                if (raw.Length < o + 12)
                    break;

                float bx = BitConverter.ToSingle(raw, o + 0);
                float by = BitConverter.ToSingle(raw, o + 4);
                float bz = BitConverter.ToSingle(raw, o + 8);
                bones.Add(new Vector3(bx, by, bz));
            }
        }
        catch
        {
        }

        return bones;
    }
}
