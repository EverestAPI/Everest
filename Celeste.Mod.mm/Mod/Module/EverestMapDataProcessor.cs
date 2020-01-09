using System;
using System.Collections.Generic;

namespace Celeste.Mod {
    public abstract class EverestMapDataProcessor {

        public MapDataFixup Context { get; private set; }
        public AreaKey AreaKey => Context.AreaKey;
        public AreaData AreaData => Context.AreaData;
        public ModeProperties Mode => Context.Mode;
        public Dictionary<string, Action<BinaryPacker.Element>> Steps { get; private set; }

        public abstract void Reset();

        internal void _Init(MapDataFixup context) {
            Context = context;
            Steps = Init();
        }
        public abstract Dictionary<string, Action<BinaryPacker.Element>> Init();

        public abstract void End();

    }
}
