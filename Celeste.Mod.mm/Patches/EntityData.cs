#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it


namespace Celeste {
    class patch_EntityData : EntityData {

        public extern bool orig_Has(string key);
        public new bool Has(string key) {
            if (Values == null)
                return false;
            return orig_Has(key);
        }

    }
}
