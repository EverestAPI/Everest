#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod.Utils;
using System.Buffers;

namespace Celeste.Mod.Helpers;

/// <summary>
///   A temporary <see cref="SpriteBatch"/>.
/// </summary>
/// <remarks>
///   When constructed, the current <see cref="SpriteBatch"/> properties and <see cref="RenderTarget2D"/> are preserved.
///   Then, the <see cref="SpriteBatch"/> is ended, the <see cref="RenderTarget2D"/> is swapped if necessary,
///   and finally the <see cref="SpriteBatch"/> is restarted with the new properties.<br/>
///   When disposed, the previous <see cref="SpriteBatch"/> properties and <see cref="RenderTarget2D"/> are restored.
///   <br/>
///   Useful when interrupting a <see cref="SpriteBatch"/> mid-render to, for example, render a specific entity to a
///   temporary <see cref="RenderTarget2D"/> while applying a custom shader, all while preserving the previous
///   configuration.<br/>
///   <b>Note: Restarting a spritebatch flushes it to the GPU.</b>
///   While the cost is not that significant, it's best to avoid restarting the spritebatch too often per frame.
/// </remarks>
/// <seealso cref="TemporarySpriteBatchBuilder"/>
public ref struct TemporarySpriteBatch
{
    // we don't really expect the RenderTargetBinding[] array size to exceed 1,
    // so a max of 16 seems reasonable
    private static readonly ArrayPool<RenderTargetBinding> _renderTargetPool
        = ArrayPool<RenderTargetBinding>.Create(0x10, 50);

    /// <summary>
    ///   The <see cref="SpriteSortMode"/> of this <see cref="TemporarySpriteBatch"/>.
    /// </summary>
    public readonly SpriteSortMode CurrentSortMode;

    /// <summary>
    ///   The <see cref="BlendState"/> of this <see cref="TemporarySpriteBatch"/>.
    /// </summary>
    public readonly BlendState CurrentBlendState;

    /// <summary>
    ///   The <see cref="SamplerState"/> of this <see cref="TemporarySpriteBatch"/>.
    /// </summary>
    public readonly SamplerState CurrentSamplerState;

    /// <summary>
    ///   The <see cref="DepthStencilState"/> of this <see cref="TemporarySpriteBatch"/>.
    /// </summary>
    public readonly DepthStencilState CurrentDepthStencilState;

    /// <summary>
    ///   The <see cref="RasterizerState"/> of this <see cref="TemporarySpriteBatch"/>.
    /// </summary>
    public readonly RasterizerState CurrentRasterizerState;

    /// <summary>
    ///   The custom <see cref="Effect"/> of this <see cref="TemporarySpriteBatch"/>.
    /// </summary>
    public readonly Effect? CurrentCustomEffect;

    /// <summary>
    ///   The transformation <see cref="Matrix"/> of this <see cref="TemporarySpriteBatch"/>.
    /// </summary>
    public readonly Matrix CurrentTransformMatrix;

    /// <summary>
    ///   The <see cref="RenderTarget2D"/> swapped in for the duration of this <see cref="TemporarySpriteBatch"/>
    ///   or <c>null</c> to draw to the screen if <see cref="HasRenderTarget"/> is <c>true</c>; else always <c>null</c>.
    /// </summary>
    public readonly RenderTarget2D? CurrentRenderTarget;


    /// <summary>
    ///   The <see cref="SpriteSortMode"/> that was used prior to the start of this
    ///   <see cref="TemporarySpriteBatch"/>.
    /// </summary>
    public readonly SpriteSortMode PreviousSortMode;

    /// <summary>
    ///   The <see cref="BlendState"/> that was used prior to the start of this
    ///   <see cref="TemporarySpriteBatch"/>.
    /// </summary>
    public readonly BlendState PreviousBlendState;

    /// <summary>
    ///   The <see cref="SamplerState"/> that was used prior to the start of this
    ///   <see cref="TemporarySpriteBatch"/>.
    /// </summary>
    public readonly SamplerState PreviousSamplerState;

    /// <summary>
    ///   The <see cref="DepthStencilState"/> that was used prior to the start of this
    ///   <see cref="TemporarySpriteBatch"/>.
    /// </summary>
    public readonly DepthStencilState PreviousDepthStencilState;

    /// <summary>
    ///   The <see cref="RasterizerState"/> that was used prior to the start of this
    ///   <see cref="TemporarySpriteBatch"/>.
    /// </summary>
    public readonly RasterizerState PreviousRasterizerState;

    /// <summary>
    ///   The custom <see cref="Effect"/> that was used prior to the start of this
    ///   <see cref="TemporarySpriteBatch"/>.
    /// </summary>
    public readonly Effect? PreviousCustomEffect;

    /// <summary>
    ///   The transformation <see cref="Matrix"/> that was used prior to the start of this
    ///   <see cref="TemporarySpriteBatch"/>.
    /// </summary>
    public readonly Matrix PreviousTransformMatrix;

    /// <summary>
    ///   The <see cref="RenderTargetBinding"/>s that were used prior to the start of this <see cref="TemporarySpriteBatch"/>
    ///   or <c>null</c> to draw to the screen if <see cref="HasRenderTarget"/> is <c>true</c>; else always <c>null</c>.
    /// </summary>
    public readonly RenderTargetBinding[]? PreviousRenderTargets;


    /// <summary>
    ///   Whether a <see cref="RenderTarget2D"/> was swapped in for the duration of this
    ///   <see cref="TemporarySpriteBatch"/>.
    /// </summary>
    public readonly bool HasRenderTarget;

    /// <summary>
    ///   Whether this <see cref="TemporarySpriteBatch"/> is still active.
    /// </summary>
    public bool Active { get; private set; }


    /// <summary>
    ///   Create and immediately begin a new <see cref="TemporarySpriteBatch"/>.
    /// </summary>
    /// <seealso cref="TemporarySpriteBatchBuilder"/>
    internal TemporarySpriteBatch(
        SpriteSortMode? sortMode,
        BlendState? blendState,
        SamplerState? samplerState,
        DepthStencilState? depthStencilState,
        RasterizerState? rasterizerState,
        Effect? customEffect,
        Matrix? transformMatrix,
        RenderTarget2D? renderTarget,
        bool hasCustomEffect,
        bool hasRenderTarget)
    {
        GetSpriteBatchFields(
            out PreviousSortMode,
            out PreviousBlendState,
            out PreviousSamplerState,
            out PreviousDepthStencilState,
            out PreviousRasterizerState,
            out PreviousCustomEffect,
            out PreviousTransformMatrix);

        CurrentSortMode = sortMode ?? PreviousSortMode;
        CurrentBlendState = blendState ?? PreviousBlendState;
        CurrentSamplerState = samplerState ?? PreviousSamplerState;
        CurrentDepthStencilState = depthStencilState ?? PreviousDepthStencilState;
        CurrentRasterizerState = rasterizerState ?? PreviousRasterizerState;
        CurrentCustomEffect = hasCustomEffect ? customEffect : PreviousCustomEffect;
        CurrentTransformMatrix = transformMatrix ?? PreviousTransformMatrix;

        HasRenderTarget = hasRenderTarget;

        GraphicsDevice graphicsDevice = Engine.Graphics.GraphicsDevice;
        if (hasRenderTarget)
        {
            int renderTargetCount = graphicsDevice.GetRenderTargetsNoAllocEXT(null);
            if (renderTargetCount > 0)
            {
                PreviousRenderTargets = _renderTargetPool.Rent(renderTargetCount);
                graphicsDevice.GetRenderTargetsNoAllocEXT(PreviousRenderTargets);
            }
            CurrentRenderTarget = renderTarget;
        }

        Active = true;
        Draw.SpriteBatch.End();
        if (hasRenderTarget)
            Engine.Graphics.GraphicsDevice.SetRenderTarget(CurrentRenderTarget);
        Draw.SpriteBatch.Begin(
            CurrentSortMode,
            CurrentBlendState,
            CurrentSamplerState,
            CurrentDepthStencilState,
            CurrentRasterizerState,
            CurrentCustomEffect,
            CurrentTransformMatrix);
    }

    /// <summary>
    ///   End this <see cref="TemporarySpriteBatch"/>, restore the previous render targets if necessary, and restore
    ///   the previous <see cref="SpriteBatch"/> properties.
    /// </summary>
    public void Dispose()
    {
        if (!Active)
            return;

        Active = false;
        Draw.SpriteBatch.End();
        if (HasRenderTarget)
        {
            Engine.Graphics.GraphicsDevice.SetRenderTargets(PreviousRenderTargets);
            _renderTargetPool.Return(PreviousRenderTargets!, clearArray: true);
        }
        Draw.SpriteBatch.Begin(
            PreviousSortMode,
            PreviousBlendState,
            PreviousSamplerState,
            PreviousDepthStencilState,
            PreviousRasterizerState,
            PreviousCustomEffect,
            PreviousTransformMatrix);
    }

    private static void GetSpriteBatchFields(
        out SpriteSortMode sortMode,
        out BlendState blendState,
        out SamplerState samplerState,
        out DepthStencilState depthStencilState,
        out RasterizerState rasterizerState,
        out Effect? customEffect,
        out Matrix transformMatrix)
    {
        // life would be good if we could just access these directly...

        DynamicData dynData = DynamicData.For(Draw.SpriteBatch);
        sortMode = dynData.Get<SpriteSortMode>("sortMode");
        blendState = dynData.Get<BlendState>("blendState")!;
        samplerState = dynData.Get<SamplerState>("samplerState")!;
        depthStencilState = dynData.Get<DepthStencilState>("depthStencilState")!;
        rasterizerState = dynData.Get<RasterizerState>("rasterizerState")!;
        customEffect = dynData.Get<Effect>("customEffect");
        transformMatrix = dynData.Get<Matrix>("transformMatrix");
    }
}
