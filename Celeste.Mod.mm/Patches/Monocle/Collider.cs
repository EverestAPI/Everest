using Microsoft.Xna.Framework;
using MonoMod;

namespace Monocle;

public class patch_Collider {
    // Force all these properties to be AggressiveInlining, as they're crucial to collision perf, and contain virtual calls,
    // which could get inlined if the extending type seals their properties.
    
    [MonoModIgnore, ForceAggressiveInlining]
    public float CenterX { get; set; }

    [MonoModIgnore, ForceAggressiveInlining]
	public float CenterY { get; set; }

    [MonoModIgnore, ForceAggressiveInlining]
	public Vector2 TopLeft { get; set; }

    [MonoModIgnore, ForceAggressiveInlining]
	public Vector2 TopCenter { get; set; }

    [MonoModIgnore, ForceAggressiveInlining]
	public Vector2 TopRight { get; set; }

    [MonoModIgnore, ForceAggressiveInlining]
	public Vector2 CenterLeft { get; set; }

    [MonoModIgnore, ForceAggressiveInlining]
	public Vector2 Center { get; set; }

    [MonoModIgnore, ForceAggressiveInlining]
    public Vector2 Size { get; }

    [MonoModIgnore, ForceAggressiveInlining]
	public Vector2 HalfSize { get; }

    [MonoModIgnore, ForceAggressiveInlining]
	public Vector2 CenterRight { get; set; }

    [MonoModIgnore, ForceAggressiveInlining]
	public Vector2 BottomLeft { get; set; }

    [MonoModIgnore, ForceAggressiveInlining]
	public Vector2 BottomCenter { get; set; }

    [MonoModIgnore, ForceAggressiveInlining]
	public Vector2 BottomRight { get; set; }

    [MonoModIgnore, ForceAggressiveInlining]
	public Vector2 AbsolutePosition { get; set; }

    [MonoModIgnore, ForceAggressiveInlining]
	public float AbsoluteX { get; set; }

    [MonoModIgnore, ForceAggressiveInlining]
	public float AbsoluteY { get; set; }

    [MonoModIgnore, ForceAggressiveInlining]
	public float AbsoluteTop { get; set; }

    [MonoModIgnore, ForceAggressiveInlining]
	public float AbsoluteBottom { get; set; }

    [MonoModIgnore, ForceAggressiveInlining]
	public float AbsoluteLeft { get; set; }

    [MonoModIgnore, ForceAggressiveInlining]
	public float AbsoluteRight { get; set; }

    [MonoModIgnore, ForceAggressiveInlining]
	public Rectangle Bounds { get; set; }
}