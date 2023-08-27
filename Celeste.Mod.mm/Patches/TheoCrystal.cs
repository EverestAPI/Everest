#pragma warning disable CS0108 // Method hides inherited member
#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;
using System;

namespace Celeste {
    class patch_TheoCrystal : TheoCrystal {
        public patch_Holdable Hold; // avoids extra cast

        public patch_TheoCrystal(EntityData data, Vector2 offset)
            : base(data, offset) {
        }

        public extern void orig_ctor(Vector2 position);

        [MonoModConstructor]
        public void ctor(Vector2 position) {
            orig_ctor(position);
            Hold.SpeedSetter = (speed) => { Speed = speed; };
        }

        [MonoModIgnore]
        [PatchTheoCrystalUpdateAttribute]
        public extern new void Update();

        private static bool IsPlayerHoldingItemAndTransitioningUp(TheoCrystal theoCrystal) {
            if (new DynamicData(theoCrystal).Get<Level>("Level") is { } level && level.Tracker.GetEntity<Player>() is { } player) {
                return player.Holding?.IsHeld ?? false && level.Transitioning && player.CenterY < level.Bounds.Top;
            } else {
                return false;
            }
        }
    }
}

namespace MonoMod {

    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchTheoCrystalUpdate))]
    class PatchTheoCrystalUpdateAttribute : Attribute { }

    static partial class MonoModRules {
        public static void PatchTheoCrystalUpdate(MethodDefinition method, CustomAttribute attrib) {

            MethodDefinition m_IsPlayerHoldingItemAndTransitioningUp = method.DeclaringType.FindMethod("IsPlayerHoldingItemAndTransitioningUp");
            ILLabel afterDieLabel = null;

            new ILContext(method).Invoke(il => {
                ILCursor curser = new(il);
                curser.GotoNext(MoveType.After,
                    instr => instr.MatchCall("Microsoft.Xna.Framework.Rectangle", "get_Bottom"),
                    instr => instr.MatchConvR4(),
                    instr => instr.MatchBleUn(out afterDieLabel),
                    instr => instr.MatchLdarg(0),
                    instr => instr.MatchCallvirt("Celeste.TheoCrystal", "Die"));

                curser.Index -= 2;

                curser.Emit(OpCodes.Ldarg_0);
                curser.Emit(OpCodes.Call, m_IsPlayerHoldingItemAndTransitioningUp);
                curser.Emit(OpCodes.Brtrue, afterDieLabel);
            });
        }
    }
}

