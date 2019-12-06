using System.Collections.Generic;

namespace Celeste.Mod.Helpers {
    public class ModUpdateInfo {
        public virtual string Name { get; set; }
        public virtual string Version { get; set; }
        public virtual int LastUpdate { get; set; }
        public virtual string URL { get; set; }
        public virtual List<string> xxHash { get; set; }
        public virtual string GameBananaType { get; set; }
        public virtual int GameBananaId { get; set; }
    }
}
