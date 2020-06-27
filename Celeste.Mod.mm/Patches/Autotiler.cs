#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Celeste {
    class patch_Autotiler : Autotiler {

        private Dictionary<char, patch_TerrainType> lookup;
        private byte[] adjacent;

        public patch_Autotiler(string filename)
            : base(filename) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        private extern void orig_ReadInto(patch_TerrainType data, Tileset tileset, XmlElement xml);
        private void ReadInto(patch_TerrainType data, Tileset tileset, XmlElement xml) {
            if (xml.HasAttr("scanWidth")) {
                int scanWidth = xml.AttrInt("scanWidth");

                if (scanWidth < 0)
                    throw new Exception("Tileset scan width cannot be negative.");
                if (scanWidth % 2 == 0)
                    throw new Exception("Tileset scan width cannot be an even number.");

                data.ScanHeight = scanWidth;
            } else
                data.ScanWidth = 3;

            if (xml.HasAttr("scanHeight")) {
                int scanHeight = xml.AttrInt("scanHeight");

                if (scanHeight < 0)
                    throw new Exception("Tileset scan height cannot be negative.");
                if (scanHeight % 2 == 0)
                    throw new Exception("Tileset scan height cannot be an even number.");

                data.ScanHeight = scanHeight;
            } else
                data.ScanHeight = 3;

            XmlNodeList fills = xml.SelectNodes("set[starts-with(@mask, 'fill')]"); // Faster to just use an attr, but this is cleaner & easier to use.
            if (fills != null && fills.Count > 0) {

                data.CustomFills = new List<patch_Tiles>();
                for (int i = 0; i < fills.Count; i++)
                    data.CustomFills.Add(new patch_Tiles());
            }

            if (data.CustomFills == null && data.ScanWidth == 3 && data.ScanHeight == 3)
                orig_ReadInto(data, tileset, xml);
            else
                ReadIntoCustomTemplate(data, tileset, xml);

            if (xml.HasAttr("sound"))
                SurfaceIndex.TileToIndex[xml.AttrChar("id")] = xml.AttrInt("sound");

            if (xml.HasAttr("debris"))
                data.Debris = xml.Attr("debris");
        }

        private void ReadIntoCustomTemplate(patch_TerrainType data, Tileset tileset, XmlElement xml) {
            foreach (XmlNode child in xml) {
                if (child is XmlElement) {
                    XmlElement node = child as XmlElement;

                    string text = node.Attr("mask");
                    patch_Tiles tiles;
                    if (text == "center") {
                        if (data.CustomFills != null)
                            Logger.Log(LogLevel.Warn, "Autotiler", "\"Center\" tiles will not be used if Custom Fills are present.");

                        tiles = data.Center;
                    } else if (text == "padding") {
                        if (data.CustomFills != null)
                            Logger.Log(LogLevel.Warn, "Autotiler", "\"Padding\" tiles will not be used if Custom Fills are present.");

                        tiles = data.Padded;
                    } else if (text.StartsWith("fill")) {
                        tiles = data.CustomFills[int.Parse(text.Substring(4))];
                    } else {
                        patch_Masked masked = new patch_Masked();
                        masked.Mask = new byte[data.ScanWidth * data.ScanHeight];
                        tiles = masked.Tiles;

                        try {
                            // Allows for spacer characters like '-' in the xml
                            int maskIdx = 0;
                            foreach (char c in text) {
                                switch (c) {
                                    case '0':
                                        masked.Mask[maskIdx++] = 0; // No tile
                                        break;
                                    case '1':
                                        masked.Mask[maskIdx++] = 1; // Tile
                                        break;
                                    case 'x':
                                    case 'X':
                                        masked.Mask[maskIdx++] = 2; // Any
                                        break;
                                }
                            }
                        } catch (IndexOutOfRangeException e) {
                            throw new IndexOutOfRangeException("Mask size is greater than the size specified by scanWidth and scanHeight.", e);
                        }
                        data.Masked.Add(masked);
                    }

                    foreach (string tile in node.Attr("tiles").Split(';')) {
                        string[] subtexture = tile.Split(',');
                        int x = int.Parse(subtexture[0]);
                        int y = int.Parse(subtexture[1]);

                        tiles.Textures.Add(tileset[x, y]);
                    }

                    if (node.HasAttr("sprites")) {
                        foreach (string sprites in node.Attr("sprites").Split(','))
                            tiles.OverlapSprites.Add(sprites);
                        tiles.HasOverlays = true;
                    }
                }
            }

            data.Masked.Sort((patch_Masked a, patch_Masked b) => {
                int aAnys = 0;
                int bAnys = 0;
                for (int i = 0; i < data.ScanWidth * data.ScanHeight; i++) {
                    if (a.Mask[i] == 2) {
                        aAnys++;
                    }
                    if (b.Mask[i] == 2) {
                        bAnys++;
                    }
                }
                return aAnys - bAnys;
            });
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
            patch_TerrainType terrainType = lookup[tile];

            int width = terrainType.ScanWidth;
            int height = terrainType.ScanHeight;

            if (terrainType.CustomFills == null && width == 3 && height == 3) {
                return orig_TileHandler(mapData, x, y, forceFill, forceID, behaviour); // Default tileset, default handler.
            }

            bool fillTile = true;
            adjacent = new byte[width * height];

            int idx = 0;
            for (int yOffset = 0; yOffset < height; yOffset++) {
                for (int xOffset = 0; xOffset < width; xOffset++) {
                    // Integer division will effectively truncate the "middle" tile
                    bool tilePresent = CheckTile(terrainType, mapData, x + (xOffset - width / 2), y + (yOffset - height / 2), forceFill, behaviour);
                    if (!tilePresent && behaviour.EdgesIgnoreOutOfLevel && !CheckForSameLevel(x, y, x + xOffset, y + yOffset)) {
                        tilePresent = true;
                    }
                    adjacent[idx++] = (byte) (tilePresent ? 1 : 0);
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
                    if (!CheckCross(terrainType, mapData, x, y, forceFill, behaviour, 1 + width / 2, 1 + height / 2))
                        return terrainType.Center;

                    return terrainType.Padded;
                }
            }

            foreach (patch_Masked item in terrainType.Masked) {
                bool matched = true;
                for (int i = 0; i < width * height; i++) {
                    if (item.Mask[i] == 2) // Matches Any
                        continue;

                    if (item.Mask[i] != adjacent[i]) {
                        matched = false;
                        break;
                    }
                }
                if (matched)
                    return item.Tiles;
            }

            return null;
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
            public List<patch_Masked> Masked;
            public patch_Tiles Center;
            public patch_Tiles Padded;

            public string Debris;

            public int ScanWidth;
            public int ScanHeight;
            public List<patch_Tiles> CustomFills;

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
