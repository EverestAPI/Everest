using Microsoft.Xna.Framework.Content;

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
            asset = (T) Everest.Content.Process(assetName, asset);
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
