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
                                            masked.Mask[i++] = 0; // No tile
                                            break;
                                        case '1':
                                            masked.Mask[i++] = 1; // Tile
                                            break;
                                        case 'x':
                                        case 'X':
                                            masked.Mask[i++] = 2; // Any
                                            break;
                                        case 'y':
                                        case 'Y':
                                            masked.Mask[i++] = 3; // Not this tile
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
                    if (a.Mask[i] >= 10) {
                        aFilters++;
                    }
                    if (b.Mask[i] >= 10) {
                        bFilters++;
                    }

                    if (a.Mask[i] == 3) {
                        aNots++;
                    }
                    if (b.Mask[i] == 3) {
                        bNots++;
                    }

                    if (a.Mask[i] == 2) {
                        aAnys++;
                    }
                    if (b.Mask[i] == 2) {
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
            if (char.IsLower(c))
                // Take the letter, convert it into a number from 10 to 36
                return (byte) ((c - 'a') + 10);
            else if (char.IsUpper(c))
                // Take the letter, convert it into a number from 37 to 63
                return (byte) ((c - 'A') + 37);
            throw new ArgumentException("Custom tileset mask filter must be an uppercase or lowercase letter.");
        }

        [MonoModIgnore]
        private extern char GetTile(VirtualMap<char> mapData, int x, int y, Rectangle forceFill, char forceID, Behaviour behaviour);

        [MonoModIgnore]
        private extern bool IsEmpty(char id);

        [MonoModIgnore]
        private extern bool CheckTile(patch_TerrainType set, VirtualMap<char> mapData, int x, int y, Rectangle forceFill, Behaviour behaviour);

        [MonoModIgnore]
        private extern bool CheckForSameLevel(int x1, int y1, int x2, int y2);

        private extern patch_Tiles orig_TileHandler(VirtualMap<char> mapData, int x, int y, Rectangle forceFill, char forceID, Behaviour behaviour);
        private patch_Tiles TileHandler(VirtualMap<char> mapData, int x, int y, Rectangle forceFill, char forceID, Behaviour behaviour) {
            char tile = GetTile(mapData, x, y, forceFill, forceID, behaviour);
            if (IsEmpty(tile))
                return null;

            // Satisfies error handling for the orig_ method too.
            if (!lookup.TryGetValue(tile, out patch_TerrainType terrainType)) {
                throw new AutotilerException($"Level contains a tileset with an id of '{tile}' that is not defined.") {
                    Source = "TileHandler",
                    ID = tile,
                    X = x,
                    Y = y
                };
            }

            int width = terrainType.ScanWidth;
            int height = terrainType.ScanHeight;

            if (terrainType.CustomFills == null && width == 3 && height == 3 && terrainType.whitelists.Count == 0 && terrainType.blacklists.Count == 0) {
                return orig_TileHandler(mapData, x, y, forceFill, forceID, behaviour); // Default tileset, default handler.
            }

            bool fillTile = true;
            char[] adjacent = new char[width * height];

            int idx = 0;
            for (int yOffset = 0; yOffset < height; yOffset++) {
                for (int xOffset = 0; xOffset < width; xOffset++) {
                    // Integer division will effectively truncate the "middle" (this) tile
                    bool tilePresent = TryGetTile(terrainType, mapData, x + (xOffset - width / 2), y + (yOffset - height / 2), forceFill, forceID, behaviour, out char adjTile);
                    if (!tilePresent && behaviour.EdgesIgnoreOutOfLevel && !CheckForSameLevel(x, y, x + xOffset, y + yOffset)) {
                        tilePresent = true;
                    }
                    adjacent[idx++] = adjTile;
                    if (!tilePresent)
                        fillTile = false;
                }
            }

            if (fillTile) {
                if (terrainType.CustomFills != null) {
                    // Start at depth of 1 since first layer has already been checked by masks.
                    int depth = GetDepth(terrainType, mapData, x, y, forceFill, behaviour, 1);
                    return terrainType.CustomFills[depth - 1];
                } else {
                    if (CheckCross(terrainType, mapData, x, y, forceFill, behaviour, 1 + width / 2, 1 + height / 2))
                        return terrainType.Center;

                    return terrainType.Padded;
                }
            }

            foreach (patch_Masked item in terrainType.Masked) {
                bool matched = true;
                for (int i = 0; i < width * height; i++) {
                    if (item.Mask[i] == 2) // Matches Any
                        continue;

                    if (item.Mask[i] == 1 && IsEmpty(adjacent[i])) {
                        matched = false;
                        break;
                    }

                    if (item.Mask[i] == 0 && !IsEmpty(adjacent[i])) {
                        matched = false;
                        break;
                    }

                    if (item.Mask[i] == 3 && adjacent[i] == tile) {
                        matched = false;
                        break;
                    }

                    if (terrainType.blacklists.Count > 0) {
                        if (terrainType.blacklists.ContainsKey(item.Mask[i]) && terrainType.blacklists[item.Mask[i]].Contains(adjacent[i].ToString())) {
                            matched = false;
                            break;
                        }

                    }

                    if (terrainType.whitelists.Count > 0) {
                        if (terrainType.whitelists.ContainsKey(item.Mask[i]) && !terrainType.whitelists[item.Mask[i]].Contains(adjacent[i].ToString())) {
                            matched = false;
                            break;
                        }

                    }

                }

                if (matched)
                    return item.Tiles;
            }

            return null;
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

        private int GetDepth(patch_TerrainType terrainType, VirtualMap<char> mapData, int x, int y, Rectangle forceFill, Behaviour behaviour, int depth) {
            int searchX = depth + terrainType.ScanWidth / 2;
            int searchY = depth + terrainType.ScanHeight / 2;

            if (CheckCross(terrainType, mapData, x, y, forceFill, behaviour, searchX, searchY) && depth < terrainType.CustomFills.Count)
                return GetDepth(terrainType, mapData, x, y, forceFill, behaviour, ++depth);

            return depth;
        }

        private bool CheckCross(patch_TerrainType terrainType, VirtualMap<char> mapData, int x, int y, Rectangle forceFill, Behaviour behaviour, int width, int height) {
            if (behaviour.PaddingIgnoreOutOfLevel)
                return (CheckTile(terrainType, mapData, x - width, y, forceFill, behaviour) || !CheckForSameLevel(x, y, x - width, y)) &&
                    (CheckTile(terrainType, mapData, x + width, y, forceFill, behaviour) || !CheckForSameLevel(x, y, x + width, y)) &&
                    (CheckTile(terrainType, mapData, x, y - height, forceFill, behaviour) || !CheckForSameLevel(x, y, x, y - height)) &&
                    (CheckTile(terrainType, mapData, x, y + height, forceFill, behaviour) || !CheckForSameLevel(x, y, x, y + height));
            else
                return CheckTile(terrainType, mapData, x - width, y, forceFill, behaviour) &&
                    CheckTile(terrainType, mapData, x + width, y, forceFill, behaviour) &&
                    CheckTile(terrainType, mapData, x, y - height, forceFill, behaviour) &&
                    CheckTile(terrainType, mapData, x, y + height, forceFill, behaviour);
        }

        public bool TryGetCustomDebris(out string path, char tiletype) {
            return !string.IsNullOrEmpty(path = lookup[tiletype].Debris);
        }

        // Required because TerrainType is private.
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

            [MonoModIgnore]
            public extern bool Ignore(char c);

            public extern void orig_ctor(char id);
            [MonoModConstructor]
            public void ctor(char id) {
                orig_ctor(id);

                whitelists = new Dictionary<byte, string>();
                blacklists = new Dictionary<byte, string>();
            }

        }

        // Required because Tiles is private.
        [MonoModIgnore]
        private class patch_Tiles {
            public List<MTexture> Textures;
            public List<string> OverlapSprites;
            public bool HasOverlays;

        }

        // Required because Masked is private.
        [MonoModIgnore]
        private class patch_Masked {
            public byte[] Mask;
            public patch_Tiles Tiles;

        }

    }
}
