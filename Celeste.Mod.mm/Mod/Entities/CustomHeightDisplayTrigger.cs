using Microsoft.Xna.Framework;

namespace Celeste.Mod.Entities {
    [CustomEntity("everest/CustomHeightDisplayTrigger")]
    public class CustomHeightDisplayTrigger : Trigger {
        bool isVanilla;
        int target;

        int from;
        string text;
        bool progressAudio;
        bool displayOnTransition;

        public CustomHeightDisplayTrigger(EntityData data, Vector2 offset)
            : base(data, offset) {
            isVanilla = data.Bool("vanilla", false);
            target = data.Int("target", 0);

            from = data.Int("from", 0);
            text = data.Attr("text", "{X}m");
            progressAudio = data.Bool("progressAudio", false);
            displayOnTransition = data.Bool("displayOnTransition", false);
        }

        public override void OnEnter(Player player) {
            base.OnEnter(player);
            if (isVanilla)
                Scene.Add(new HeightDisplay(target));
            else
                Scene.Add(new CustomHeightDisplay(text, target, from, progressAudio, displayOnTransition));
            RemoveSelf();
        }

    }
}
