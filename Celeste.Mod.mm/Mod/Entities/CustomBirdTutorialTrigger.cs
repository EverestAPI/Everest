using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Entities {
    [CustomEntity("everest/customBirdTutorialTrigger")]
    [Tracked]
    class CustomBirdTutorialTrigger : Trigger {
        public string BirdId;
        public bool ShowTutorial;

        public CustomBirdTutorialTrigger(EntityData data, Vector2 offset) : base(data, offset) {
            BirdId = data.Attr("birdId");
            ShowTutorial = data.Bool("showTutorial");
        }

        public override void OnEnter(Player player) {
            base.OnEnter(player);

            CustomBirdTutorial matchingBird = CustomBirdTutorial.FindById(Scene as Level, BirdId);
            if (matchingBird != null) {
                if (ShowTutorial) {
                    matchingBird.TriggerShowTutorial();
                } else {
                    matchingBird.TriggerHideTutorial();
                }
            }
        }
    }
}