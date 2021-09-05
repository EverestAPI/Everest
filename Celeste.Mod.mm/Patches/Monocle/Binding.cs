using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Input;

namespace Monocle {
    class patch_Binding : Binding {

        public bool Remove(params Keys[] keys) {
            // use a stupid way: pretend the keys are removed and check if the rest are still valid
            List<Keys> before = new List<Keys>(Keyboard);
            List<Keys> after = Keyboard.Where(key => !keys.Contains(key)).ToList();
            if (after.Count == 0 && ExclusiveFrom.Count > 0) {
                return false;
            }

            // assign to field is necessary because it's accessed when cross references exist
            Keyboard = after;
            bool result = true;
            foreach (Keys key in after) {
                if (ExclusiveFrom.Any(b => b.Needs(key))) {
                    result = false;
                    break;
                }
            }
            Keyboard = result ? after : before;

            return result;
        }

        public bool Remove(params Buttons[] buttons) {
            // use a stupid way: pretend the buttons are removed and check if the rest are still valid
            List<Buttons> before = new List<Buttons>(Controller);
            List<Buttons> after = Controller.Where(button => !buttons.Contains(button)).ToList();
            if (after.Count == 0 && ExclusiveFrom.Count > 0) {
                return false;
            }

            // assign to field is necessary because it's accessed when cross references exist
            Controller = after;
            bool result = true;
            foreach (Buttons button in after) {
                if (ExclusiveFrom.Any(b => b.Needs(button))) {
                    result = false;
                    break;
                }
            }
            Controller = result ? after : before;

            return result;
        }

    }
}
