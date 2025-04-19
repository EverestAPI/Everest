#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Mono.Cecil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System;

namespace Microsoft.Xna.Framework {

    [GameDependencyPatch("FNA")]
    public partial class patch_Game : Game {
        // We're effectively in Game, but still need to "expose" private fields to our mod.
        private readonly patch_GameTime gameTime;

        [MonoModIfFlag("Headless")]
        [MonoModConstructor]
        [MonoModIgnore]
        [PatchGameCtor]
        public extern void ctor();

        // Directly call Update(), avoiding timing / Render()
        [MonoModIfFlag("Headless")]
        [MonoModLinkFrom("System.Void Microsoft.Xna.Framework.Game::Tick()")]
        public void UpdateWrapper() {
            gameTime.ElapsedGameTime = TargetElapsedTime;
            gameTime.TotalGameTime += TargetElapsedTime;

            Update(gameTime);
        }
    }

    [GameDependencyPatch("FNA")]
    public class patch_GameTime : GameTime {
        // We're effectively in GameTime, but still need to "expose" private fields to our mod.
        public new TimeSpan TotalGameTime { get; internal set; }
        public new TimeSpan ElapsedGameTime { get; internal set; }
    }
}

namespace MonoMod {
    /// <summary>
    /// A patch which replaces the GameWindow instance with a headless one
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchGameCtor))]
    class PatchGameCtor : Attribute { }

    /// <summary>
    /// A patch which removes registering the graphics adapter
    /// </summary>
    [MonoModIfFlag("Headless")]
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchGameBeforeLoop))]
    class PatchGameBeforeLoop : Attribute { }

    static partial class MonoModRules {
        public static void PatchGameCtor(ILContext context, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(context);

            TypeDefinition t_HeadlessWindow = MonoModRule.Modder.FindType("Microsoft.Xna.Framework.HeadlessWindow").Resolve();
            MethodReference m_HeadlessWindow_ctor = MonoModRule.Modder.Module.ImportReference(t_HeadlessWindow.FindMethod(".ctor"));

            // Replace 'FNAPlatform.CreateWindow()' with 'new HeadlessWindow()'
            cursor.GotoNext(instr => instr.MatchLdsfld("Microsoft.Xna.Framework.FNAPlatform", "CreateWindow"));
            cursor.RemoveRange(2);
            cursor.EmitNewobj(m_HeadlessWindow_ctor);
        }

        public static void PatchGameBeforeLoop(ILContext context, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(context);

            // Remove 'this.currentAdapter = FNAPlatform.RegisterGame(this);'
            cursor.RemoveRange(5);
        }
    }
}
