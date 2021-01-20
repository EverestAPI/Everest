using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod {
    public class MapDataFixup {

        public List<EverestMapDataProcessor> Processors = new List<EverestMapDataProcessor>();
        public readonly MapData MapData;
        public readonly AreaKey AreaKey;
        public readonly AreaData AreaData;
        public readonly AreaData ParentAreaData;
        public readonly ModeProperties Mode;
        public readonly ModeProperties ParentMode;
        public readonly MapData ParentMapData;
        public BinaryPacker.Element Root;

        public MapDataFixup(MapData map) {
            MapData = map;
            AreaKey = map.Area;
            AreaData = AreaData.Get(AreaKey);
            ParentAreaData = AreaDataExt.Get(AreaData.GetMeta()?.Parent) ?? AreaData;
            Mode = AreaData.Mode[(int) AreaKey.Mode];
            ParentMode = ParentAreaData.Mode.ElementAtOrDefault((int) AreaKey.Mode) ?? Mode;
            ParentMapData = ParentMode?.MapData ?? map;

            foreach (EverestModule module in Everest._Modules) {
                module.PrepareMapDataProcessors(this);
            }
        }

        public void Add<T>() where T : EverestMapDataProcessor, new() {
            Add(new T());
        }

        public void Add<T>(T p) where T : EverestMapDataProcessor {
            p._Init(this);
            Processors.Add(p);
        }

        public T Get<T>() where T : EverestMapDataProcessor {
            foreach (EverestMapDataProcessor p in Processors)
                if (p is T)
                    return (T) p;
            return null;
        }

        public void Process(BinaryPacker.Element root) {
            Root = root;

            foreach (EverestMapDataProcessor p in Processors)
                p.Reset();

            Run("root", root);

            foreach (EverestMapDataProcessor p in Processors)
                p.End();
        }

        public void Run(string stepName, BinaryPacker.Element el) {
            foreach (EverestMapDataProcessor p in Processors)
                p.Run(stepName, el);
        }

    }
}
