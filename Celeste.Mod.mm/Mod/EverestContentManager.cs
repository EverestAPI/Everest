using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Celeste.Mod {
    class EverestContentManager : ContentManager {

        private ContentManager Inner;

        public EverestContentManager(ContentManager inner)
            : base(inner.ServiceProvider) {
            Inner = inner;
            RootDirectory = inner.RootDirectory;
        }

        public override T Load<T>(string assetName) {
            AssetMetadata mapping = Everest.Content.Get(assetName);
            // If we've got a valid mapping, load it instead of the original asset.
            if (mapping != null)
                return base.Load<T>(assetName);

            // We don't have any overriding mapping - load from the inner CM instead.
            T asset = Inner.Load<T>(assetName);
            asset = Everest.Content.Process(assetName, asset);
            return asset;
        }

        public override void Unload() {
            Inner.Unload();
            base.Unload();
        }

        protected override void Dispose(bool disposing) {
            Inner.Dispose();
            base.Dispose(disposing);
        }

    }
}
