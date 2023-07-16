using Celeste.Mod.Entities;
using Celeste.Mod.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Monocle {
    class patch_Engine : Engine {

        public static new int ViewWidth { get; private set; }
        public static void SetViewWidth(int value) => ViewWidth = value;

        public static new int ViewHeight { get; private set; }
        public static void SetViewHeight(int value) => ViewHeight = value;

        public static new Viewport Viewport { get; private set; }
        public static void SetViewport(Viewport value) => Viewport = value;

#pragma warning disable CS0649 // variable defined in vanilla
        private Scene nextScene;
#pragma warning restore CS0649

        /// <summary>
        /// Allows to check which scene is next in methods like Entity.SceneEnd() or Scene.End().
        /// </summary>
        public static Scene NextScene => ((patch_Engine) Instance).nextScene;

        public patch_Engine(int width, int height, int windowWidth, int windowHeight, string windowTitle, bool fullscreen, bool vsync)
            : base(width, height, windowWidth, windowHeight, windowTitle, fullscreen, vsync) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        private static readonly FieldInfo f_Game_RunApplication = typeof(Game).GetField("RunApplication", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo m_Game_RunLoop = typeof(Game).GetMethod("RunLoop", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo m_Game_AfterLoop = typeof(Game).GetMethod("AfterLoop", BindingFlags.NonPublic | BindingFlags.Instance);

        [MonoModReplace]
        public new void RunWithLogging() {
            bool continueLoop = false;
            restart:;
            try {
                if (!continueLoop)
                    Run();
                else {
                    m_Game_RunLoop.Invoke(this, Array.Empty<object>());
                    EndRun();
                    m_Game_AfterLoop.Invoke(this, Array.Empty<object>());
                }
            } catch (Exception ex) {
                Debugger.Break();

                if (continueLoop && ex is TargetInvocationException)
                    ex = ex.InnerException;

                // Try to handle the error by opening an in-game handler first (unless the exception occurred while exiting)
                if ((bool) f_Game_RunApplication.GetValue(this) && CriticalErrorHandler.HandleCriticalError(ex)) {
                    // Restart the update loop
                    continueLoop = true;
                    goto restart;
                }

                // Lazy loading can cause some issues, f.e. vanishing outlines on dust bunnies.
                // It thus shouldn't be enabled automatically.
                /*
                if (e is OutOfMemoryException && CoreModule.Instance?._Settings != null && !CoreModule.Settings.LazyLoading) {
                    CoreModule.Settings.LazyLoading = true;
                    CoreModule.Instance.SaveSettings();
                }
                */

                ErrorLog.Write(ex);
                ErrorLog.Open();
            }
        }

        [MonoModIgnore]
        [PatchEngineUpdate]
        protected override extern void Update(GameTime gameTime);

        private static float GetTimeRateComponentMultiplier(Scene scene) {
            return scene?.Tracker.GetComponents<TimeRateModifier>()
                                 .Cast<TimeRateModifier>()
                                 .Where(trm => trm.Enabled)
                                 .Select(trm => trm.Multiplier)
                                 .Aggregate(1f, (acc, val) => acc * val)
                   ?? 1;
        }

    }
    public static class EngineExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static int ViewWidth {
            get {
                return Engine.ViewWidth;
            }
            set {
                patch_Engine.SetViewWidth(value);
            }
        }

        public static int ViewHeight {
            get {
                return Engine.ViewHeight;
            }
            set {
                patch_Engine.SetViewHeight(value);
            }
        }

        public static Viewport Viewport {
            get {
                return Engine.Viewport;
            }
            set {
                patch_Engine.SetViewport(value);
            }
        }

    }
}

namespace MonoMod {
    
    /// <summary>
    /// Patch the method to apply TimeRateModifier multipliers.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchEngineUpdate))]
    class PatchEngineUpdateAttribute : Attribute { }
    
    static partial class MonoModRules {
        public static void PatchEngineUpdate(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_Engine = context.Method.DeclaringType;
            FieldReference f_scene = t_Engine.FindField("scene");
            MethodReference m_GetTimeRateComponentMultiplier = t_Engine.FindMethod("GetTimeRateComponentMultiplier");
            
            ILCursor cursor = new ILCursor(context);
            // multiply time rate with GetTimeRateComponentMultiplier(scene)
            cursor.GotoNext(MoveType.After, instr => instr.MatchLdsfld("Monocle.Engine", "TimeRateB"),
                                            instr => instr.MatchMul());
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, f_scene);
            cursor.Emit(OpCodes.Call, m_GetTimeRateComponentMultiplier);
            cursor.Emit(OpCodes.Mul);
        }
    }
}
