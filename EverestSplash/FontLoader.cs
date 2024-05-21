using EverestSplash.SDL2;
using System;
using System.Collections.Generic;
using System.IO;

namespace EverestSplash;

/// <summary>
/// A simple FontLoader for BMFonts, documented here: https://www.angelcode.com/products/bmfont/documentation.html
/// It uses SDL_Textures for performance reasons.
/// </summary>
public class FontLoader : IDisposable {
    // A file describing the font layout
    private readonly Stream fontData;
    // An SDL surface containing the font atlas
    private readonly STexture fontTexture;

    private FontInfo fontInfo;

    private Dictionary<char, CharSize> CharSizes = new();

    /// <summary>
    /// Creates a new FontLoader instance.
    /// </summary>
    /// <param name="fontData">The file describing the fontSurface layout.</param>
    /// <param name="fontTexture">An SDL texture containing the font atlas.</param>
    public FontLoader(Stream fontData, STexture fontTexture) {
        this.fontData = fontData;
        this.fontTexture = fontTexture;
        ReadBin();
        Console.WriteLine($"Font {fontInfo.name} loaded");
    }

    #region Data Parsing
    // Reads the binary file, using the BMF 3 format
    private void ReadBin() {
        fontInfo = new();
        using (BinaryReader reader = new(fontData)) {
            // FIRST BLOCK: Info
            if (!VerifyHeader(reader.ReadBytes(4)))
                throw new InvalidDataException("Invalid header!");
            AssertFontFeature(reader.ReadByte(), 1); // Verify block ID
            reader.ReadUInt32(); // Ignore block size
            fontInfo.fontSize = reader.ReadInt16();
            reader.ReadByte(); // skip bitField
            AssertFontFeature(reader.ReadByte()); // skip charSet
            AssertFontFeature(reader.ReadUInt16(), 100); // skip strechH
            reader.ReadByte(); // skip aa
            for (int i = 0; i < 4; i++) // skip padding Up/Right/Down/Left
                AssertFontFeature(reader.ReadByte());
            fontInfo.spacingHoriz = reader.ReadByte();
            fontInfo.spacingVert = reader.ReadByte();
            AssertFontFeature(reader.ReadByte()); // skip outline
            fontInfo.name = reader.ReadSZString();

            // SECOND BLOCK: Common
            AssertFontFeature(reader.ReadByte(), 2); // Verify block ID
            reader.ReadUInt32(); // Ignore block size
            
            fontInfo.lineHeight = reader.ReadUInt16();
            fontInfo.baseHeight = reader.ReadUInt16();
            reader.ReadUInt16(); // skip scaleW
            reader.ReadUInt16(); // skip scaleH
            AssertFontFeature(reader.ReadUInt16(), 1); // skip pages, only a single page is supported
            AssertFontFeature(reader.ReadByte()); // skip bitField
            AssertFontFeature(reader.ReadByte(), 0); // skip alphaChnl
            for (int i = 0; i < 3; i++) // skip red/green/blue chnl
                AssertFontFeature(reader.ReadByte(), 4); // 4 means no relevant data
            
            // THIRD BLOCK: Pages
            AssertFontFeature(reader.ReadByte(), 3); // Verify block ID
            uint pageBlockSize = reader.ReadUInt32();
            reader.ReadBytes((int)pageBlockSize); // Skip over it, multiple pages are not supported
            
            // FOURTH BLOCK: Chars
            AssertFontFeature(reader.ReadByte(), 4); // Verify block ID
            uint charBlockSize = reader.ReadUInt32();
            const int singleCharBlockSize = 20;
            uint charCount = charBlockSize / singleCharBlockSize;
            for (int i = 0; i < charCount; i++) {
                CharSize charSize = new(
                    reader.ReadInt32(),   // chr
                    reader.ReadInt16(),    // x
                    reader.ReadInt16(),    // y
                    reader.ReadInt16(), // width
                    reader.ReadInt16(), // height
                    reader.ReadInt16(), // Xoffset
                    reader.ReadInt16(), // Yoffset
                    reader.ReadInt16()
                );
                AssertFontFeature(reader.ReadByte()); // skip page, 0 is assumed
                AssertFontFeature(reader.ReadByte(), 15); // skip chnl, 15 means all channels have data

                CharSizes.Add((char) charSize.Chr, charSize); // charSize.Chr is guaranteed to be a valid character
            }
            
            // FIFTH BLOCK: Kerning pairs
            // No-op, this block should not be there since its not supported, so its ignored
        }
    }
    
    private static bool VerifyHeader(byte[] header) { // header consists of BMF and 3 as the version
        if (header.Length != 4) return false;
        return header[0] == 'B' && header[1] == 'M' && header[2] == 'F' && header[3] == 3;
    }

    private static void AssertFontFeature(int feature, int expValue = 0) {
        if (feature != expValue)
            throw new InvalidDataException("Unsupported font feature detected!");
    }

    private struct FontInfo {
        public int fontSize; // The font size
        public int spacingHoriz; // Pixel spacing between characters
        public int spacingVert; // Pixel spacing between characters
        public string name; // The font name
        public int lineHeight; // The height of a line of text
        public int baseHeight; // The height at where the chars are placed (smaller than lineHeight)
    }

    private record CharSize(int Chr, short X, short Y, short Width, short Height, short Xoffset, short Yoffset, int Xadvance);
    #endregion

    #region SDL Rendering

    /// <summary>
    /// Draws a string into a renderer.
    /// </summary>
    /// <param name="text">The text to draw, all characters must be in the font.</param>
    /// <param name="renderer">A valid SDL2 renderer.</param>
    /// <param name="origin">The position where the text should be drawn.</param>
    // TODO: New line support
    public void DrawText(string text, IntPtr renderer, SDL.SDL_Point origin) {
        SDL.SDL_Rect srcRect = new() {
            x = 0,
            y = 0,
            w = 0,
            h = 0,
        };
        SDL.SDL_Rect dstRect = new() {
            x = 0,
            y = 0,
            w = 0,
            h = 0,
        };
        int cx = origin.x, cy = origin.y; // cursor x and y
        foreach (char chr in text) {
            if (!CharSizes.TryGetValue(chr, out CharSize? charSize))
                throw new InvalidOperationException("Tried to draw a nonexistent character");
            // Set up sizes
            srcRect.w = dstRect.w = charSize.Width;
            srcRect.h = dstRect.h = charSize.Height;
            
            // Set up source position
            srcRect.x = charSize.X;
            srcRect.y = charSize.Y;
            
            // Figure out destination position
            dstRect.y = cy + charSize.Yoffset; // Yoffset is the distance between the line top and the first char pixel
            dstRect.x = cx + charSize.Xoffset; // Xoffset is how much we must move from the last post

            if (SDL.SDL_RenderCopy(renderer, fontTexture.Handle, ref srcRect, ref dstRect) != 0) 
                throw new InvalidOperationException("Cannot DrawText: " + SDL.SDL_GetError());

            cx += charSize.Xadvance; // Xadvance is how much this character makes the cursor move
        }
    }

    /// <summary>
    /// Obtains the bounds for a given text string using the current font.
    /// </summary>
    /// <param name="text">The text to calculate.</param>
    /// <returns>The bounds of this text as an SDL_Point</returns>
    /// <exception cref="InvalidOperationException">If any character is not present on the current font.</exception>
    public SDL.SDL_Point GetTextSize(string text) {
        int size = 0;
        for (int i = 0; i < text.Length; i++) {
            char chr = text[i];
            if (!CharSizes.TryGetValue(chr, out CharSize? charSize))
                throw new InvalidOperationException("Tried to draw a nonexistent character");
            size += charSize.Xoffset;
            if (i == text.Length - 1) // The width is required for last char, since Xadvance may overshoot or undershoot
                size += charSize.Width;
            else
                size += charSize.Xadvance;
        }

        return new SDL.SDL_Point { x = size, y = fontInfo.lineHeight };
    }

    /// <summary>
    /// Obtains the SDL_PixelFormat that the font texture is using
    /// </summary>
    /// <returns>A SDL_PixelFormat.</returns>
    public uint GetFontFormat() {
        return fontTexture.Format;
    }
    
    /// <summary>
    /// Obtain whether the a char present in the current font.
    /// </summary>
    /// <param name="chr">The char to check.</param>
    /// <returns>Whether the char has a glyph in this font.</returns>
    public bool IsValidChar(char chr) {
        return CharSizes.ContainsKey(chr);
    }

    #endregion

    // Disposes the object
    public void Dispose() {
        fontData.Dispose();
        fontTexture.Dispose();
    }
}

/// <summary>
/// A handy class to efficiently abstract the rendering process.
/// Renders text anywhere, regenerating its cache when needed.
/// </summary>
public class FontCache : IDisposable {
    private STexture? cachedTexture;
    private string renderedText = "";
    private bool cacheValid = false;
    private readonly FontLoader fontRenderer;

    /// <summary>
    /// Creates a new instance attached to a fontRenderer.
    /// </summary>
    /// <param name="fontRenderer">The font renderer.</param>
    public FontCache(FontLoader fontRenderer) {
        this.fontRenderer = fontRenderer;
    }

    /// <summary>
    /// Sets the text to be draw, thread-safe.
    /// Cache is only regenerated on rendering.
    /// </summary>
    /// <param name="newText">The new text to draw.</param>
    public void SetText(string newText) {
        if (renderedText == newText) return;
        renderedText = newText;
        cacheValid = false;
    }

    /// <summary>
    /// Renders the text on the renderer at a certain position, uses cache when available.
    /// </summary>
    /// <param name="renderer">The current renderer.</param>
    /// <param name="origin">The location where the text should be drawn.</param>
    /// <param name="scale">Scale of the text, depends on the font size</param>
    public void Render(IntPtr renderer, SDL.SDL_Point origin, float scale = 1f) {
        if (renderedText == "") return;
        RenderToCache(renderer);
        if (cachedTexture == null || cachedTexture.Handle == IntPtr.Zero) throw new Exception("Could not render to cache!");
        SDL.SDL_FRect dstRect = new() {
            x = origin.x,
            y = origin.y,
            w = cachedTexture.Width * scale,
            h = cachedTexture.Height * scale,
        };
        if (SDL.SDL_RenderCopyF(renderer, cachedTexture.Handle, IntPtr.Zero, ref dstRect) != 0)
            throw new Exception("Could not render texture to renderer!");
    }

    /// <summary>
    /// Renders the current text to a cached texture, only re generates cache when needed.
    /// </summary>
    /// <param name="renderer">The current renderer.</param>
    public void RenderToCache(IntPtr renderer) {
        if (renderedText == "") return;
        if (cacheValid && cachedTexture != null && cachedTexture.Handle != IntPtr.Zero) return; // Cache is ok, no-op
        SDL.SDL_Point textSize = fontRenderer.GetTextSize(renderedText);
        if (cachedTexture == null || cachedTexture.Width < textSize.x || cachedTexture.Height < textSize.y) {
            cachedTexture?.Dispose();
            IntPtr texPtr = SDL.SDL_CreateTexture(
                renderer,
                fontRenderer.GetFontFormat(),
                (int) SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_TARGET,
                textSize.x,
                textSize.y
            );
            if (texPtr == IntPtr.Zero)
                throw new Exception("Could not create texture: " + SDL.SDL_GetError());
            cachedTexture = new STexture(texPtr);
            SDL.SDL_SetTextureBlendMode(cachedTexture.Handle, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND); // The texture has transparency
        }
        
        if (cachedTexture.Handle == IntPtr.Zero)
            throw new Exception("Could not create texture: " + SDL.SDL_GetError());
        
        SDL.SDL_SetRenderTarget(renderer, cachedTexture.Handle); // Switch to rendering to the texture
        SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 0); // We need a transparent background
        SDL.SDL_RenderClear(renderer);
        fontRenderer.DrawText(renderedText, renderer, new SDL.SDL_Point {x = 0, y = 0});
        SDL.SDL_SetRenderTarget(renderer, IntPtr.Zero); // Reset it, we're done
        cacheValid = true;
    }

    public SDL.SDL_Point GetCachedTextureSize() {
        return new SDL.SDL_Point {
            x = cachedTexture?.Width ?? 0,
            y = cachedTexture?.Height ?? 0,
        };
    }

    // Do i need to explain this...
    public void Dispose() {
        cachedTexture?.Dispose();
    }
}
