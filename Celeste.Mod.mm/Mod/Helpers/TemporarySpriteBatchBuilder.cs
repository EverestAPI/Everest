using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics.CodeAnalysis;

namespace Celeste.Mod.Helpers;

/// <summary>
///   A <see cref="TemporarySpriteBatch"/> builder.
/// </summary>
/// <remarks>
///   This class lets users configure the creation of a new <see cref="TemporarySpriteBatch"/>, which allows
///   users to interrupt an existing <see cref="SpriteBatch"/>, optionally swap in a <see cref="RenderTarget2D"/>
///   and restart the <see cref="SpriteBatch"/> with custom properties.<br/>
///   When done, the <see cref="SpriteBatch"/> is ended, the previous <see cref="RenderTarget2D"/> is restored and the
///   old <see cref="SpriteBatch"/> is resumed.<br/>
///   <b>Note: Restarting a spritebatch flushes it to the GPU.</b>
///   While the cost is not that significant, it's best to avoid restarting the spritebatch too often per frame.
/// </remarks>
public sealed class TemporarySpriteBatchBuilder
{
    /// <summary>
    ///   Whether the <see cref="SpriteBatch"/>'s custom
    ///   <see cref="Microsoft.Xna.Framework.Graphics.Effect"/> should be overridden.
    /// </summary>
    /// <seealso cref="CustomEffect"/>
    public bool HasCustomEffect { get; private set; }

    /// <summary>
    ///   Whether to swap the current <see cref="Microsoft.Xna.Framework.Graphics.RenderTarget2D"/>
    ///   in-between <see cref="SpriteBatch"/>es.
    /// </summary>
    /// <seealso cref="RenderTarget"/>
    public bool HasRenderTarget { get; private set; }


    /// <summary>
    ///   The <see cref="Microsoft.Xna.Framework.Graphics.SpriteSortMode"/> that the new
    ///   <see cref="SpriteBatch"/> should use, or <c>null</c> if the old value should be preserved.
    /// </summary>
    public SpriteSortMode? SortMode { get; private set; }

    /// <summary>
    ///   The <see cref="Microsoft.Xna.Framework.Graphics.BlendState"/> that the new
    ///   <see cref="SpriteBatch"/> should use, or <c>null</c> if the old value should be preserved.
    /// </summary>
    [MaybeNull]
    public BlendState BlendState { get; private set; }

    /// <summary>
    ///   The <see cref="Microsoft.Xna.Framework.Graphics.SamplerState"/> that the new
    ///   <see cref="SpriteBatch"/> should use, or <c>null</c> if the old value should be preserved.
    /// </summary>
    [MaybeNull]
    public SamplerState SamplerState { get; private set; }

    /// <summary>
    ///   The <see cref="Microsoft.Xna.Framework.Graphics.DepthStencilState"/> that the new
    ///   <see cref="SpriteBatch"/> should use, or <c>null</c> if the old value should be preserved.
    /// </summary>
    [MaybeNull]
    public DepthStencilState DepthStencilState { get; private set; }

    /// <summary>
    ///   The <see cref="Microsoft.Xna.Framework.Graphics.RasterizerState"/> that the new
    ///   <see cref="SpriteBatch"/> should use, or <c>null</c> if the old value should be preserved.
    /// </summary>
    [MaybeNull]
    public RasterizerState RasterizerState { get; private set; }

    /// <summary>
    ///   The custom <see cref="Microsoft.Xna.Framework.Graphics.Effect"/> that the new
    ///   <see cref="SpriteBatch"/> should use, or <c>null</c> if no shader should be used.
    /// </summary>
    /// <remarks>
    ///    When <see cref="HasCustomEffect"/> is <c>false</c>, the old value will be preserved
    ///    and this property will always be <c>null</c>.
    /// </remarks>
    [MaybeNull]
    public Effect CustomEffect { get; private set; }

    /// <summary>
    ///   The transformation <see cref="Microsoft.Xna.Framework.Matrix"/> that the new
    ///   <see cref="SpriteBatch"/> should use, or <c>null</c> if the old value should be preserved.
    /// </summary>
    public Matrix? TransformMatrix { get; private set; }

    /// <summary>
    ///   The <see cref="Microsoft.Xna.Framework.Graphics.RenderTarget2D"/> that should be swapped to
    ///   in-between <see cref="SpriteBatch"/>es, or <c>null</c> to render to the screen.
    /// </summary>
    /// <remarks>
    ///    When <see cref="HasRenderTarget"/> is <c>false</c>, no render target changes will be done
    ///    and this property will always be <c>null</c>.
    /// </remarks>
    [MaybeNull]
    public RenderTarget2D RenderTarget { get; private set; }


    // the defaults are the same as the ones in SpriteBatch.Begin

    /// <summary>
    ///   Override the new <see cref="SpriteBatch"/>'s <see cref="Microsoft.Xna.Framework.Graphics.SpriteSortMode"/>.
    /// </summary>
    /// <param name="sortMode">
    ///   The new sort mode.
    /// </param>
    public TemporarySpriteBatchBuilder WithSortMode(SpriteSortMode sortMode)
    {
        SortMode = sortMode;
        return this;
    }

    /// <summary>
    ///   Override the new <see cref="SpriteBatch"/>'s <see cref="Microsoft.Xna.Framework.Graphics.BlendState"/>.
    /// </summary>
    /// <param name="blendState">
    ///   The new blend state. If <c>null</c>, defaults to <see cref="Microsoft.Xna.Framework.Graphics.BlendState.AlphaBlend"/>.
    /// </param>
    public TemporarySpriteBatchBuilder WithBlendState([MaybeNull] BlendState blendState)
    {
        BlendState = blendState ?? BlendState.AlphaBlend;
        return this;
    }

    /// <summary>
    ///   Override the new <see cref="SpriteBatch"/>'s <see cref="Microsoft.Xna.Framework.Graphics.SamplerState"/>.
    /// </summary>
    /// <param name="samplerState">
    ///   The new sampler state. If <c>null</c>, defaults to <see cref="Microsoft.Xna.Framework.Graphics.SamplerState.LinearClamp"/>.
    /// </param>
    public TemporarySpriteBatchBuilder WithSamplerState([MaybeNull] SamplerState samplerState)
    {
        SamplerState = samplerState ?? SamplerState.LinearClamp;
        return this;
    }

    /// <summary>
    ///   Override the new <see cref="SpriteBatch"/>'s <see cref="Microsoft.Xna.Framework.Graphics.DepthStencilState"/>.
    /// </summary>
    /// <param name="depthStencilState">
    ///   The new depth stencil state. If <c>null</c>, defaults to <see cref="Microsoft.Xna.Framework.Graphics.DepthStencilState.None"/>.
    /// </param>
    public TemporarySpriteBatchBuilder WithDepthStencilState([MaybeNull] DepthStencilState depthStencilState)
    {
        DepthStencilState = depthStencilState ?? DepthStencilState.None;
        return this;
    }

    /// <summary>
    ///   Override the new <see cref="SpriteBatch"/>'s <see cref="Microsoft.Xna.Framework.Graphics.RasterizerState"/>.
    /// </summary>
    /// <param name="rasterizerState">
    ///   The new rasterizer state. If <c>null</c>, defaults to <see cref="Microsoft.Xna.Framework.Graphics.RasterizerState.CullCounterClockwise"/>.
    /// </param>
    public TemporarySpriteBatchBuilder WithRasterizerState([MaybeNull] RasterizerState rasterizerState)
    {
        RasterizerState = rasterizerState ?? RasterizerState.CullCounterClockwise;
        return this;
    }

    /// <summary>
    ///   Override the new <see cref="SpriteBatch"/>'s custom <see cref="Microsoft.Xna.Framework.Graphics.Effect"/>.
    /// </summary>
    /// <param name="customEffect">
    ///   The new custom effect or <c>null</c> if none should be used.
    /// </param>
    public TemporarySpriteBatchBuilder WithCustomEffect([MaybeNull] Effect customEffect)
    {
        HasCustomEffect = true;
        CustomEffect = customEffect;
        return this;
    }

    /// <summary>
    ///   Override the new <see cref="SpriteBatch"/>'s transformation <see cref="Microsoft.Xna.Framework.Matrix"/>.
    /// </summary>
    /// <param name="transformMatrix">
    ///   The new transformation matrix.
    /// </param>
    public TemporarySpriteBatchBuilder WithTransformMatrix(Matrix transformMatrix)
    {
        TransformMatrix = transformMatrix;
        return this;
    }

    /// <summary>
    ///   Override the <see cref="RenderTarget2D"/> in-between <see cref="SpriteBatch"/>es.
    /// </summary>
    /// <param name="renderTarget">
    ///   The new render target or <c>null</c> to refer to the screen.
    /// </param>
    public TemporarySpriteBatchBuilder WithRenderTarget([MaybeNull] RenderTarget2D renderTarget)
    {
        HasRenderTarget = true;
        RenderTarget = renderTarget;
        return this;
    }

    /// <summary>
    ///   Restart the <see cref="SpriteBatch"/> with the configured properties.
    /// </summary>
    /// <returns>
    ///   A <see cref="TemporarySpriteBatch"/> that will restore the previous <see cref="SpriteBatch"/> properties
    ///   when disposed. Remember to put it in a <c>using</c> block.
    /// </returns>
    public TemporarySpriteBatch Use()
        => new(
            SortMode,
            BlendState,
            SamplerState,
            DepthStencilState,
            RasterizerState,
            CustomEffect,
            TransformMatrix,
            RenderTarget,
            HasCustomEffect, HasRenderTarget
        );
}
