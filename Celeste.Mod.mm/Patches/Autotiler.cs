#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod;
using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Xml;

namespace Celeste {
    class patch_Autotiler : Autotiler {

        private Dictionary<char, patch_TerrainType> lookup;

        public patch_Autotiler(string filename)
            : base(filename) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern Generated orig_GenerateOverlay(char id, int x, int y, int tilesX, int tilesY, VirtualMap<char> mapData);
        public new Generated GenerateOverlay(char id, int x, int y, int tilesX, int tilesY, VirtualMap<char> mapData) {
            // be sure our overlay doesn't cross null segments, because they might just not get rendered there.
            for (int i = x; i < x + tilesX; i = (i / 50 + 1) * 50) {
                for (int j = y; j < y + tilesY; j = (j / 50 + 1) * 50) {
                    if (!mapData.AnyInSegmentAtTile(i, j)) {
                        mapData[i, j] = mapData.EmptyValue;
                    }
                }
            }

            return orig_GenerateOverlay(id, x, y, tilesX, tilesY, mapData);
        }

        private extern void orig_ReadInto(patch_TerrainType data, Tileset tileset, XmlElement xml);
        private void ReadInto(patch_TerrainType data, Tileset tileset, XmlElement xml) {
            if (xml.HasAttr("scanWidth")) {
                int scanWidth = xml.AttrInt("scanWidth");
                if (scanWidth <= 0 || scanWidth % 2 == 0)
                    throw new ArgumentException($"Invalid scan width for tileset with id '{data.ID}'. Scan width must be a positive, odd integer.");

                data.ScanWidth = scanWidth;
            } else
                data.ScanWidth = 3;

            if (xml.HasAttr("scanHeight")) {
                int scanHeight = xml.AttrInt("scanHeight");

                if (scanHeight <= 0 || scanHeight % 2 == 0)
                    throw new ArgumentException($"Invalid scan height for tileset with id '{data.ID}'. Tileset scan height must be a positive, odd integer.");

                data.ScanHeight = scanHeight;
            } else
                data.ScanHeight = 3;

            XmlNodeList fills = xml.SelectNodes("set[starts-with(@mask, 'fill')]"); // Faster to ask for an attr on the tileset node, but this is cleaner & easier to use.
            if (fills != null && fills.Count > 0) {

                data.CustomFills = new List<patch_Tiles>();
                for (int i = 0; i < fills.Count; i++)
                    data.CustomFills.Add(new patch_Tiles());
            }

            if (data.CustomFills == null && data.ScanWidth == 3 && data.ScanHeight == 3 && !xml.HasChild("define")) // ReadIntoCustomTemplate can handle vanilla templates but meh
                orig_ReadInto(data, tileset, xml);
            else {
                Logger.Log(LogLevel.Debug, "Autotiler", $"Reading template for tileset with id '{data.ID}', scan height {data.ScanHeight}, and scan width {data.ScanWidth}.");
                ReadIntoCustomTemplate(data, tileset, xml);
            }

            if (xml.HasAttr("soundPath") && xml.HasAttr("sound")) { // Could accommodate for no sound attr, but requiring it should improve clarity on user's end 
                SurfaceIndex.TileToIndex[xml.AttrChar("id")] = xml.AttrInt("sound");
                patch_SurfaceIndex.IndexToCustomPath[xml.AttrInt("sound")] = (xml.Attr("soundPath").StartsWith("event:/") ? "" : "event:/") + xml.Attr("soundPath");
            } else if (xml.HasAttr("sound")) {
                SurfaceIndex.TileToIndex[xml.AttrChar("id")] = xml.AttrInt("sound");
            } else if (!SurfaceIndex.TileToIndex.ContainsKey(xml.AttrChar("id"))) {
                SurfaceIndex.TileToIndex[xml.AttrChar("id")] = 0; // fall back to no sound
            }

            if (xml.HasAttr("debris"))
                data.Debris = xml.Attr("debris");
        }

        private void ReadIntoCustomTemplate(patch_TerrainType data, Tileset tileset, XmlElement xml) {
            foreach (XmlNode child in xml) {
                if (child is XmlElement node) {
                    if (node.Name == "set") { // Potential somewhat breaking change, although there is no reason for another name to have been used.
                        string text = node.Attr("mask");
                        patch_Tiles tiles;
                        if (text == "center") {
                            if (data.CustomFills != null)
                                Logger.Log(LogLevel.Warn, "Autotiler", $"\"Center\" tiles for tileset with id '{data.ID}' will not be used if custom fills are present.");

                            tiles = data.Center;
                        } else if (text == "padding") {
                            if (data.CustomFills != null)
                                Logger.Log(LogLevel.Warn, "Autotiler", $"\"Padding\" tiles for tileset with id '{data.ID}' will not be used if custom fills are present.");

                            tiles = data.Padded;
                        } else if (text.StartsWith("fill")) {
                            tiles = data.CustomFills[int.Parse(text.Substring(4))];
                        } else {
                            patch_Masked masked = new patch_Masked();
                            masked.Mask = new byte[data.ScanWidth * data.ScanHeight];
                            tiles = masked.Tiles;

                            try {
                                // Allows for spacer characters like '-' in the xml
                                int i = 0;
                                foreach (char c in text) {
                                    switch (c) {
                                        case '0':
                                            masked.Mask[i++] = patch_Masked.TileEmptyMask;
                                            break;
                                        case '1':
                                            masked.Mask[i++] = patch_Masked.TilePresentMask;
                                            break;
                                        case 'x':
                                        case 'X':
                                            masked.Mask[i++] = patch_Masked.AnyMask;
                                            break;
                                        case 'y':
                                        case 'Y':
                                            masked.Mask[i++] = patch_Masked.NotThisTileMask;
                                            break;
                                        case 'z':
                                        case 'Z':
                                            break; // Reserved
                                        default: // Custom filters
                                            if (char.IsLetter(c))
                                                masked.Mask[i++] = GetByteLookup(c);
                                            break;
                                        /* 
                                         * Error handling for characters that don't exist in a defined filter could be added,
                                         * but is slightly more likely to break old custom tilesets if someone has defined a mask that containes nonstandard spacers (usually '-')
                                        */
                                    }
                                }
                            } catch (IndexOutOfRangeException e) {
                                throw new IndexOutOfRangeException($"Mask size in tileset with id '{data.ID}' is greater than the size specified by scanWidth and scanHeight (defaults to 3x3).", e);
                            }
                            data.Masked.Add(masked);
                        }

                        foreach (string tile in node.Attr("tiles").Split(';')) {
                            string[] subtexture = tile.Split(',');
                            int x = int.Parse(subtexture[0]);
                            int y = int.Parse(subtexture[1]);

                            try {
                                tiles.Textures.Add(tileset[x, y]);
                            } catch (IndexOutOfRangeException e) {
                                throw new IndexOutOfRangeException($"Tileset with id '{data.ID}' missing tile at ({x}, {y}).", e);
                            }
                        }

                        if (node.HasAttr("sprites")) {
                            foreach (string sprites in node.Attr("sprites").Split(','))
                                tiles.OverlapSprites.Add(sprites);
                            tiles.HasOverlays = true;
                        }

                    } else if (node.Name == "define") {
                        byte id = GetByteLookup(node.AttrChar("id"));
                        string filter = node.Attr("filter");

                        if (node.AttrBool("ignore"))
                            data.blacklists[id] = filter;
                        else
                            data.whitelists[id] = filter;
                    }
                }
            }

            data.Masked.Sort((patch_Masked a, patch_Masked b) => {
                // Sorts the masks to give preference to more specific masks.
                // Order is Custom Filters -> "Not This" -> "Any" -> Everything else
                int aFilters = 0;
                int bFilters = 0;
                int aNots = 0;
                int bNots = 0;
                int aAnys = 0;
                int bAnys = 0;
                for (int i = 0; i < data.ScanWidth * data.ScanHeight; i++) {
                    if (a.Mask[i] >= patch_Masked.CustomFilterMaskStart) {
                        aFilters++;
                    }
                    if (b.Mask[i] >= patch_Masked.CustomFilterMaskStart) {
                        bFilters++;
                    }

                    if (a.Mask[i] == patch_Masked.NotThisTileMask) {
                        aNots++;
                    }
                    if (b.Mask[i] == patch_Masked.NotThisTileMask) {
                        bNots++;
                    }

                    if (a.Mask[i] == patch_Masked.AnyMask) {
                        aAnys++;
                    }
                    if (b.Mask[i] == patch_Masked.AnyMask) {
                        bAnys++;
                    }
                }
                if (aFilters > 0 || bFilters > 0)
                    return aFilters - bFilters;

                if (aNots > 0 || bNots > 0)
                    return aNots - bNots;

                return aAnys - bAnys;
            });
        }

        private byte GetByteLookup(char c) {
            // Because of how the below code converts chars to numbers, only ascii values are safe...
            if (char.IsAscii(c)) {
                if (char.IsLower(c))
                    // Take the letter, convert it into a number from 10 to 36
                    return (byte) ((c - 'a') + patch_Masked.CustomFilterMaskStart);
                
                if (char.IsUpper(c))
                    // Take the letter, convert it into a number from 37 to 63
                    return (byte) ((c - 'A') + patch_Masked.CustomFilterMaskCapitalLetterStart);
            }

            throw new ArgumentException("Custom tileset mask filter must be an ASCII uppercase or lowercase letter.");
        }

        [MonoModIgnore]
        private extern char GetTile(VirtualMap<char> mapData, int x, int y, Rectangle forceFill, char forceID, Behaviour behaviour);

        [MonoModIgnore]
        private extern bool IsEmpty(char id);

        [MonoModIgnore]
        private extern bool CheckTile(patch_TerrainType set, VirtualMap<char> mapData, int x, int y, Rectangle forceFill, Behaviour behaviour);

        [MonoModIgnore]
        private extern bool CheckForSameLevel(int x1, int y1, int x2, int y2);

        // While this method is no longer used, we still need to keep it around for backwards compat.
        // All hooks on orig_TileHandler would've needed to duplicate the behaviour for TileHandler anyway to stay compatible with custom masks,
        // so this should be safe.
        [Obsolete("Never called, all code paths use TileHandler now")]
        private extern patch_Tiles orig_TileHandler(VirtualMap<char> mapData, int x, int y, Rectangle forceFill, char forceID, Behaviour behaviour);
        private patch_Tiles TileHandler(VirtualMap<char> mapData, int x, int y, Rectangle forceFill, char forceID, Behaviour behaviour) {
            char tile = GetTile(mapData, x, y, forceFill, forceID, behaviour);
            if (IsEmpty(tile))
                return null;

            // Satisfies error handling for the orig_ method too.
            if (!lookup.TryGetValue(tile, out patch_TerrainType terrainType)) {
                Logger.Log(LogLevel.Error, "Autotiler", $"Undefined tile id '{tile}' at ({x}, {y})");
                return new patch_Tiles {
                    Textures = { ((patch_Atlas) GFX.Game).GetFallback() },
                };
            }

            int width = terrainType.ScanWidth;
            int height = terrainType.ScanHeight;

            bool fillTile = true;
            // Stores information about adjacent tiles, flattened.
            // This needs to be an array instead of stackalloc'd span, as we'll use it to construct a EquatableCharArray later
            char[] adjacent = terrainType.GetSharedAdjacentBuffer();
            // Stores information whether adjacent tiles are present or not, taking into consideration the 'ignores' field.
            // Used for '1' and '0' masks.
            Span<bool> adjacentPresent = stackalloc bool[adjacent.Length];
            
            // Calculate the level that contains this tile, so that we can quickly check if the neighbouring tiles are in the same level.
            Rectangle levelBounds = behaviour.EdgesIgnoreOutOfLevel ? GetContainingLevelBounds(x, y) : default;

            int idx = 0;
            for (int yOffset = 0; yOffset < height; yOffset++) {
                for (int xOffset = 0; xOffset < width; xOffset++) {
                    // Integer division will effectively truncate the "middle" (this) tile
                    bool tilePresent = TryGetTile(terrainType, mapData, x + (xOffset - width / 2), y + (yOffset - height / 2), forceFill, forceID, behaviour, out char adjTile);

                    if (!tilePresent && behaviour.EdgesIgnoreOutOfLevel && !levelBounds.Contains(x + (xOffset - width / 2), y + (yOffset - height / 2))) {
                        tilePresent = true;
                    }

                    adjacentPresent[idx] = tilePresent;
                    adjacent[idx++] = adjTile;
                    
                    if (!tilePresent)
                        fillTile = false;
                }
            }

            if (fillTile) {
                if (terrainType.CustomFills != null) {
                    // Start at depth of 1 since first layer has already been checked by masks.
                    int depth = GetDepth(terrainType, mapData, x, y, forceFill, behaviour, 1, levelBounds);
                    
                    return terrainType.CustomFills[depth - 1];
                }

                if (CheckCross(terrainType, mapData, x, y, forceFill, behaviour, 1 + width / 2, 1 + height / 2, levelBounds))
                    return terrainType.Center;

                return terrainType.Padded;
            }

            // If we already checked the same set of adjacent tiles, the exact same mask will match again.
            // This means we can easily cache this.
            if (terrainType.GetCachedMaskOrNull(adjacent) is { } cachedMask) {
                return cachedMask.Tiles;
            }

            foreach (patch_Masked item in terrainType.Masked) {
                bool matched = true;
                byte[] mask = item.Mask;
                
                // mask.Length as well as adjacent.Length should always be equal to width * height
                // To get rid of JIT-generated bounds checks:
                // - We'll do a normal for loop through the `mask`, to get rid of bounds checks for `mask`
                // - And this additional check here, to get rid of bounds checks for `adjacent`
                if (adjacent.Length < mask.Length)
                    continue;
                
                for (int i = 0; i < mask.Length; i++) {
                    bool thisTileMatched = mask[i] switch {
                        patch_Masked.AnyMask => true,
                        patch_Masked.TilePresentMask => adjacentPresent[i],
                        patch_Masked.TileEmptyMask => !adjacentPresent[i],
                        patch_Masked.NotThisTileMask => adjacent[i] != tile,
                        var customMask => IsCustomMaskMatch(terrainType, customMask, adjacent[i])
                    };

                    if (!thisTileMatched) {
                        matched = false;
                        break;
                    }
                }

                if (matched) {
                    terrainType.CacheMask(adjacent, item);
                    return item.Tiles;
                }
            }

            return null;
        }
        
        /// <summary>
        /// Checks whether the given <paramref name="tile"/> matches the custom <paramref name="mask"/>. 
        /// </summary>
        private static bool IsCustomMaskMatch(patch_TerrainType terrainType, byte mask, char tile) {
            if (terrainType.blacklists.Count > 0) {
                if (terrainType.blacklists.TryGetValue(mask, out string value) && value.Contains(tile, StringComparison.Ordinal)) {
                    return false;
                }

            }

            if (terrainType.whitelists.Count > 0) {
                if (terrainType.whitelists.TryGetValue(mask, out string value) && !value.Contains(tile, StringComparison.Ordinal)) {
                    return false;
                }

            }

            return true;
        }
        
        /// <summary>
        /// Gets the bounds of the level that contains the given point.
        /// This loops through all levels (rooms) in the map.
        /// </summary>
        private Rectangle GetContainingLevelBounds(int x, int y) {
            foreach (Rectangle rectangle in LevelBounds)
            {
                if (rectangle.Contains(x, y))
                {
                    return rectangle;
                }
            }

            return new(x, y, 1, 1);
        }

        // Replaces "CheckTile" in modded TileHandler method.
        private bool TryGetTile(patch_TerrainType set, VirtualMap<char> mapData, int x, int y, Rectangle forceFill, char forceID, Behaviour behaviour, out char tile) {
            tile = '0';
            if (forceFill.Contains(x, y)) {
                tile = forceID;
                return true;
            }

            if (mapData == null) { // Not entirely sure how this should be handled best.
                return behaviour.EdgesExtend;
            }

            if (x >= 0 && y >= 0 && x < mapData.Columns && y < mapData.Rows) {
                tile = mapData[x, y];
                return !IsEmpty(tile) && !set.Ignore(tile);
            }

            if (!behaviour.EdgesExtend) {
                return false;
            }

            tile = mapData[Calc.Clamp(x, 0, mapData.Columns - 1), Calc.Clamp(y, 0, mapData.Rows - 1)];
            return !IsEmpty(tile) && !set.Ignore(tile);
        }

        private int GetDepth(patch_TerrainType terrainType, VirtualMap<char> mapData, int x, int y, Rectangle forceFill, Behaviour behaviour, int depth, Rectangle levelBounds) {
            int searchX = depth + terrainType.ScanWidth / 2;
            int searchY = depth + terrainType.ScanHeight / 2;

            if (CheckCross(terrainType, mapData, x, y, forceFill, behaviour, searchX, searchY, levelBounds) && depth < terrainType.CustomFills.Count)
                return GetDepth(terrainType, mapData, x, y, forceFill, behaviour, ++depth, levelBounds);

            return depth;
        }
        
        private bool CheckCross(patch_TerrainType terrainType, VirtualMap<char> mapData, int x, int y, Rectangle forceFill, Behaviour behaviour, int width, int height, Rectangle levelBounds) {
            if (behaviour.PaddingIgnoreOutOfLevel) {
                return (CheckTile(terrainType, mapData, x - width, y, forceFill, behaviour) || !levelBounds.Contains(x - width, y)) &&
                       (CheckTile(terrainType, mapData, x + width, y, forceFill, behaviour) || !levelBounds.Contains(x + width, y)) &&
                       (CheckTile(terrainType, mapData, x, y - height, forceFill, behaviour) || !levelBounds.Contains(x, y - height)) &&
                       (CheckTile(terrainType, mapData, x, y + height, forceFill, behaviour) || !levelBounds.Contains(x, y + height));
            }

            return CheckTile(terrainType, mapData, x - width, y, forceFill, behaviour) &&
                   CheckTile(terrainType, mapData, x + width, y, forceFill, behaviour) &&
                   CheckTile(terrainType, mapData, x, y - height, forceFill, behaviour) &&
                   CheckTile(terrainType, mapData, x, y + height, forceFill, behaviour);
        }

        public bool TryGetCustomDebris(out string path, char tiletype) {
            return !string.IsNullOrEmpty(path = lookup.TryGetValue(tiletype, out patch_TerrainType t) ? t.Debris : "");
        }

        private class patch_TerrainType {
            public char ID;
            public List<patch_Masked> Masked;
            public patch_Tiles Center;
            public patch_Tiles Padded;

            public string Debris;

            public int ScanWidth;
            public int ScanHeight;
            public List<patch_Tiles> CustomFills;

            public Dictionary<byte, string> whitelists;
            public Dictionary<byte, string> blacklists;

            // Cached shared buffer for GetSharedAdjacentBuffer
            private char[] _adjacentBuffer;
            // Cache for GetCachedMaskOrNull
            private Dictionary<EquatableCharArray, patch_Masked> _maskCache;

            /// <summary>
            /// Returns a shared char[] that can be used by the Autotiler to hold all adjacent tiles. 
            /// </summary>
            internal char[] GetSharedAdjacentBuffer() {
                int size = ScanWidth * ScanHeight;
                
                // if ScanWidth/ScanHeight got changed, we need to create a new buffer
                if (_adjacentBuffer is { } b && b.Length != size) {
                    _adjacentBuffer = null;
                }

                return _adjacentBuffer ??= new char[size];
            }

            /// <summary>
            /// Returns the <see cref="Masked"/> that is known to match the given adjacent tiles,
            /// or null if this combination of tiles hasn't been checked yet.
            /// </summary>
            internal patch_Masked GetCachedMaskOrNull(char[] adjacent) {
                return _maskCache.GetValueOrDefault(new(adjacent), null);
            }

            internal void CacheMask(char[] adjacent, patch_Masked mask) {
                char[] adjacentCopy = (char[])adjacent.Clone();

                _maskCache[new(adjacentCopy)] = mask;
            }

            [MonoModIgnore]
            public extern bool Ignore(char c);

            public extern void orig_ctor(char id);
            [MonoModConstructor]
            public void ctor(char id) {
                orig_ctor(id);

                whitelists = new Dictionary<byte, string>();
                blacklists = new Dictionary<byte, string>();
                _maskCache = new();
            }

            /// <summary>
            /// A wrapper over a char[], which allows it to be used as a dictionary key to perform a SequenceEqual comparison.
            /// This allows us to use a shared char[] to index the dict, without having to allocate a temporary string instance.
            /// </summary>
            private readonly struct EquatableCharArray : IEquatable<EquatableCharArray> {
                private readonly char[] Data;

                public EquatableCharArray(char[] data) {
                    Data = data;
                }
                
                public bool Equals(EquatableCharArray other) {
                    return Data.AsSpan().SequenceEqual(other.Data);
                }

                public override int GetHashCode() => string.GetHashCode(Data);
                    
                public override bool Equals(object obj)
                    => obj is EquatableCharArray other && Equals(other);

                public static bool operator ==(EquatableCharArray left, EquatableCharArray right) {
                    return left.Equals(right);
                }

                public static bool operator !=(EquatableCharArray left, EquatableCharArray right) {
                    return !(left == right);
                }
            }
        }

        // Required because Tiles is private.
        [MonoModIgnore]
        private class patch_Tiles {
            public List<MTexture> Textures;
            public List<string> OverlapSprites;
            public bool HasOverlays;

        }
        
        // Add additional constants to clean up code
        private class patch_Masked {
            [MonoModIgnore]
            public byte[] Mask;
            
            [MonoModIgnore]
            public patch_Tiles Tiles;

            public const byte TileEmptyMask = 0;
            public const byte TilePresentMask = 1;
            public const byte AnyMask = 2;
            public const byte NotThisTileMask = 3;

            /// <summary>
            /// The first mask type used for custom filters.
            /// </summary>
            internal const byte CustomFilterMaskStart = 10;
            
            /// <summary>
            /// The first mask type used for custom filters with capital letters.
            /// </summary>
            internal const byte CustomFilterMaskCapitalLetterStart = CustomFilterMaskStart + 27;
        }

    }
}
