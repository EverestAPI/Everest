using System.Collections;
using Monocle;

namespace Celeste.Mod.Entities
{
    public class DialogCutscene : CutsceneEntity
    {
        private Player player;
        private string dialogEntry;

        public DialogCutscene(string dialogID, Player playerEnt) : base(true, false)
        {
            dialogEntry = dialogID;
            player = playerEnt;
        }

        public override void OnBegin(Level level)
        {
            base.Add(new Coroutine(this.Cutscene(level), true));
        }

        private IEnumerator Cutscene(Level level)
        {
            this.player.StateMachine.State = 11;
            this.player.StateMachine.Locked = true;
            this.player.ForceCameraUpdate = true;
            yield return Textbox.Say(dialogEntry, null);
            this.EndCutscene(level, true);
            yield break;
        }

        public override void OnEnd(Level level)
        {
            this.player.StateMachine.Locked = false;
            this.player.StateMachine.State = 0;
            this.player.ForceCameraUpdate = false;
            bool wasSkipped = this.WasSkipped;
            if (wasSkipped)
            {
                level.Camera.Position = this.player.CameraTarget;
            }
        }
    }
}
