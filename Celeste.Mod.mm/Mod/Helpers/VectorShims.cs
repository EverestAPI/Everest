using Microsoft.Xna.Framework;
using MonoMod;
using System;

namespace Celeste.Mod.Helpers {
    // Shim some Vector2/3/4 methods because they execute with more precision than they should on .NET Framework
    // To preserve compatibilty, we need to force these methods to execute with higher precision as well
    public static class VectorShims {

        // DON'T. TOUCH. THIS.
        // For the sake of your own sanity, only mess with the below code when forced to at gunpoint
        // If you still do, run the full 100% and any% TASes afterwards
        // Don't be surprised if stuff falls apart, I've warned you
        // Note that the only way to reliably determine the correct behaviour here is to inspect the x86 assembly code emitted by the .NET Framework JIT
        // (everything also depends on if the assembly was compiled in Debug/Release mode - there's your XNA/FNA desyncs)

        public const string Vector2FName = "Microsoft.Xna.Framework.Vector2";
        public const string Vector3FName = "Microsoft.Xna.Framework.Vector3";
        public const string Vector4FName = "Microsoft.Xna.Framework.Vector4";

#region Vector2 Shims
        [MonoModLinkFrom($"System.Single {Vector2FName}::Length()")]
        public static float Length(ref Vector2 v) => (float) Math.Sqrt((double) v.X * (double) v.X + (double) v.Y * (double) v.Y);

        [MonoModLinkFrom($"System.Single {Vector2FName}::LengthSquared()")]
        public static float LengthSquared(ref Vector2 v) => (float) ((double) v.X * (double) v.X + (double) v.Y * (double) v.Y);

        [MonoModLinkFrom($"System.Void {Vector2FName}::Normalize()")]
        public static void Normalize(ref Vector2 v) {
            float invLen = (float) (1.0 / (float) Math.Sqrt((double) v.X * (double) v.X + (double) v.Y * (double) v.Y));
            v.X *= invLen;
            v.Y *= invLen;
        }

        [MonoModLinkFrom($"{Vector2FName} {Vector2FName}::Normalize({Vector2FName})")]
        public static Vector2 Normalize(Vector2 v) {
            Normalize(ref v);
            return v;
        }

        [MonoModLinkFrom($"System.Void {Vector2FName}::Normalize({Vector2FName}&,{Vector2FName}&)")]
        public static void Normalize(ref Vector2 v, out Vector2 o) {
            o = v;
            Normalize(ref o);
        }

        [MonoModLinkFrom($"System.Single {Vector2FName}::Distance({Vector2FName},{Vector2FName})")]
        public static float Distance(Vector2 a, Vector2 b) {
            double xD = (float) ((double) a.X - (double) b.X), yD = (float) ((double) a.Y - (double) b.Y);
            return (float) Math.Sqrt(xD*xD + yD*yD);
        }

        [MonoModLinkFrom($"System.Void {Vector2FName}::Distance({Vector2FName}&,{Vector2FName}&,System.Single&)")]
        public static void Distance(ref Vector2 a, ref Vector2 b, out float r) => r = Distance(a, b);

        [MonoModLinkFrom($"System.Single {Vector2FName}::DistanceSquared({Vector2FName},{Vector2FName})")]
        public static float DistanceSquared(Vector2 a, Vector2 b) {
            double xD = (float) ((double) a.X - (double) b.X), yD = (float) ((double) a.Y - (double) b.Y);
            return (float) (xD*xD + yD*yD);
        }

        [MonoModLinkFrom($"System.Void {Vector2FName}::DistanceSquared({Vector2FName}&,{Vector2FName}&,System.Single&)")]
        public static void DistanceSquared(ref Vector2 a, ref Vector2 b, out float r) => r = DistanceSquared(a, b);
#endregion

#region Vector3 Shims
        [MonoModLinkFrom($"System.Single {Vector3FName}::Length()")]
        public static float Length(ref Vector3 v) => (float) Math.Sqrt((double) v.X * (double) v.X + (double) v.Y * (double) v.Y + (double) v.Z * (double) v.Z);

        [MonoModLinkFrom($"System.Single {Vector3FName}::LengthSquared()")]
        public static float LengthSquared(ref Vector3 v) => (float) ((double) v.X * (double) v.X + (double) v.Y * (double) v.Y + (double) v.Z * (double) v.Z);

        [MonoModLinkFrom($"System.Void {Vector3FName}::Normalize()")]
        public static void Normalize(ref Vector3 v) {
            float invLen = (float) (1.0 / (float) Math.Sqrt((double) v.X * (double) v.X + (double) v.Y * (double) v.Y + (double) v.Z * (double) v.Z));
            v.X *= invLen;
            v.Y *= invLen;
            v.Z *= invLen;
        }

        [MonoModLinkFrom($"{Vector3FName} {Vector3FName}::Normalize({Vector3FName})")]
        public static Vector3 Normalize(Vector3 v) {
            Normalize(ref v);
            return v;
        }

        [MonoModLinkFrom($"System.Void {Vector3FName}::Normalize({Vector3FName}&,{Vector3FName}&)")]
        public static void Normalize(ref Vector3 v, out Vector3 o) {
            o = v;
            Normalize(ref o);
        }

        [MonoModLinkFrom($"System.Single {Vector3FName}::Distance({Vector3FName},{Vector3FName})")]
        public static float Distance(Vector3 a, Vector3 b) {
            double xD = (float) ((double) a.X - (double) b.X), yD = (float) ((double) a.Y - (double) b.Y), zD = (float) ((double) a.Z - (double) b.Z);
            return (float) Math.Sqrt(xD*xD + yD*yD + zD*zD);
        }

        [MonoModLinkFrom($"System.Void {Vector3FName}::Distance({Vector3FName}&,{Vector3FName}&,System.Single&)")]
        public static void Distance(ref Vector3 a, ref Vector3 b, out float r) => r = Distance(a, b);

        [MonoModLinkFrom($"System.Single {Vector3FName}::DistanceSquared({Vector3FName},{Vector3FName})")]
        public static float DistanceSquared(Vector3 a, Vector3 b) {
            double xD = (float) ((double) a.X - (double) b.X), yD =(float) ((double) a.Y - (double) b.Y), zD = (float) ((double) a.Z - (double) b.Z);
            return (float) (xD*xD + yD*yD + zD*zD);
        }

        [MonoModLinkFrom($"System.Void {Vector3FName}::DistanceSquared({Vector3FName}&,{Vector3FName}&,System.Single&)")]
        public static void DistanceSquared(ref Vector3 a, ref Vector3 b, out float r) => r = DistanceSquared(a, b);
#endregion

#region Vector4 Shims
        [MonoModLinkFrom($"System.Single {Vector4FName}::Length()")]
        public static float Length(ref Vector4 v) => (float) Math.Sqrt((double) v.X * (double) v.X + (double) v.Y * (double) v.Y + (double) v.Z * (double) v.Z + (double) v.W * (double) v.W);

        [MonoModLinkFrom($"System.Single {Vector4FName}::LengthSquared()")]
        public static float LengthSquared(ref Vector4 v) => (float) ((double) v.X * (double) v.X + (double) v.Y * (double) v.Y + (double) v.Z * (double) v.Z + (double) v.W * (double) v.W);

        [MonoModLinkFrom($"System.Void {Vector4FName}::Normalize()")]
        public static void Normalize(ref Vector4 v) {
            float invLen = (float) (1.0 / (float) Math.Sqrt((double) v.X * (double) v.X + (double) v.Y * (double) v.Y + (double) v.Z * (double) v.Z + (double) v.W * (double) v.W));
            v.X *= invLen;
            v.Y *= invLen;
            v.Z *= invLen;
            v.W *= invLen;
        }

        [MonoModLinkFrom($"{Vector4FName} {Vector4FName}::Normalize({Vector4FName})")]
        public static Vector4 Normalize(Vector4 v) {
            Normalize(ref v);
            return v;
        }

        [MonoModLinkFrom($"System.Void {Vector4FName}::Normalize({Vector4FName}&,{Vector4FName}&)")]
        public static void Normalize(ref Vector4 v, out Vector4 o) {
            o = v;
            Normalize(ref o);
        }

        [MonoModLinkFrom($"System.Single {Vector4FName}::Distance({Vector4FName},{Vector4FName})")]
        public static float Distance(Vector4 a, Vector4 b) {
            double xD = (float) ((double) a.X - (double) b.X), yD = (float) ((double) a.Y - (double) b.Y), zD = (float) ((double) a.Z - (double) b.Z), wD = (float) ((double) a.W - (double) b.W);
            return (float) Math.Sqrt(xD*xD + yD*yD + zD*zD + wD*wD);
        }

        [MonoModLinkFrom($"System.Void {Vector4FName}::Distance({Vector4FName}&,{Vector4FName}&,System.Single&)")]
        public static void Distance(ref Vector4 a, ref Vector4 b, out float r) => r = Distance(a, b);

        [MonoModLinkFrom($"System.Single {Vector4FName}::DistanceSquared({Vector4FName},{Vector4FName})")]
        public static float DistanceSquared(Vector4 a, Vector4 b) {
            double xD = (float) ((double) a.X - (double) b.X), yD = (float) ((double) a.Y - (double) b.Y), zD = (float) ((double) a.Z - (double) b.Z), wD = (float) ((double) a.W - (double) b.W);
            return (float) (xD*xD + yD*yD + zD*zD + wD*wD);
        }

        [MonoModLinkFrom($"System.Void {Vector4FName}::DistanceSquared({Vector4FName}&,{Vector4FName}&,System.Single&)")]
        public static void DistanceSquared(ref Vector4 a, ref Vector4 b, out float r) => r = DistanceSquared(a, b);
#endregion

    }
}