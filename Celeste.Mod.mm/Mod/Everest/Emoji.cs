using Monocle;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml;

namespace Celeste.Mod {
    public static class Emoji {

        public const char Start = '\uE000';
        public const char End = '\uF8FF';

        /// <summary>
        /// A list of all registered emoji names, in order of their IDs.
        /// </summary>
        public static ReadOnlyCollection<string> Registered => new ReadOnlyCollection<string>(_Registered);
        public static char Last => (char) ('\uE000' + _Registered.Count - 1);

        private static List<string> _Registered = new List<string>();
        private static Dictionary<string, int> _IDs = new Dictionary<string, int>();
        private static List<bool> _IsMonochrome = new List<bool>();
        private static List<PixelFontCharacter> _Chars = new List<PixelFontCharacter>();

        private static bool Initialized = false;
        private static Queue<KeyValuePair<string, MTexture>> Queue = new Queue<KeyValuePair<string, MTexture>>();
        private static XmlElement _FakeXML;
        public static XmlElement FakeXML {
            get {
                if (_FakeXML != null)
                    return _FakeXML;
                _FakeXML = new XmlDocument().CreateElement("emoji");
                return _FakeXML;
            }
        }

        internal static bool IsInitialized() {
            return Initialized;
        }

        internal static void Auto() {
            if (Initialized)
                return;
            Initialized = true;

            foreach (KeyValuePair<string, MTexture> kvp in GFX.Gui.GetTextures())
                if (kvp.Key.StartsWith("emoji/"))
                    Register(kvp.Key.Substring(6), kvp.Value);

            foreach (KeyValuePair<string, MTexture> kvp in Queue)
                Register(kvp.Key, kvp.Value);
        }

        /// <summary>
        /// Register an emoji.
        /// </summary>
        /// <param name="name">The emoji name.</param>
        /// <param name="emoji">The emoji texture.</param>
        public static void Register(string name, MTexture emoji) {
            if (!Initialized) {
                Queue.Enqueue(new KeyValuePair<string, MTexture>(name, emoji));
                return;
            }

            bool monochrome;
            if (monochrome = name.EndsWith(".m")) {
                name = name.Substring(0, name.Length - 2);
            }

            XmlElement xml = FakeXML;
            xml.SetAttr("x", 0);
            xml.SetAttr("y", 0);
            xml.SetAttr("width", emoji.Width);
            xml.SetAttr("height", emoji.Height);
            xml.SetAttr("xoffset", 0);
            xml.SetAttr("yoffset", 0);
            xml.SetAttr("xadvance", emoji.Width);

            int id = _Registered.IndexOf(name);
            if (id < 0) {
                id = _Registered.Count;
                _Registered.Add(name);

                lock (_IDs) {
                    _IDs[name] = id;
                }

                _IsMonochrome.Add(monochrome);

            } else {
                _IsMonochrome[id] = monochrome;
            }

            _Chars.Add(new PixelFontCharacter(Start + id, emoji, xml));
        }

        /// <summary>
        /// Fill a font with emoji.
        /// </summary>
        /// <param name="font">The font to fill.</param>
        public static void Fill(PixelFont font) {
            Auto();
            foreach (PixelFontSize size in font.Sizes) {
                foreach (PixelFontCharacter c in _Chars) {
                    size.Characters[c.Character] = c;
                }
            }
        }

        /// <summary>
        /// Gets the char for the specified emoji.
        /// </summary>
        /// <param name="name">The emoji name.</param>
        /// <returns>The emoji char.</returns>
        public static int Get(string name)
            => _IDs[name];

        /// <summary>
        /// Gets the char for the specified emoji.
        /// </summary>
        /// <param name="name">The emoji name.</param>
        /// <param name="c">The emoji char.</param>
        /// <returns>Whether the emoji was registered or not.</returns>
        public static bool TryGet(string name, out char c) {
            c = '\0';
            if (!_IDs.TryGetValue(name, out int id))
                return false;
            c = (char) (Start + id);
            return true;
        }

        /// <summary>
        /// Gets whether the emoji is monochrome or not.
        /// </summary>
        /// <param name="c">The emoji char.</param>
        /// <returns>Whether the emoji is monochrome or not.</returns>
        public static bool IsMonochrome(char c)
            => _IsMonochrome[c - Start];

        /// <summary>
        /// Transforms all instances of :emojiname: to \uSTART+ID
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string Apply(string text) {
            if (text == null || text.Count(c => c == ':') < 2)
                return text;
            // TODO: This trashes the GC and doesn't allow escaping!
            lock (_IDs) {
                foreach (KeyValuePair<string, int> kvp in _IDs)
                    text = text.Replace(":" + kvp.Key + ":", ((char) (Start + kvp.Value)).ToString());
            }
            return text;
        }

    }
}
