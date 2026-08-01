using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Celeste.Mod.Helpers;

/// <summary>
/// Helper class for interning boxed representations of simple types, used extensively in EntityData and BinaryPacker elements.
/// </summary>
public static class InternHelper {
    private static readonly object True = true;
    private static readonly object False = false;
    
    /// <summary>
    /// Returns a boxed representation of the given value, cached between calls.
    /// </summary>
    public static object Intern(bool value) => value ? True : False;
    
    /// <summary>
    /// Returns a boxed representation of the given value, cached between calls.
    /// Be careful when using this method on arbitrary values, as once interned, the value will never get un-interned and will leak memory.
    /// Use only when you can prove it's beneficial to do so.
    /// </summary>
    public static object Intern(int value) => InternHelper<int>.Intern(value);
    
    /// <summary>
    /// Returns a boxed representation of the given value, cached between calls.
    /// Be careful when using this method on arbitrary values, as once interned, the value will never get un-interned and will leak memory.
    /// Use only when you can prove it's beneficial to do so.
    /// </summary>
    public static object Intern(float value) => InternHelper<float>.Intern(value);
    
    /// <summary>
    /// Tries to intern the given value if deemed profitable.
    /// Returns either the interned value, or the original if no interning occured.
    /// Be careful when using this method on arbitrary values, as once interned, the value will never get un-interned and will leak memory.
    /// Use only when you can prove it's beneficial to do so.
    /// </summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static object TryIntern(object value) {
        if (value is int i)
            return InternHelper<int>.Intern(i);
        if (value is float f)
            return InternHelper<float>.Intern(f);
        if (value is bool b)
            return Intern(b);
        if (value is string s)
            return string.Intern(s);

        return value;
    }
}

internal static class InternHelper<T> {
    private static readonly Dictionary<T, object> Cache = new();
    
    public static object Intern(T value) {
        return Cache.TryGetValue(value, out object result) ? result : Cache[value] = value;
    }
}
