#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Monocle;

namespace Celeste {
    static class patch_Achievements {
        public static extern void orig_Register(Achievement achievement);

        public static void Register(Achievement achievement) {
            bool shouldRegister = achievement switch {
                Achievement.STRB1 or Achievement.STRB2 or Achievement.STRB3 or Achievement.CASS
                    => ((patch_SaveData) SaveData.Instance)?.LevelSet == "Celeste",
                Achievement.ONEUP or Achievement.WOW or Achievement.FAREWELL or Achievement.CSIDES
                    => (Engine.Scene as Level ?? (Engine.Scene as LevelLoader)?.Level)?.Session.Area.GetLevelSet() == "Celeste",
                Achievement.PICO8
                    => !Everest.Content.TryGet<AssetTypePico8Tilemap>("Pico8Tilemap", out _),
                _ => true
            };
            if (shouldRegister) {
                orig_Register(achievement);
            }
        }
    }
}
