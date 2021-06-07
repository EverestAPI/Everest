using Monocle;
using System.Collections;

namespace Celeste.Mod {
    /// <summary>
    /// Wrapper class for IEnumerators which perform mission-critical operations and thus
    /// should continue running in the background, even when the entity has been removed.
    /// </summary>
    public class SafeRoutine : IEnumerator {

        public IEnumerator Inner;
        public Entity CoroutineEntity;
        public Coroutine Coroutine;

        public object Current {
            get {
                return null;
            }
        }

        public SafeRoutine(IEnumerator inner) {
            Inner = inner;
        }

        public bool MoveNext() {
            if (Coroutine == null) {
                CoroutineEntity = new Entity();
                CoroutineEntity.Tag = Tags.Global | Tags.Persistent | Tags.TransitionUpdate | Tags.PauseUpdate | Tags.FrozenUpdate;
                CoroutineEntity.Add(Coroutine = new Coroutine(Inner, true));
                Engine.Scene.Add(CoroutineEntity);
            }

            bool finished = Coroutine.Finished;
            if (finished) {
                CoroutineEntity.RemoveSelf();
                CoroutineEntity = null;
            }
            return !finished;
        }

        public void Reset() {
            Inner.Reset();
            CoroutineEntity?.RemoveSelf();
            Coroutine = null;
            CoroutineEntity = null;
        }

    }
}
