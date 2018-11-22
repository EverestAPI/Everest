using Microsoft.Xna.Framework;

namespace Celeste.Mod.DialogCutsceneHelper
{
    public class DialogCutsceneTrigger : Trigger
    {
        string dialogEntry;
        private bool triggered;
        private EntityID id;
        private bool onlyOnce;

        public DialogCutsceneTrigger(EntityData data, Vector2 offset, EntityID entId) : base(data, offset)
        {
            this.dialogEntry = data.Attr("dialogId", "");
            this.onlyOnce = data.Bool("onlyOnce", true);
            triggered = false;
            id = entId;
        }

        public override void OnEnter(Player player)
        {
            if (!triggered && !(base.Scene as Level).Session.GetFlag("DoNotLoad" + this.id))
            {
                triggered = true;
                base.Scene.Add(new DialogCutscene(this.dialogEntry, player));
                if (onlyOnce)
                {
                    (base.Scene as Level).Session.SetFlag("DoNotLoad" + this.id, true); //Sets flag to not load
                }
            }
        }
    }
}
