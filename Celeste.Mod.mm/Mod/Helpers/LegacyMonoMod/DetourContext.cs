using MonoMod;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace Celeste.Mod.Helpers.LegacyMonoMod {
    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.DetourContext")]
    public class LegacyDetourContext : EmptyDetourContext, IDisposable {

        public int Priority;
        private string _ID;
        private readonly string _FallbackID;
        private readonly MethodBase Creator;

        public string ID {
            get => _ID ?? _FallbackID;
            set => _ID = string.IsNullOrEmpty(value) ? null : value;
        }

        public List<string> Before = new List<string>();
        public List<string> After = new List<string>();

        public LegacyDetourConfig DetourConfig => new LegacyDetourConfig(ID, Priority, Before, After);
        public LegacyDetourConfig HookConfig => DetourConfig;
        public LegacyDetourConfig ILHookConfig => DetourConfig;
    
        private readonly DataScope detourScope;

        public LegacyDetourContext() : this(0, null) {}
        public LegacyDetourContext(int prio) : this(prio, null) {}
        public LegacyDetourContext(string id) : this(0, id) {}
        public LegacyDetourContext(int prio, string id) {
            Priority = prio;
            _ID = id;

            // Get the creator
            StackTrace stack = new StackTrace();
            int frameCount = stack.FrameCount;
            for (int i = 0; i < frameCount; i++) {
                MethodBase caller = stack.GetFrame(i).GetMethod();
                if (caller?.DeclaringType == typeof(DetourContext))
                    continue;
                Creator = caller;
                break;
            }

            // Use the detour context
            detourScope = Use();
        }

        public void Dispose() => detourScope.Dispose();

        protected override bool TryGetConfig(out DetourConfig config) {
            config = DetourConfig;
            return true;
        }

    }
}
