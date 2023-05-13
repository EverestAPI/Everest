using MonoMod;
using System;

namespace Microsoft.Xna.Framework {
    // Patch some Vector2/3/4 methods because they execute with more precision than they should on .NET Framework
    // To preserve compatibilty, we need to ensure that these methods execute with higher precision as well

    // DON'T. TOUCH. THIS.
    // For the sake of your own sanity, only mess with the below code when forced to at gunpoint
    // If you still do, run the full 100% and any% TASes afterwards
    // Don't be surprised if stuff falls apart, I've warned you
    // Note that the only way to reliably determine the correct behaviour here is to inspect the x86 assembly code emitted by the .NET Framework JIT
    // (everything also depends on if the assembly was compiled in Debug/Release mode - there's your XNA/FNA desyncs)

    [GameDependencyPatch("FNA")]
    struct patch_Vector2 {

        public float X, Y;

        [MonoModReplace]
        public float Length() => (float) Math.Sqrt((double) X * (double) X + (double) Y * (double) Y);

        [MonoModReplace]
        public float LengthSquared() => (float) ((double) X * (double) X + (double) Y * (double) Y);

        [MonoModReplace]
        public void Normalize() {
            double invLen = 1.0 / (float) Math.Sqrt((double) X * (double) X + (double) Y * (double) Y);
            X = (float) (X * invLen);
            Y = (float) (Y * invLen);
        }

        [MonoModReplace]
        public static Vector2 Normalize(Vector2 v) {
            v.Normalize();
            return v;
        }

        [MonoModReplace]
        public static void Normalize(ref Vector2 v, out Vector2 o) {
            o = v;
            o.Normalize();
        }

        [MonoModReplace]
        public static float Distance(Vector2 a, Vector2 b) {
            double xD = (double) a.X - (double) b.X, yD = (double) a.Y - (double) b.Y;
            return (float) Math.Sqrt(xD*xD + yD*yD);
        }

        [MonoModReplace]
        public static void Distance(ref Vector2 a, ref Vector2 b, out float r) => r = Distance(a, b);

        [MonoModReplace]
        public static float DistanceSquared(Vector2 a, Vector2 b) {
            double xD = (double) a.X - (double) b.X, yD = (double) a.Y - (double) b.Y;
            return (float) (xD*xD + yD*yD);
        }

        [MonoModReplace]
        public static void DistanceSquared(ref Vector2 a, ref Vector2 b, out float r) => r = DistanceSquared(a, b);

        [MonoModReplace]
        public static float Dot(Vector2 a, Vector2 b) => (float) ((double) a.X * b.X + (double) a.Y * b.Y);

        [MonoModReplace]
        public static void Dot(ref Vector2 a, ref Vector2 b, out float r) => r = (float) ((double) a.X * b.X + (double) a.Y * b.Y);

        [MonoModReplace]
        public static Vector2 Divide(Vector2 v, float s) => new Vector2(v.X / s, v.Y / s);

        [MonoModReplace]
        public static void Divide(ref Vector2 v, float s, out Vector2 r) {
            r.X = v.X / s;
            r.Y = v.Y / s;
        }

        [MonoModReplace]
        public static Vector2 operator /(patch_Vector2 v, float s) => new Vector2(v.X / s, v.Y / s);

    }

    [GameDependencyPatch("FNA")]
    struct patch_Vector3 {

        public float X, Y, Z;

        [MonoModReplace]
        public float Length() => (float) Math.Sqrt((double) X * (double) X + (double) Y * (double) Y + (double) Z * (double) Z);

        [MonoModReplace]
        public float LengthSquared() => (float) ((double) X * (double) X + (double) Y * (double) Y + (double) Z * (double) Z);

        [MonoModReplace]
        public void Normalize() {
            double invLen = 1.0 / (float) Math.Sqrt((double) X * (double) X + (double) Y * (double) Y + (double) Z * (double) Z);
            X = (float) (X * invLen);
            Y = (float) (Y * invLen);
            Z = (float) (Z * invLen);
        }

        [MonoModReplace]
        public static Vector3 Normalize(Vector3 v) {
            v.Normalize();
            return v;
        }

        [MonoModReplace]
        public static void Normalize(ref Vector3 v, out Vector3 o) {
            o = v;
            o.Normalize();
        }

        [MonoModReplace]
        public static float Distance(Vector3 a, Vector3 b) {
            double xD = (double) a.X - (double) b.X, yD = (double) a.Y - (double) b.Y, zD = (double) a.Z - (double) b.Z;
            return (float) Math.Sqrt(xD*xD + yD*yD + zD*zD);
        }

        [MonoModReplace]
        public static void Distance(ref Vector3 a, ref Vector3 b, out float r) => r = Distance(a, b);

        [MonoModReplace]
        public static float DistanceSquared(Vector3 a, Vector3 b) {
            double xD = (double) a.X - (double) b.X, yD = (double) a.Y - (double) b.Y, zD = (double) a.Z - (double) b.Z;
            return (float) (xD*xD + yD*yD + zD*zD);
        }

        [MonoModReplace]
        public static void DistanceSquared(ref Vector3 a, ref Vector3 b, out float r) => r = DistanceSquared(a, b);

        [MonoModReplace]
        public static float Dot(Vector3 a, Vector3 b) => (float) ((double) a.X * b.X + (double) a.Y * b.Y + (double) a.Z * b.Z);

        [MonoModReplace]
        public static void Dot(ref Vector3 a, ref Vector3 b, out float r) => r = (float) ((double) a.X * b.X + (double) a.Y * b.Y + (double) a.Z * b.Z);

        [MonoModReplace]
        public static Vector3 Divide(Vector3 v, float s) => new Vector3(v.X / s, v.Y / s, v.Z / s);

        [MonoModReplace]
        public static void Divide(ref Vector3 v, float s, out Vector3 r) {
            r.X = v.X / s;
            r.Y = v.Y / s;
            r.Z = v.Z / s;
        }

        [MonoModReplace]
        public static Vector3 operator /(patch_Vector3 v, float s) => new Vector3(v.X / s, v.Y / s, v.Z / s);

    }


    [GameDependencyPatch("FNA")]
    struct patch_Vector4 {

        public float X, Y, Z, W;

        [MonoModReplace]
        public float Length() => (float) Math.Sqrt((double) X * (double) X + (double) Y * (double) Y + (double) Z * (double) Z + (double) W * (double) W);

        [MonoModReplace]
        public float LengthSquared() => (float) ((double) X * (double) X + (double) Y * (double) Y + (double) Z * (double) Z + (double) W * (double) W);

        [MonoModReplace]
        public void Normalize() {
            double invLen = 1.0 / (float) Math.Sqrt((double) X * (double) X + (double) Y * (double) Y + (double) Z * (double) Z + (double) W * (double) W);
            X = (float) (X * invLen);
            Y = (float) (Y * invLen);
            Z = (float) (Z * invLen);
            W = (float) (W * invLen);
        }

        [MonoModReplace]
        public static Vector4 Normalize(Vector4 v) {
            v.Normalize();
            return v;
        }

        [MonoModReplace]
        public static void Normalize(ref Vector4 v, out Vector4 o) {
            o = v;
            o.Normalize();
        }

        [MonoModReplace]
        public static float Distance(Vector4 a, Vector4 b) {
            double xD = (double) a.X - (double) b.X, yD = (double) a.Y - (double) b.Y, zD = (double) a.Z - (double) b.Z, wD = (double) a.W - (double) b.W;
            return (float) Math.Sqrt(xD*xD + yD*yD + zD*zD + wD*wD);
        }

        [MonoModReplace]
        public static void Distance(ref Vector4 a, ref Vector4 b, out float r) => r = Distance(a, b);

        [MonoModReplace]
        public static float DistanceSquared(Vector4 a, Vector4 b) {
            double xD = (double) a.X - (double) b.X, yD = (double) a.Y - (double) b.Y, zD = (double) a.Z - (double) b.Z, wD = (double) a.W - (double) b.W;
            return (float) (xD*xD + yD*yD + zD*zD + wD*wD);
        }

        [MonoModReplace]
        public static void DistanceSquared(ref Vector4 a, ref Vector4 b, out float r) => r = DistanceSquared(a, b);

        [MonoModReplace]
        public static float Dot(Vector4 a, Vector4 b) => (float) ((double) a.X * b.X + (double) a.Y * b.Y + (double) a.Z * b.Z + (double) a.W * b.W);

        [MonoModReplace]
        public static void Dot(ref Vector4 a, ref Vector4 b, out float r) => r = (float) ((double) a.X * b.X + (double) a.Y * b.Y + (double) a.Z * b.Z + (double) a.W * b.W);

        [MonoModReplace]
        public static Vector4 Divide(Vector4 v, float s) => new Vector4(v.X / s, v.Y / s, v.Z / s, v.W / s);

        [MonoModReplace]
        public static void Divide(ref Vector4 v, float s, out Vector4 r) {
            r.X = v.X / s;
            r.Y = v.Y / s;
            r.Z = v.Z / s;
            r.W = v.W / s;
        }

        [MonoModReplace]
        public static Vector4 operator /(patch_Vector4 v, float s) => new Vector4(v.X / s, v.Y / s, v.Z / s, v.W / s);

    }
}