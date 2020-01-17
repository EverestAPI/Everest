using Microsoft.Xna.Framework;

namespace Celeste.Mod.Entities
{
    [CustomEntity("everest/activateDreamBlocksTrigger")]
    class ActivateDreamBlocksTrigger : Trigger
    {
        public ActivateDreamBlocksTrigger(EntityData data, Vector2 offset)
            : base(data, offset) { }

        public override void OnEnter(Player player)
        {
            Level level = Scene as Level;
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
