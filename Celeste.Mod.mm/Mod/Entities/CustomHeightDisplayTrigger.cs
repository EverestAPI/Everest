using Microsoft.Xna.Framework;

namespace Celeste.Mod.Entities {
    [CustomEntity("everest/CustomHeightDisplayTrigger")]
    class CustomHeightDisplayTrigger : Trigger {
        bool isVanilla;
        int height;

        int from;
        string text;
        bool progressAudio;
        bool displayOnTransition;

        public CustomHeightDisplayTrigger(EntityData data, Vector2 offset)
            : base(data, offset) {
            isVanilla = data.Bool("vanilla", false);
            height = data.Int("target", 0);

            from = data.Int("from", 0);
            text = data.Attr("text");
            progressAudio = data.Bool("progressAudio", false);
            displayOnTransition = data.Bool("displayOnTransition", false);
        }

        public override void OnEnter(Player player) {
            base.OnEnter(player);
            if (isVanilla)
                Scene.Add(new HeightDisplay(height));
            else
                Scene.Add(new CustomHeightDisplay(text, height, from, progressAudio, displayOnTransition));
            RemoveSelf();
        }

    }
}
