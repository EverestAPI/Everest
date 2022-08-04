using Monocle;
using MonoMod;
using System.Collections.Generic;

namespace Celeste.Mod.Helpers {
    /// <summary>
    /// Surrogate class for serializing MouseButton bindings separately from the rest of the input bindings.
    /// This is to prevent Vanilla from complaining and wiping them when it tries to load them.
    /// 
    /// We can't just serialize them as properties with the appropriate `get` and `set` accessors because
    /// xml deserializes IEnumerables by getting the object and then using the `Add` method to add to it.
    /// </summary>
    public class VanillaMouseBindings {

        public List<patch_MInput.patch_MouseData.MouseButtons> Left;
        public List<patch_MInput.patch_MouseData.MouseButtons> Right;
        public List<patch_MInput.patch_MouseData.MouseButtons> Down;
        public List<patch_MInput.patch_MouseData.MouseButtons> Up;
        public List<patch_MInput.patch_MouseData.MouseButtons> MenuLeft;
        public List<patch_MInput.patch_MouseData.MouseButtons> MenuRight;
        public List<patch_MInput.patch_MouseData.MouseButtons> MenuDown;
        public List<patch_MInput.patch_MouseData.MouseButtons> MenuUp;
        public List<patch_MInput.patch_MouseData.MouseButtons> Grab;
        public List<patch_MInput.patch_MouseData.MouseButtons> Jump;
        public List<patch_MInput.patch_MouseData.MouseButtons> Dash;
        public List<patch_MInput.patch_MouseData.MouseButtons> Talk;
        public List<patch_MInput.patch_MouseData.MouseButtons> Pause;
        public List<patch_MInput.patch_MouseData.MouseButtons> Confirm;
        public List<patch_MInput.patch_MouseData.MouseButtons> Cancel;
        public List<patch_MInput.patch_MouseData.MouseButtons> Journal;
        public List<patch_MInput.patch_MouseData.MouseButtons> QuickRestart;
        public List<patch_MInput.patch_MouseData.MouseButtons> DemoDash;
        public List<patch_MInput.patch_MouseData.MouseButtons> RightMoveOnly;
        public List<patch_MInput.patch_MouseData.MouseButtons> LeftMoveOnly;
        public List<patch_MInput.patch_MouseData.MouseButtons> DownMoveOnly;
        public List<patch_MInput.patch_MouseData.MouseButtons> UpMoveOnly;
        public List<patch_MInput.patch_MouseData.MouseButtons> RightDashOnly;
        public List<patch_MInput.patch_MouseData.MouseButtons> LeftDashOnly;
        public List<patch_MInput.patch_MouseData.MouseButtons> DownDashOnly;
        public List<patch_MInput.patch_MouseData.MouseButtons> UpDashOnly;

        /// <summary>
        /// Initializes fields based on the equivalent fields in <see cref="Settings.Instance"/>.
        /// </summary>
        public VanillaMouseBindings Init() {
            Left = ((patch_Binding) Settings.Instance.Left).Mouse;
            Right = ((patch_Binding) Settings.Instance.Right).Mouse;
            Down = ((patch_Binding) Settings.Instance.Down).Mouse;
            Up = ((patch_Binding) Settings.Instance.Up).Mouse;
            MenuLeft = ((patch_Binding) Settings.Instance.MenuLeft).Mouse;
            MenuRight = ((patch_Binding) Settings.Instance.MenuRight).Mouse;
            MenuDown = ((patch_Binding) Settings.Instance.MenuDown).Mouse;
            MenuUp = ((patch_Binding) Settings.Instance.MenuUp).Mouse;
            Grab = ((patch_Binding) Settings.Instance.Grab).Mouse;
            Jump = ((patch_Binding) Settings.Instance.Jump).Mouse;
            Dash = ((patch_Binding) Settings.Instance.Dash).Mouse;
            Talk = ((patch_Binding) Settings.Instance.Talk).Mouse;
            Pause = ((patch_Binding) Settings.Instance.Pause).Mouse;
            Confirm = ((patch_Binding) Settings.Instance.Confirm).Mouse;
            Cancel = ((patch_Binding) Settings.Instance.Cancel).Mouse;
            Journal = ((patch_Binding) Settings.Instance.Journal).Mouse;
            QuickRestart = ((patch_Binding) Settings.Instance.QuickRestart).Mouse;
            DemoDash = ((patch_Binding) Settings.Instance.DemoDash).Mouse;
            LeftMoveOnly = ((patch_Binding) Settings.Instance.LeftMoveOnly).Mouse;
            RightMoveOnly = ((patch_Binding) Settings.Instance.RightMoveOnly).Mouse;
            DownMoveOnly = ((patch_Binding) Settings.Instance.DownMoveOnly).Mouse;
            UpMoveOnly = ((patch_Binding) Settings.Instance.UpMoveOnly).Mouse;
            LeftDashOnly = ((patch_Binding) Settings.Instance.LeftDashOnly).Mouse;
            RightDashOnly = ((patch_Binding) Settings.Instance.RightDashOnly).Mouse;
            DownDashOnly = ((patch_Binding) Settings.Instance.DownDashOnly).Mouse;
            UpDashOnly = ((patch_Binding) Settings.Instance.UpDashOnly).Mouse;
            return this;
        }

        /// <summary>
        /// Applies the values in this object's fields to the equivalent fields in <see cref="Settings.Instance"/>
        /// using <see cref="patch_Binding.Add(patch_MInput.patch_MouseData.MouseButtons[])"/>.
        /// </summary>
        public void Apply() {
            ((patch_Binding) Settings.Instance.Left).Add(Left.ToArray());
            ((patch_Binding) Settings.Instance.Right).Add(Right.ToArray());
            ((patch_Binding) Settings.Instance.Down).Add(Down.ToArray());
            ((patch_Binding) Settings.Instance.Up).Add(Up.ToArray());
            ((patch_Binding) Settings.Instance.MenuLeft).Add(MenuLeft.ToArray());
            ((patch_Binding) Settings.Instance.MenuRight).Add(MenuRight.ToArray());
            ((patch_Binding) Settings.Instance.MenuDown).Add(MenuDown.ToArray());
            ((patch_Binding) Settings.Instance.MenuUp).Add(MenuUp.ToArray());
            ((patch_Binding) Settings.Instance.Grab).Add(Grab.ToArray());
            ((patch_Binding) Settings.Instance.Jump).Add(Jump.ToArray());
            ((patch_Binding) Settings.Instance.Dash).Add(Dash.ToArray());
            ((patch_Binding) Settings.Instance.Talk).Add(Talk.ToArray());
            ((patch_Binding) Settings.Instance.Pause).Add(Pause.ToArray());
            ((patch_Binding) Settings.Instance.Confirm).Add(Confirm.ToArray());
            ((patch_Binding) Settings.Instance.Cancel).Add(Cancel.ToArray());
            ((patch_Binding) Settings.Instance.Journal).Add(Journal.ToArray());
            ((patch_Binding) Settings.Instance.QuickRestart).Add(QuickRestart.ToArray());
            ((patch_Binding) Settings.Instance.DemoDash).Add(DemoDash.ToArray());
            ((patch_Binding) Settings.Instance.LeftMoveOnly).Add(LeftMoveOnly.ToArray());
            ((patch_Binding) Settings.Instance.RightMoveOnly).Add(RightMoveOnly.ToArray());
            ((patch_Binding) Settings.Instance.DownMoveOnly).Add(DownMoveOnly.ToArray());
            ((patch_Binding) Settings.Instance.UpMoveOnly).Add(UpMoveOnly.ToArray());
            ((patch_Binding) Settings.Instance.LeftDashOnly).Add(LeftDashOnly.ToArray());
            ((patch_Binding) Settings.Instance.RightDashOnly).Add(RightDashOnly.ToArray());
            ((patch_Binding) Settings.Instance.DownDashOnly).Add(DownDashOnly.ToArray());
            ((patch_Binding) Settings.Instance.UpDashOnly).Add(UpDashOnly.ToArray());
        }

    }
}
