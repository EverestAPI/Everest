using Microsoft.Xna.Framework;
using MonoMod;
using System;

namespace Celeste.Mod.Helpers {
    // Shim some Vector2/3/4 methods because they execute with more precision than they should on .NET Framework
    // To preserve compatibilty, we need to force these methods to execute with higher precision as well
    public static class VectorShims {

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
            double invLen = 1.0 / Math.Sqrt((double) v.X * (double) v.X + (double) v.Y * (double) v.Y);
            v.X = (float) (v.X * invLen);
            v.Y = (float) (v.Y * invLen);
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
#endregion

#region Vector3 Shims
        [MonoModLinkFrom($"System.Single {Vector3FName}::Length()")]
        public static float Length(ref Vector3 v) => (float) Math.Sqrt((double) v.X * (double) v.X + (double) v.Y * (double) v.Y + (double) v.Z * (double) v.Z);

        [MonoModLinkFrom($"System.Single {Vector3FName}::LengthSquared()")]
        public static float LengthSquared(ref Vector3 v) => (float) ((double) v.X * (double) v.X + (double) v.Y * (double) v.Y + (double) v.Z * (double) v.Z);

        [MonoModLinkFrom($"System.Void {Vector3FName}::Normalize()")]
        public static void Normalize(ref Vector3 v) {
            double invLen = 1.0 / Math.Sqrt((double) v.X * (double) v.X + (double) v.Y * (double) v.Y + (double) v.Z * (double) v.Z);
            v.X = (float) (v.X * invLen);
            v.Y = (float) (v.Y * invLen);
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
#endregion

#region Vector4 Shims
        [MonoModLinkFrom($"System.Single {Vector4FName}::Length()")]
        public static float Length(ref Vector4 v) => (float) Math.Sqrt((double) v.X * (double) v.X + (double) v.Y * (double) v.Y + (double) v.Z * (double) v.Z + (double) v.W * (double) v.W);

        [MonoModLinkFrom($"System.Single {Vector4FName}::LengthSquared()")]
        public static float LengthSquared(ref Vector4 v) => (float) ((double) v.X * (double) v.X + (double) v.Y * (double) v.Y + (double) v.Z * (double) v.Z + (double) v.W * (double) v.W);

        [MonoModLinkFrom($"System.Void {Vector4FName}::Normalize()")]
        public static void Normalize(ref Vector4 v) {
            double invLen = 1.0 / Math.Sqrt((double) v.X * (double) v.X + (double) v.Y * (double) v.Y + (double) v.Z * (double) v.Z + (double) v.W * (double) v.W);
            v.X = (float) (v.X * invLen);
            v.Y = (float) (v.Y * invLen);
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
#endregion

    }
}