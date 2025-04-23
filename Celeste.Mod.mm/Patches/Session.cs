#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Celeste {
    public class patch_Session : Session {

        public class Slider {
            private patch_Session _Session;

            public string Name { get; init; }
            internal float _Value;

            internal Slider(patch_Session session, string name, float value = 0f) {
                _Session = session;
                Name = name;
                _Value = value;
            }

            public float Value {
                get => _Value;
                set {
                    float previous = _Value;
                    _Value = value;
                    Everest.Events.Session.SliderChanged(_Session, this, previous);
                }
            }
        }

        /// <summary>
        /// Used internally for serialization.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static class SerializationOnly {
            [Serializable]
            [EditorBrowsable(EditorBrowsableState.Never)]
            public struct Slider {
                [XmlAttribute]
                public string Name;
                [XmlAttribute]
                public float Value;
            }
        }

        [XmlIgnore]
        private Dictionary<string, Slider> _Sliders;

        [XmlIgnore]
        public IReadOnlyDictionary<string, Slider> Sliders => _Sliders;

        /// <summary>
        /// Used internally for serialization; getting or setting this will cause issues.
        /// Instead, use <see cref="Sliders" />.
        /// </summary>
        ///
        /// NOTE: this cannot be private or [Obselete] because either of those would break serialization.
        [XmlArray("Sliders")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public SerializationOnly.Slider[] SlidersSerializationOnly {
            get {
                var result = new SerializationOnly.Slider[_Sliders.Count];
                int i = 0;
                foreach ((string name, Slider slider) in _Sliders) {
                    result[i++] = new() { Name = name, Value = slider._Value };
                }
                return result;
            }
            set {
                _Sliders.Clear();
                foreach (SerializationOnly.Slider slider in value) {
                    _Sliders[slider.Name] = new(this, slider.Name, slider.Value);
                }
            }
        }

        public bool RestartedFromGolden;

        public patch_Session(AreaKey area, string checkpoint = null, AreaStats oldStats = null) : base(area, checkpoint, oldStats) { }

        [MonoModReplace]
        [MonoModConstructor]
        private void ctor() {
            Audio = new();
            Flags = new();
            LevelFlags = new();
            Strawberries = new();
            DoNotLoad = new();
            Keys = new();
            Counters = new();
            SummitGems = new bool[6];
            FirstLevel = true;
            DarkRoomAlpha = 0.75f;

            JustStarted = true;
            InArea = true;
            _Sliders = new Dictionary<string, Slider>();
        }

        [PatchSessionOrigCtor]
        public extern void orig_ctor(AreaKey area, string checkpoint = null, AreaStats oldStats = null);

        [MonoModConstructor]
        public void ctor(AreaKey area, string checkpoint = null, AreaStats oldStats = null) {
            patch_AreaData areaData = patch_AreaData.Get(area);
            if (area.Mode == AreaMode.Normal) {
                areaData.RestoreASideAreaData();
            } else {
                areaData.OverrideASideMeta(area.Mode);
            }
            orig_ctor(area, checkpoint, oldStats);
        }

        public float GetSlider(string slider) {
            if (_Sliders.TryGetValue(slider, out Slider obj)) {
                return obj._Value;
            }
            return 0f;
        }

        public Slider GetSliderObject(string slider) {
            if (!_Sliders.TryGetValue(slider, out Slider obj)) {
                _Sliders[slider] = obj = new(this, slider, 0f);
                Everest.Events.Session.SliderChanged(this, obj, null);
            }
            return obj;
        }

        public void SetSlider(string slider, float value)
            => GetSliderObject(slider).Value = value;

        public void AddToSlider(string slider, float amount)
            => GetSliderObject(slider).Value += amount;

    }
}

namespace MonoMod {

    /// <summary>
    /// Patch the session .ctor to default to "" if the map has no rooms,
    /// or the default room does not exist
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchSessionOrigCtor))]
    class PatchSessionOrigCtorAttribute : Attribute { }

    static partial class MonoModRules {
        public static void PatchSessionOrigCtor(ILContext context, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(context);
            cursor.GotoNext(MoveType.After, instr => instr.MatchCallvirt("Celeste.MapData", "StartLevel"));

            ILLabel end = cursor.DefineLabel();

            // go to ldfld string LevelData::Name if StartLevel() is not null
            ILLabel yesLdfldName = cursor.DefineLabel();
            cursor.Emit(OpCodes.Dup);
            cursor.Emit(OpCodes.Brtrue_S, yesLdfldName);

            // else replace it with null and skip the ldfld string LevelData::Name
            ILLabel noLdfldName = cursor.DefineLabel();
            cursor.Emit(OpCodes.Pop);
            cursor.Emit(OpCodes.Ldnull);
            cursor.Emit(OpCodes.Br_S, noLdfldName);

            // next opcode is ldfld string LevelData::Name; mark the labels
            cursor.MarkLabel(yesLdfldName);
            cursor.Index++;
            cursor.MarkLabel(noLdfldName);

            // now use the value if it's not null, else default to ""
            cursor.Emit(OpCodes.Dup);
            cursor.Emit(OpCodes.Brtrue_S, end);
            cursor.Emit(OpCodes.Pop);
            cursor.Emit(OpCodes.Ldstr, "");

            // don't forget to mark where the stfld string Session::Level is
            cursor.MarkLabel(end);
        }
    }
}
