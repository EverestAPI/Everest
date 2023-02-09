using MonoMod;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Celeste.Mod.Helpers.LegacyMonoMod {
    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.IDetour")]
    public interface ILegacyDetour : IDisposable {
        bool IsValid { get; }
        bool IsApplied { get; }

        void Apply();
        void Undo();
        void Free();

        MethodBase GenerateTrampoline(MethodBase signature = null);
        T GenerateTrampoline<T>() where T : Delegate;
    }

    [RelinkLegacyMonoMod("MonoMod.RuntimeDetour.ISortableDetour")]
    public interface ILegacySortableDetour : ILegacyDetour {
        uint GlobalIndex { get; }
        int Priority { get; set; }
        string ID { get; set; }
        IEnumerable<string> Before { get; set; }
        IEnumerable<string> After { get; set; }
    }
}
