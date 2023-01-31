using MonoMod.InlineRT;

namespace MonoMod {
    static class MonoModRules {

        static MonoModRules()
            => MonoModRule.Modder.PostProcessors += NETCoreifier.Coreifier.ConvertToNetCore;

    }
}