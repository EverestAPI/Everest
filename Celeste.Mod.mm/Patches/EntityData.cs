#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it


using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Celeste {
    class patch_EntityData : EntityData {
        /// <origdoc/>

        public extern bool orig_Has(string key);
        /// <inheritdoc cref="EntityData.Has(string)"/>
        public new bool Has(string key) {
            if (Values == null)
                return false;
            return orig_Has(key);
        }
        internal string BackupLevelString;
        public EntityID EntityID;

        internal void SetEntityID(bool? isTrigger = null) {
            EntityID = new EntityID(Level.Name ?? BackupLevelString ?? string.Empty, ID + ((isTrigger ?? false) || patch_LevelData._isRegisteringTriggers ? 10000000 : 0));
        }
    }
}
