using System;
using System.Collections.Generic;

namespace Celeste.Mod {
    public abstract class EverestMapDataProcessor {

        public MapDataFixup Context { get; private set; }
        public AreaKey AreaKey => Context.AreaKey;
        public patch_AreaData AreaData => Context.AreaData;
        public AreaData ParentAreaData => Context.ParentAreaData;
        public ModeProperties Mode => Context.Mode;
        public ModeProperties ParentMode => Context.ParentMode;
        public patch_MapData MapData => Context.MapData;
        public patch_MapData ParentMapData => Context.ParentMapData;
        public Dictionary<string, Action<BinaryPacker.Element>> Steps { get; protected set; }

        public abstract void Reset();

        internal void _Init(MapDataFixup context) {
            Context = context;
            Steps = Init();
        }
        public abstract Dictionary<string, Action<BinaryPacker.Element>> Init();

        public virtual void Run(string stepName, BinaryPacker.Element el) {
            Dictionary<string, Action<BinaryPacker.Element>> steps = Steps;
            if (steps != null && steps.TryGetValue(stepName, out Action<BinaryPacker.Element> step) && step != null)
                step(el);
        }

        public abstract void End();

    }
}
