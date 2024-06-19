using Monocle;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
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

            foreach (KeyValuePair<string, MTexture> kvp in ((patch_Atlas) GFX.Gui).Textures)
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
            Register(name, emoji, ((patch_MTexture) emoji)?.ScaleFix ?? 1f);
        }

        /// <summary>
        /// Register an emoji with scaling constraints.
        /// </summary>
        /// <param name="name">The emoji name.</param>
        /// <param name="emoji">The emoji texture.</param>
        /// <param name="targetWidth">The width to render this emoji as. Adjusts the MTexture.ScaleFix as side-effect!</param>
        /// <param name="targetHeight">The height to render this emoji as. Adjusts the MTexture.ScaleFix as side-effect!</param>
        public static void Register(string name, MTexture emoji, int targetWidth = 0, int targetHeight = 0) {
            if (emoji == null) {
                Register(name, emoji);
                return;
            }

            float scaleFixW = targetWidth <= 0 ? 1f : targetWidth / (float) emoji.Width;
            float scaleFixH = targetHeight <= 0 ? 1f : targetHeight / (float) emoji.Height;

            if (targetWidth <= 0)
                ((patch_MTexture) emoji).ScaleFix = scaleFixH;
            else if (targetHeight <= 0)
                ((patch_MTexture) emoji).ScaleFix = scaleFixW;
            else
                ((patch_MTexture) emoji).ScaleFix = System.Math.Min(scaleFixW, scaleFixH);

            Register(name, emoji);
        }

        /// <summary>
        /// Register an emoji.
        /// </summary>
        /// <param name="name">The emoji name.</param>
        /// <param name="emoji">The emoji texture.</param>
        /// <param name="scale">Scaling factor for the emoji spacing. Defaults to emoji.ScaleFix.</param>
        public static void Register(string name, MTexture emoji, float scale) {
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
            xml.SetAttr("xadvance", (int) (emoji.Width * scale));

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
            if (text == null)
                return text;
            // TODO: This trashes the GC and doesn't allow escaping!
            lock (_IDs) {
                StringBuilder resultBuilder = new StringBuilder();
                int appendStartIndex = 0;
                int head = -1, tail = 0;
                // suppose text is "aaaa:1111:2222:bbbb", and only ":2222:" is emoji name
                // H = head, T = tail, S = appendStartIndex
                while (tail < text.Length) {
                    if (text[tail] == ':') {
                        if (head >= 0 && text[head] == ':') {
                            /*
                                    aaaa:1111:2222:bbbb
                                (2) ^S  ^H   ^T   ^
                                (4) ^S       ^H   ^T
                                now head and tail are pointing to colons so we need to check if the text inside colons is an emoji name
                            */
                            string name = text.Substring(head + 1, (tail - 1) - (head + 1) + 1);
                            if (_IDs.TryGetValue(name, out int value)) {
                                // if it is, we need to first append the text before emoji
                                resultBuilder.Append(text, appendStartIndex, (head - 1) - appendStartIndex + 1);
                                // then append the emoji itself
                                resultBuilder.Append((char) (Start + value));
                                // the emoji name has been replaced, we need to advance tail pointer once
                                // because the colon is used and can't belong to next emoji
                                tail++;
                                appendStartIndex = tail;
                                /*
                                        aaaa:1111:2222:bbbb
                                    (5)          ^H   S^T
                                */
                            }
                        }
                        head = tail;
                        /*
                                aaaa:1111:2222:bbbb
                            (1) ^S H^T   ^
                            (3) ^S      H^T
                            when tail is pointing to a colon, we need to let head also points to it
                            so when tail moves to next colon, text[head..tail] can be an emoji name and we can check then replace it
                        */
                    }
                    tail++;
                }
                /*
                        aaaa:1111:2222:bbbb
                    (6)               H^S  ^T
                    there are still text left since last append, so we need to append them
                */
                if (appendStartIndex < text.Length) {
                    resultBuilder.Append(text, appendStartIndex, (text.Length - 1) - appendStartIndex + 1);
                }
                return resultBuilder.ToString();
            }
        }

    }
}
