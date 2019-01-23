using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Entities {
    public class CustomNPC : NPC {
        private string spritePath;
        private int spriteRate;
        private string dialogEntry;
        private bool onlyOnce;
        private bool endLevel;
        private List<MTexture> textures;
        private string name;
        private EntityID id;
        private bool approachWhenTalking;
        private int approachDistance;

        private string[] dialogs;

        private bool animated = false;
        private float frame;
        private Vector2 scale = new Vector2(1, 1);
        private Vector2 indicatorOffset = Vector2.Zero;

        private Coroutine talkRoutine;

        public event Action<int> OnStart;
        public event Action OnEnd;

        public CustomNPC(EntityData data, Vector2 offset, EntityID id) : base(data.Position + offset) {
            this.id = id;

            spritePath = data.Attr("sprite", ""); // Path is from Graphics/Atlases/Gameplay/characters
            spriteRate = data.Int("spriteRate", 1);
            dialogEntry = data.Attr("dialogId", "");
            dialogs = dialogEntry.Split(',');
            onlyOnce = data.Bool("onlyOnce", true);
            endLevel = data.Bool("endLevel", false);
            indicatorOffset.X = data.Float("indicatorOffsetX", 0);
            indicatorOffset.Y = data.Float("indicatorOffsetY", 0);
            approachWhenTalking = data.Bool("approachWhenTalking", false);
            approachDistance = data.Int("approachDistance", 16);

            if (data.Bool("flipX", false))
                scale.X = -1;
            if (data.Bool("flipY", false))
                scale.Y = -1;

            string extension = Path.GetExtension(spritePath);
            if (!string.IsNullOrEmpty(extension))
                spritePath = spritePath.Replace(extension, "");
            spritePath = Path.Combine("characters", spritePath).Replace('\\', '/');
            name = Regex.Replace(spritePath, "\\d+$", string.Empty);

            textures = GFX.Game.GetAtlasSubtextures(name);
            if (textures != null && textures.Count > 1)
                animated = true;

            frame = 0;
        }

        public override void Added(Scene scene) {
            base.Added(scene);

            if ((scene as Level).Session.GetFlag("DoNotTalk" + id))
                return;

            // Determine the talker bounds and coordinates based on the first animation frame.

            float width = 0;
            float height = 0;
            if (textures != null && textures.Count > 0) {
                width = textures[0].Width * scale.X;
                height = textures[0].Height * scale.Y;
            }

            Add(Talker = new TalkComponent(
                new Rectangle(-(int) width / 2 - 48, (int) -height - 16, (int) width + 96, (int) height + 32),
                new Vector2(-0.5f + indicatorOffset.X, -height / 2 + indicatorOffset.Y),
                OnTalk
            ));
        }

        public override void Update() {
            base.Update();

            if (animated && textures != null && textures.Count > 0) {
                frame += spriteRate * Engine.DeltaTime;
                frame %= textures.Count;
            }
        }

        public override void Render() {
            if (textures != null && textures.Count > 0) {
                MTexture mtexture = textures[(int) frame];
                mtexture.DrawJustified(Position, new Vector2(0.5f, 1.0f), Color.White, scale);
            }
        }

        private void OnTalk(Player player) {
            if (onlyOnce && (dialogs.Length == 1 || Session.GetCounter(id + "DialogCounter") > dialogs.Length - 2)) {
                (Scene as Level).Session.SetFlag("DoNotTalk" + id, true); // Sets flag to not load
            }
            player.StateMachine.State = Player.StDummy;
            OnStart?.Invoke(Session.GetCounter(id + "DialogCounter"));
            Level.StartCutscene(OnTalkEnd);
            Add(talkRoutine = new Coroutine(Talk(player)));
        }

        private void OnTalkEnd(Level level) {
            Player player = Scene.Tracker.GetEntity<Player>();
            if (player != null) {
                player.StateMachine.Locked = false;
                player.StateMachine.State = 0;
            }
            talkRoutine.Cancel();
            talkRoutine.RemoveSelf();
            Session.IncrementCounter(id + "DialogCounter");
            if (endLevel) {
                if (dialogs.Length == 1 || Session.GetCounter(id + "DialogCounter") > dialogs.Length - 1) {
                    level.CompleteArea();
                    player.StateMachine.State = Player.StDummy;
                }
            }
            if (onlyOnce) {
                if (dialogs.Length == 1 || Session.GetCounter(id + "DialogCounter") > dialogs.Length - 1) {
                    Remove(Talker);
                }
            } else if (Session.GetCounter(id + "DialogCounter") > dialogs.Length - 1 || dialogs.Length == 1) {
                Session.SetCounter(id + "DialogCounter", 0);
            }
            OnEnd?.Invoke();
        }

        private IEnumerator Talk(Player player) {
            if (approachWhenTalking) {
                if (scale.X > 0) {
                    yield return PlayerApproachRightSide(player, true, approachDistance);
                } else {
                    yield return PlayerApproachLeftSide(player, true, approachDistance);
                }

            }
            yield return Textbox.Say(dialogs[Session.GetCounter(id + "DialogCounter")], null);
            Level.EndCutscene();
            OnTalkEnd(Level);
        }
    }
}
