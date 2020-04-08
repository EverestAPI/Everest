using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.UI {
    public class OuiSoundTest : Oui, OuiModOptions.ISubmenu {

        private const float onScreenX = 960f;
        private const float offScreenX = 2880f;

        private EventInstance playing;

        private string audioPrevMusic;
        private string audioPrevAmbience;

        private float pressedTimer;
        private float timer;
        private float ease;

        private int[] digits = new int[5];
        private int selectedDigit;
        private string selectedBankPath;
        private string selectedPath;

        private Wiggler[] wigglerDigits;
        private Wiggler wigglerPath;
        private Wiggler wigglerBankPath;

        private Color unselectColor = Color.White;
        private Color unselectSpecialColor = Color.LightSlateGray;
        private Color selectColorA = Calc.HexToColor("84FF54");
        private Color selectColorB = Calc.HexToColor("FCFF59");

        public OuiSoundTest() {
            wigglerDigits = new Wiggler[digits.Length];
            for (int i = 0; i < digits.Length; i++)
                wigglerDigits[i] = Wiggler.Create(0.25f, 4f);
            wigglerPath = Wiggler.Create(0.25f, 4f);
            wigglerBankPath = Wiggler.Create(0.25f, 4f);
            Position = new Vector2(0f, 1080f);
            Visible = false;
        }

        public override IEnumerator Enter(Oui from) {
            audioPrevMusic = Audio.GetEventName(Audio.CurrentMusicEventInstance);
            Audio.SetMusic(null);
            audioPrevAmbience = Audio.GetEventName(Audio.CurrentAmbienceEventInstance);
            Audio.SetAmbience(null);

            Visible = true;

            for (int i = 0; i < digits.Length; i++)
                digits[i] = 0;
            selectedDigit = digits.Length - 1;
            UpdateSelectedPath();

            Vector2 posFrom = Position;
            Vector2 posTo = Vector2.Zero;
            for (float t = 0f; t < 1f; t += Engine.DeltaTime * 2f) {
                ease = Ease.CubeIn(t);
                Position = posFrom + (posTo - posFrom) * Ease.CubeInOut(t);
                yield return null;
            }
            ease = 1f;
            posFrom = Vector2.Zero;
            posTo = Vector2.Zero;

            yield return 0.2f;

            Focused = true;

            yield return 0.2f;

            for (int i = 0; i < digits.Length; i++)
                wigglerDigits[i].Start();
            wigglerPath.Start();
            wigglerBankPath.Start();
        }

        public override IEnumerator Leave(Oui next) {
            Audio.SetMusic(audioPrevMusic);
            Audio.SetAmbience(audioPrevAmbience);
            Audio.Play(SFX.ui_main_whoosh_large_out);

            if (playing != null)
                Audio.Stop(playing);

            Focused = false;

            Vector2 posFrom = Position;
            Vector2 posTo = new Vector2(0f, 1080f);
            for (float t = 0f; t < 1f; t += Engine.DeltaTime * 2f) {
                ease = 1f - Ease.CubeIn(t);
                Position = posFrom + (posTo - posFrom) * Ease.CubeInOut(t);
                yield return null;
            }

            Visible = false;
        }

        public override void Update() {
            if (!(Selected && Focused)) {
                goto End;
            }

            if (Input.MenuRight.Pressed && selectedDigit < (digits.Length - 1)) {
                selectedDigit = Math.Min(selectedDigit + 1, (digits.Length - 1));
                wigglerDigits[selectedDigit].Start();
                Audio.Play(SFX.ui_main_roll_down);

            } else if (Input.MenuLeft.Pressed && selectedDigit > 0) {
                selectedDigit = Math.Max(selectedDigit - 1, 0);
                wigglerDigits[selectedDigit].Start();
                Audio.Play(SFX.ui_main_roll_up);

            } else if (Input.MenuDown.Pressed) {
                UpdateDigits(selectedDigit, -1);

            } else if (Input.MenuUp.Pressed) {
                UpdateDigits(selectedDigit, +1);

            } else if (Input.MenuConfirm.Pressed) {
                if (playing != null)
                    Audio.Stop(playing);
                if (!string.IsNullOrEmpty(selectedPath))
                    playing = Audio.Play(selectedPath);

            } else if (Input.MenuCancel.Pressed || Input.Pause.Pressed || Input.ESC.Pressed) {
                Focused = false;
                Audio.Play(SFX.ui_main_button_back);
                Overworld.Goto<OuiModOptions>();
            }

            End:
            pressedTimer -= Engine.DeltaTime;
            timer += Engine.DeltaTime;
            for (int i = 0; i < digits.Length; i++)
                wigglerDigits[i].Update();
            wigglerPath.Update();
            wigglerBankPath.Update();
        }

        private void UpdateDigits(int index, int dir) {
            int value = 0;
            for (int i = 0; i < digits.Length; i++) {
                value += digits[i] * (int) Math.Pow(0x10, (digits.Length - 1) - i);
            }

            value += dir * (int) Math.Pow(0x10, (digits.Length - 1) - index);
            if (value < 0)
                value = (int) Math.Pow(0x10, digits.Length) + value;

            for (int i = 0; i < digits.Length; i++) {
                int factor = (int) Math.Pow(0x10, (digits.Length - 1) - i);
                int digit = (value / factor) % 0x10;
                if (digit != digits[i])
                    wigglerDigits[i].Start();
                digits[i] = digit;
            }

            if (index <= 1)
                wigglerBankPath.Start();
            else
                wigglerPath.Start();

            if (dir < 0)
                Audio.Play(SFX.ui_main_button_toggle_off);
            else
                Audio.Play(SFX.ui_main_button_toggle_on);

            UpdateSelectedPath();
        }

        private void UpdateSelectedPath() {
            selectedBankPath = "";
            selectedPath = "";

            patch_Audio.System.getBankList(out Bank[] banks);
            int bankI = 0;
            for (int i = 0; i <= 1; i++) {
                bankI += digits[i] * (int) Math.Pow(0x10, (2 - 1) - i);
            }
            if (bankI >= banks.Length || !(banks[bankI]?.isValid() ?? false))
                return;

            Bank bank = banks[bankI];
            selectedBankPath = patch_Audio.GetBankName(bank);

            bank.getEventList(out EventDescription[] events);
            List<string> paths = events.Where(e => e?.isValid() ?? false).Select(e => patch_Audio.GetEventName(e)).ToList();
            paths.Sort();

            int eventI = 0;
            for (int i = 2; i < digits.Length; i++) {
                eventI += digits[i] * (int) Math.Pow(0x10, (digits.Length - 1) - i);
            }
            if (eventI >= paths.Count)
                return;

            selectedPath = paths[eventI];
        }

        public override void Render() {
            if (ease > 0f)
                Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * ease * 0.4f);
            base.Render();

            const float spacingX = 48f;
            const float spacingY = 64f;

            Vector2 posCenter = Position + new Vector2(1920f / 2f, 1080f / 2f);
            Vector2 pos;

            // Vector2 posInput = posCenter - new Vector2(spacingX * (digits.Length - 1f) / 2f, spacingY * 0.5f);
            Vector2 posInput = Position + new Vector2(384f, 1080f / 2f);
            pos = posInput;
            for (int i = 0; i < digits.Length; i++) {
                DrawOptionText(digits[i].ToString("X1"), pos + new Vector2(0f, wigglerDigits[i].Value * 8f), new Vector2(0f, 0.5f), Vector2.One, selectedDigit == i, i <= 1);
                pos.X += spacingX;
            }

            // pos = posCenter + new Vector2(0f, spacingY * 0.5f + wigglerPath.Value * 2f);
            pos = posInput + new Vector2(spacingX * 2f, spacingY * 0.8f + wigglerPath.Value * 2f);
            ActiveFont.DrawOutline(selectedPath ?? "", pos, new Vector2(0f, 0.5f), Vector2.One * 0.75f, Color.White * ease, 2f, Color.Black * ease * ease * ease);

            pos = posInput + new Vector2(0f, spacingY * -0.8f + wigglerBankPath.Value * 2f);
            ActiveFont.DrawOutline(selectedBankPath ?? "", pos, new Vector2(0f, 0.5f), Vector2.One * 0.75f, Color.LightSlateGray * ease, 2f, Color.Black * ease * ease * ease);

            ActiveFont.DrawEdgeOutline(Dialog.Clean("soundtest_title"), Position + new Vector2(960f, 256f), new Vector2(0.5f, 0.5f), Vector2.One * 2f, Color.Gray, 4f, Color.DarkSlateBlue, 2f, Color.Black);
        }

        private void DrawOptionText(string text, Vector2 at, Vector2 justify, Vector2 scale, bool selected, bool special = false) {
            Color color =
                selected ? (Calc.BetweenInterval(timer, 0.1f) ? selectColorA : selectColorB) :
                special ? unselectSpecialColor :
                unselectColor;
            ActiveFont.DrawOutline(text, at, justify, scale, color * ease, 2f, Color.Black * ease * ease * ease);
        }

    }
}
