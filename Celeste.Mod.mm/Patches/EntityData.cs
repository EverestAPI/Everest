using Microsoft.Xna.Framework;

#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it


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

        /// <summary>
        /// Get the <see cref="T:System.String" /> value associated with a key.
        /// An Empty or pure Whitespace value will result in the <paramref name="defaultValue"/> to be returned.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public string String(string key, string defaultValue = null) {
            string valueStr;
            if (Values != null && Values.TryGetValue(key, out object value) && !string.IsNullOrWhiteSpace(valueStr = value?.ToString())) {
                return valueStr;
            }
            return defaultValue;
        }

        // don't hardcode x/y affixes, because different code modders use different naming conventions.
        // instead, code modders are free to make extension methods which pass their own affixes!
        /// <summary>
        /// Get a <see cref="Microsoft.Xna.Framework.Vector2"/> from two <see cref="float"/> X/Y coordinate keys.<br/>
        /// If either coordinate key does not have a value, the corresponding coordinate of <paramref name="defaultValue"/>
        /// will be used.
        /// </summary>
        /// <param name="keyX">The key containing the X vector coordinate.</param>
        /// <param name="keyY">The key containing the Y vector coordinate.</param>
        /// <param name="defaultValue">The default vector coordinates, used when either key does not have a value.</param>
        public Vector2 Vector2(string keyX, string keyY, Vector2 defaultValue = default) {
            // default == Vector2.Zero, so this is fine
            return new Vector2(Float(keyX, defaultValue.X), Float(keyY, defaultValue.Y));
        }
    }
}
