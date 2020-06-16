using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Monocle;
using System;

namespace Celeste.Mod.UI {
    class MainMenuModOptionsButton : MainMenuSmallButton {
        private string subText;

        public override float ButtonHeight {
            get {
                if (subText == null)
                    return ActiveFont.LineHeight * 1.25f;

                return ActiveFont.LineHeight * 1.75f;
            }
        }

        public MainMenuModOptionsButton(string labelName, string iconName, Oui oui, Vector2 targetPosition, Vector2 tweenFrom, Action onConfirm)
            : base(labelName, iconName, oui, targetPosition, tweenFrom, onConfirm) {

            int delayedModCount = Everest.Loader.Delayed.Count;
            int modUpdatesAvailable = ModUpdaterHelper.IsAsyncUpdateCheckingDone() ? ModUpdaterHelper.GetAsyncLoadedModUpdates().Count : 0;
            if (delayedModCount > 1) {
                subText = string.Format(Dialog.Get("MENU_MODOPTIONS_MULTIPLE_MODS_FAILEDTOLOAD"), delayedModCount);
            } else if (delayedModCount == 1) {
                subText = Dialog.Clean("MENU_MODOPTIONS_ONE_MOD_FAILEDTOLOAD");
            } else if (Everest.Updater.HasUpdate) {
                subText = Dialog.Clean("MENU_MODOPTIONS_UPDATE_AVAILABLE");
            } else if (modUpdatesAvailable > 1) {
                subText = string.Format(Dialog.Get("MENU_MODOPTIONS_MOD_UPDATES_AVAILABLE"), modUpdatesAvailable);
            } else if (modUpdatesAvailable == 1) {
                subText = Dialog.Clean("MENU_MODOPTIONS_MOD_UPDATE_AVAILABLE");
            } else {
                subText = null;
            }
        }

        public override void Render() {
            base.Render();

            if (subText != null) {
                Vector2 offset = new Vector2(Ease.CubeInOut(this.GetEase()) * 32f, this.GetWiggler().Value * 8f);
                ActiveFont.DrawOutline(subText, Position + offset + new Vector2(84f, 84f), new Vector2(0f, 0.5f), Vector2.One * 0.6f, Color.OrangeRed, 2f, Color.Black);
            }
        }
    }
}
