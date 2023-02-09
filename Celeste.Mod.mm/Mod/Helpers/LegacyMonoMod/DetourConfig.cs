using MonoMod;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Celeste.Mod.Helpers.LegacyMonoMod {
    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.DetourConfig")]
    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.HookConfig")]
    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.ILHookConfig")]
    public class LegacyDetourConfig : DetourConfig {
        //TODO Does making detour config classes instead structs break stuff?
 
        private static FieldInfo GetBackingField(PropertyInfo prop)
            => prop.DeclaringType.GetField($"<{prop.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance) ??
            throw new InvalidOperationException($"Couldn't find backing field of property {prop}");

        private static readonly FieldInfo Priority_BackingField = GetBackingField(typeof(DetourConfig).GetProperty(nameof(DetourConfig.Priority)));
        private static readonly FieldInfo ID_BackingField = GetBackingField(typeof(DetourConfig).GetProperty(nameof(DetourConfig.Id)));
        private static readonly FieldInfo Before_BackingField = GetBackingField(typeof(DetourConfig).GetProperty(nameof(DetourConfig.Before)));
        private static readonly FieldInfo After_BackingField = GetBackingField(typeof(DetourConfig).GetProperty(nameof(DetourConfig.After)));

        public bool ManualApply { get; set; }

        public new int Priority {
            get => base.Priority ?? 0;
            set => Priority_BackingField.SetValue(this, (int?) value);
        }

        public string ID {
            get => base.Id;
            set => ID_BackingField.SetValue(this, value);
        }

        public new IEnumerable<string> Before {
            get => base.Before;
            set => Before_BackingField.SetValue(this, value);
        }

        public new IEnumerable<string> After {
            get => base.After;
            set => After_BackingField.SetValue(this, value);
        }

        public LegacyDetourConfig() : base(default, default, default, default) {}
        public LegacyDetourConfig(string id, int? priority = null, IEnumerable<string> before = null, IEnumerable<string> after = null) : base(id, priority, before, after) {}
    
    }
}
