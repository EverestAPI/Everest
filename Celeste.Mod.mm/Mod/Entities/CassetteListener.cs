using Monocle;
using System;
using System.Linq;

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

        /// <summary>
        /// Matches the functionality of <see cref="CassetteBlock.Tempo"/>.
        /// </summary>
        public float Tempo;

        public CassetteListener(int index, float tempo = 1f) : this(EntityID.None, index, tempo) {
        }

        public CassetteListener(EntityID id, int index, float tempo = 1f) : base(false, false) {
            Index = index;
            Tempo = tempo;
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

#pragma warning disable CS0618 // Type or member is obsolete
        public override void EntityAdded(Scene scene) {
            base.EntityAdded(scene);

            // bail if the scene is null or not a Level
            if (scene is not Level level) {
                return;
            }

            // configure the level for the cassette block manager
            level.HasCassetteBlocks = true;
            level.CassetteBlockBeats = Math.Max(Index + 1, level.CassetteBlockBeats);
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (level.CassetteBlockTempo == 1f) {
                level.CassetteBlockTempo = Tempo;
            }

            // duplicates functionality of Level.ShouldCreateCassetteManager
            if (!(level.Session.Area.Mode != AreaMode.Normal || !level.Session.Cassette)) {
                return;
            }

            // bail if there's a manager tracked
            if (level.Tracker.GetEntity<CassetteBlockManager>() is not null) {
                return;
            }

            // also cater for the possibility that the manager has not yet been added to the tracker
            if (level.Entities.Any(e => e is CassetteBlockManager) ||
                level.Entities.GetToAdd().Any(e => e is CassetteBlockManager)) {
                return;
            }

            // add a new cassette block manager to the scene
            level.Add(new CassetteBlockManager());
        }
#pragma warning restore CS0618 // Type or member is obsolete

        public enum Modes
        {
            Enabled,
            WillDisable,
            Disabled,
            WillEnable,
        }
    }
}