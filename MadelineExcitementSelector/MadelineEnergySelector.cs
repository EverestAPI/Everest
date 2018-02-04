using Celeste.Mod;
using Microsoft.Xna.Framework;
using MonoMod.Detour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Energy {
    public class MadelineEnergySelector : EverestModule {

        public static MadelineEnergySelector Instance;

        public override Type SettingsType => typeof(MadelineEnergySelectorSettings);
        public static MadelineEnergySelectorSettings Settings => (MadelineEnergySelectorSettings) Instance._Settings;

        // The methods we want to hook.
        private readonly static MethodInfo m_Update = typeof(Player).GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        
        public MadelineEnergySelector() {
            Instance = this;
        }

        public override void Load() {
            // Runtime hooks are quite different from static patches.
            Type t_MadelineExcitementSelector = GetType();
            // [trampoline] = [method we want to hook] .Detour< [signature] >( [replacement method] );
            orig_Update = m_Update.Detour<d_Update>(t_MadelineExcitementSelector.GetMethod("Update"));
        }

        public override void Unload() {
            // Let's just hope that nothing else detoured this, as this is depth-based...
            RuntimeDetour.Undetour(m_Update);
        }

        // The delegate tells MonoMod.Detour / RuntimeDetour about the method signature.
        // Instance (non-static) methods must become static, which means we add "this" as the first argument.
        public delegate void d_Update(Player self);
        // A field containing the trampoline to the original method.
        // You don't need to care about how RuntimeDetour handles this behind the scenes.
        public static d_Update orig_Update;

        private static Vector2? accelerationHelper = null;

        public static void Update(Player self)
        {
            orig_Update(self);
            
            if (!accelerationHelper.HasValue)
            {
                accelerationHelper = self.Speed;
            }

            int excitementLevel = Settings.Excitement;

            var acceleration = (self.Speed - accelerationHelper.Value);

            var accelerationBoost = acceleration;

            if (excitementLevel > 0 && excitementLevel <= 7)
            {
                accelerationBoost /= 9 - excitementLevel;
            } else if (excitementLevel > 7)
            {
                accelerationBoost *= 2;
                accelerationBoost /= 16;
                accelerationBoost *= 8 + (excitementLevel - 7);
            } else if (excitementLevel == 0)
            {
                accelerationBoost = new Vector2();
            }

            self.Speed += accelerationBoost;

            acceleration = self.Speed;
        }
    }
}
