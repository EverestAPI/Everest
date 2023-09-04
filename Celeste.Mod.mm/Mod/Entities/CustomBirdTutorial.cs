using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Celeste.Mod.Entities {
    [CustomEntity("everest/customBirdTutorial")]
    [Tracked]
    public class CustomBirdTutorial : BirdNPC {
        public string BirdId;
        private bool onlyOnce;
        private bool caw;

        private bool triggered = false;
        private bool flewAway = false;

        private BirdTutorialGui gui;

        private static Dictionary<string, Vector2> directions = new Dictionary<string, Vector2>() {
            { "Left", new Vector2(-1, 0) },
            { "Right", new Vector2(1, 0) },
            { "Up", new Vector2(0, -1) },
            { "Down", new Vector2(0, 1) },
            { "UpLeft", new Vector2(-1, -1) },
            { "UpRight", new Vector2(1, -1) },
            { "DownLeft", new Vector2(-1, 1) },
            { "DownRight", new Vector2(1, 1) }
        };

        public CustomBirdTutorial(EntityData data, Vector2 offset)
            : base(data, offset) {
            BirdId = data.Attr("birdId");
            onlyOnce = data.Bool("onlyOnce");
            caw = data.Bool("caw");
            Facing = data.Bool("faceLeft") ? Facings.Left : Facings.Right;

            // apply the facing immediately
            Sprite.Scale.X = (float) Facing;

            object info;
            object[] controls;

            // parse the info ("title")
            string infoString = data.Attr("info");
            if (GFX.Gui.Has(infoString)) {
                info = GFX.Gui[infoString];
            } else {
                info = Dialog.Clean(infoString);
            }

            int extraAdvance = 0;

            // go ahead and parse the controls. Controls can be textures, VirtualButtons, directions or strings.
            string[] controlsStrings = data.Attr("controls").Split(',');
            controls = new object[controlsStrings.Length];
            for (int i = 0; i < controls.Length; i++) {
                string controlString = controlsStrings[i];

                object modCommand = Everest.Events.CustomBirdTutorial.ParseCommand(controlString);
                if (modCommand is ButtonBinding binding) {
                    // convert ButtonBinding to VirtualButton for convenience.
                    modCommand = binding.Button;
                }

                if (modCommand != null) {
                    // this is a command a mod registered.
                    controls[i] = modCommand;
                } else if (controlString.StartsWith("mod:")) {
                    // formatted like `mod:MaxHelpingHand/ShowHints`
                    string[] autoBinding = controlString.Substring(4).Split('/');
                    EverestModule module = Everest.Modules.FirstOrDefault(m => m.Metadata.Name == autoBinding[0]);
                    if (module?.SettingsType != null) {
                        PropertyInfo matchingInput = module.SettingsType.GetProperty(autoBinding[1]);
                        ButtonBinding val = matchingInput?.GetGetMethod()?.Invoke(module._Settings, null) as ButtonBinding;
                        if (val?.Button != null) {
                            controls[i] = val.Button;
                        } else {
                            Logger.Warn("CustomBird", $"Public ButtonBinding property not found in {module.SettingsType}. ControlString: {controlString}");
                        }
                    } else {
                        Logger.Warn("CustomBird", "EverestModule or EverestModule.SettingsType not found. ControlString: " + controlString);
                    }
                } else if (GFX.Gui.Has(controlString)) {
                    // this is a texture.
                    controls[i] = GFX.Gui[controlString];
                } else if (directions.ContainsKey(controlString)) {
                    // this is a direction.
                    controls[i] = directions[controlString];
                } else {
                    FieldInfo matchingInput = typeof(Input).GetField(controlString, BindingFlags.Static | BindingFlags.Public);
                    if (matchingInput?.GetValue(null)?.GetType() == typeof(VirtualButton)) {
                        // this is a button.
                        controls[i] = matchingInput.GetValue(null);
                    } else {
                        if (controlString.StartsWith("dialog:")) {
                            // treat that as a dialog key.
                            controls[i] = Dialog.Clean(controlString.Substring("dialog:".Length));
                        } else {
                            // treat that as a plain string.
                            controls[i] = controlString;
                        }
                    }
                }

                if (controls[i] is string) {
                    // when BirdTutorialGui renders text, it is offset by 1px on the right.
                    // width computation doesn't take this 1px into account, so we should add it back in.
                    extraAdvance++;
                    if (i == 0) {
                        // as the text is rendered 1px to the right, if the first thing is a string, there will be 1px more padding on the left.
                        // we should add that extra px on the right as well.
                        extraAdvance++;
                    }
                }
            }

            gui = new BirdTutorialGui(this, new Vector2(0f, -16f), info, controls);

            DynData<BirdTutorialGui> guiData = new DynData<BirdTutorialGui>(gui);
            // if there is no first line, resize the bubble accordingly.
            if (string.IsNullOrEmpty(infoString)) {
                guiData["infoHeight"] = 0f;
            }
            // apply the extra width.
            guiData["controlsWidth"] = guiData.Get<float>("controlsWidth") + extraAdvance;
        }

        public override void Awake(Scene scene) {
            base.Awake(scene);

            if (scene.Tracker.GetEntities<CustomBirdTutorialTrigger>().OfType<CustomBirdTutorialTrigger>()
                .All(trigger => !trigger.ShowTutorial || trigger.BirdId != BirdId)) {

                // none of the custom bird tutorial triggers are here to make the tutorial bubble show up.
                // so, make the bubble show up right away.
                TriggerShowTutorial();
            }
        }

        /// <summary>
        /// Makes the tutorial bubble show up.
        /// Called automatically if no Custom Tutorial Bird Trigger that would make the tutorial bubble show up is present in the room.
        /// </summary>
        public void TriggerShowTutorial() {
            if (!triggered) {
                triggered = true;
                Add(new Coroutine(ShowTutorial(gui, caw)));
            }
        }

        /// <summary>
        /// Makes the tutorial bubble disappear and the bird fly away.
        /// </summary>
        public void TriggerHideTutorial() {
            if (!flewAway) {
                flewAway = true;

                if (triggered) {
                    Add(new Coroutine(HideTutorial()));
                }
                triggered = true;

                Add(new Coroutine(StartleAndFlyAway()));

                if (onlyOnce) {
                    SceneAs<Level>().Session.DoNotLoad.Add(EntityID);
                }
            }
        }

        /// <summary>
        /// Helper method to find a custom bird tutorial in a scene by bird ID.
        /// Note that if only 1 bird is on screen, you can use level.Tracker.GetEntity&lt;CustomBirdTutorial>() instead.
        /// </summary>
        /// <param name="level">The level to search in</param>
        /// <param name="birdId">The ID of the bird to look for</param>
        /// <returns>The custom bird tutorial matching this ID, or null if none was found.</returns>
        public static CustomBirdTutorial FindById(Level level, string birdId) {
            return level.Tracker.GetEntities<CustomBirdTutorial>()
                .OfType<CustomBirdTutorial>()
                .Where(bird => bird.BirdId == birdId)
                .FirstOrDefault();
        }
    }
}