using MonoMod;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

namespace Monocle;

public static class patch_Collide {
    // Patch various Collide methods which accept an IEnumerable<Entity> to fast-path for List<Entity>/Entity[] and not allocate.
    // We also expose new overloads accepting a List<Entity> and ReadOnlySpan<Entity>,
    // so that mods compiled against newer Everest will automatically dodge type checks in these perf-critical methods.
    
    [MonoModReplace]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Check(Entity a, IEnumerable<Entity> b) => First(a, b) != null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Check(Entity a, List<Entity> b) => First(a, CollectionsMarshal.AsSpan(b)) != null;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Check(Entity a, ReadOnlySpan<Entity> b) => First(a, b) != null;
        
    [MonoModReplace]
    public static Entity First(Entity a, IEnumerable<Entity> b) {
        if (TryGetSpan(b, out var span))
            return First(a, span);
            
        foreach (Entity entity in b)
            if (Collide.Check(a, entity))
                return entity;
            
        return null;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Entity First(Entity a, List<Entity> b) => First(a, CollectionsMarshal.AsSpan(b));
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Entity First(Entity a, ReadOnlySpan<Entity> b) {
        foreach (Entity entity in b)
            if (Collide.Check(a, entity))
                return entity;
            
        return null;
    }

    [MonoModReplace]
    public static List<Entity> All(Entity a, IEnumerable<Entity> b, List<Entity> into) {
        if (TryGetSpan(b, out var span))
            return All(a, span, into);
            
        foreach (Entity entity in b)
            if (Collide.Check(a, entity))
                into.Add(entity);
            
        return into;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<Entity> All(Entity a, List<Entity> b, List<Entity> into) => All(a, CollectionsMarshal.AsSpan(b), into);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<Entity> All(Entity a, ReadOnlySpan<Entity> b, List<Entity> into) {
        foreach (Entity entity in b)
            if (Collide.Check(a, entity))
                into.Add(entity);
            
        return into;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetSpan(IEnumerable<Entity> source, out ReadOnlySpan<Entity> span) {
        // Taken from https://github.com/dotnet/runtime/blob/b281500fa1f42732455f8d4f06bcb376d88cdfdd/src/libraries/System.Linq/src/System/Linq/Enumerable.cs#L44,
        // adjusted to check for lists first, as we expect those to be the most common.
        // Unsafe casts from the original method seem to not matter on .net7, though they do matter on newer runtimes.
        
        // Use `GetType() == typeof(...)` rather than `is` to avoid cast helpers.  This is measurably cheaper
        // but does mean we could end up missing some rare cases where we could get a span but don't (e.g. a DerivedFromEntity[]
        // masquerading as an Entity[]).  That's an acceptable tradeoff.
        
        bool result = true;
        if (source.GetType() == typeof(List<Entity>)) {
            span = CollectionsMarshal.AsSpan((List<Entity>)source);
        } else if (source.GetType() == typeof(Entity[])) {
            span = (Entity[])source;
        }
        else {
            span = default;
            result = false;
        }
 
        return result;
    }
}