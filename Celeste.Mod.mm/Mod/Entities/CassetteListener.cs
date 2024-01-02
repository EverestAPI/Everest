using Monocle;
using System;

namespace Celeste.Mod.Entities {
    [Tracked]
    public class CassetteListener : Component {
        /// <summary>
        /// Called by <see cref="Level.LoadLevel"/> when loading a room.
        /// The parameter indicates whether this component will be the active one.
        /// </summary>
        public Action<bool> OnStart;

        /// <summary>
        /// Called by <see cref="CassetteBlockManager.StopBlocks"/> after collecting a cassette.
        /// </summary>
        public Action OnFinish;

        /// <summary>
        /// Called by <see cref="CassetteBlockManager.SetWillActivate"/> if <see cref="Index"/> is about to become current.
        /// For a <see cref="CassetteBlock"/>, this is represented by a non-collidable block moving one pixel up.
        /// </summary>
        public Action OnWillActivate;

        /// <summary>
        /// Called by <see cref="CassetteBlockManager.SetWillActivate"/> if <see cref="Index"/> is about to no longer be current.
        /// For a <see cref="CassetteBlock"/>, this is represented by a collidable block moving one pixel down.
        /// </summary>
        public Action OnWillDeactivate;

        /// <summary>
        /// Called by <see cref="CassetteBlockManager.SetActiveIndex"/> if <see cref="Activated"/> was changed from false to true.
        /// For a <see cref="CassetteBlock"/>, this is represented by the block becoming collidable.
        /// </summary>
        public Action OnActivated;

        /// <summary>
        /// Called by <see cref="CassetteBlockManager.SetActiveIndex"/> if <see cref="Activated"/> was changed from true to false.
        /// For a <see cref="CassetteBlock"/>, this is represented by the block becoming non-collidable.
        /// </summary>
        public Action OnDeactivated;

        /// <summary>
        /// Matches the functionality of <see cref="CassetteBlock.Index"/>.
        /// </summary>
        public int Index;

        /// <summary>
        /// Matches the functionality of <see cref="CassetteBlock.Activated"/>.
        /// </summary>
        public bool Activated;

        /// <summary>
        /// Matches the functionality of <see cref="CassetteBlock.Mode"/>.
        /// </summary>
        public Modes Mode;

        /// <summary>
        /// Matches the functionality of <see cref="CassetteBlock.ID"/>.
        /// </summary>
        public EntityID ID;

        public CassetteListener(int index) : this(index, EntityID.None) {
        }

        public CassetteListener(int index, EntityID id) : base(false, false) {
            Index = index;
            ID = id;
        }

        /// <summary>
        /// Called by <see cref="CassetteBlockManager.SetActiveIndex"/>.
        /// </summary>
        public void SetActivated(bool activated) {
            if (activated == Activated) {
                return;
            }

            Activated = activated;

            if (activated) {
                Mode = Modes.Enabled;
                OnActivated?.Invoke();
            } else {
                Mode = Modes.Disabled;
                OnDeactivated?.Invoke();
            }
        }

        /// <summary>
        /// Called by <see cref="CassetteBlockManager.SilentUpdateBlocks"/>.
        /// </summary>
        public void Start(bool activated) {
            Activated = activated;
            Mode = activated ? Modes.Enabled : Modes.Disabled;
            OnStart?.Invoke(activated);
        }

        /// <summary>
        /// Called by <see cref="CassetteBlockManager.StopBlocks"/>.
        /// </summary>
        public void Finish() {
            Activated = false;
            Mode = Modes.Disabled;
            OnFinish?.Invoke();
        }

        /// <summary>
        /// Called by <see cref="CassetteBlockManager.SetWillActivate"/>.
        /// </summary>
        public void WillToggle() {
            if (Activated) {
                Mode = Modes.WillDisable;
                OnWillDeactivate?.Invoke();
            } else {
                Mode = Modes.WillEnable;
                OnWillActivate?.Invoke();
            }
        }

        public enum Modes
        {
            Enabled,
            WillDisable,
            Disabled,
            WillEnable,
        }
    }
}