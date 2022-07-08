#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Monocle;

namespace Celeste {
    class patch_CS07_Credits : CS07_Credits {
        private bool wasDashAssistOn;

        public patch_CS07_Credits() : base() {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public override void SceneEnd(Scene scene) {
            base.SceneEnd(scene);

            Instance = null;
            SaveData.Instance.Assists.DashAssist = wasDashAssistOn;
            Audio.BusMuted("bus:/gameplay_sfx", false);
            MInput.Disabled = false;
        }
    }
}
