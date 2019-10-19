using Microsoft.Xna.Framework;

namespace Celeste.Mod.Entities
{
    [CustomEntity("everest/activateDreamBlocksTrigger")]
    class ActivateDreamBlocksTrigger : Trigger
    {
        private readonly Level level;

        public ActivateDreamBlocksTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            level = Scene as Level;
        }

        public override void OnEnter(Player player)
        {
            if (!level.Session.Inventory.DreamDash)
            {
                level.Session.Inventory.DreamDash = true;
                foreach (DreamBlock dreamBlock in level.Tracker.GetEntities<DreamBlock>())
                {
                    dreamBlock.Activate();
                }
            }
        }
    }
}
