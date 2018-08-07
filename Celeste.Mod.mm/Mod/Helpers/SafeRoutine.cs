using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod {
    public class SafeRoutine : IEnumerator {

        private IEnumerator Inner;
        private Entity CoroutineEntity;
        private Coroutine Coroutine;

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
            return !Coroutine.Finished;
        }

        public void Reset() {
            Inner.Reset();
        }

    }
}
