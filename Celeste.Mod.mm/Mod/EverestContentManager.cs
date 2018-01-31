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
            RootDirectory = inner.RootDirectory;
        }

        protected override Stream OpenStream(string assetName) {
            AssetMetadata mapping = Everest.Content.GetMapped(assetName);
            // If we've got a valid non-patch mapping, load it.
            if (mapping != null && !mapping.IsPatch)
                return mapping.Stream;

            return base.OpenStream(assetName);
        }

        public override T Load<T>(string assetName) {
            T asset = base.Load<T>(assetName);
            if (asset == null)
                return asset;

            // If we've got a valid patch mapping, apply it.
            asset = Everest.Content.Patch(assetName, asset);

            return asset;
        }

    }
}
