#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System.Collections.Generic;

namespace Celeste {
    // : Solid because base.Awake, base.Update
    class patch_StarJumpBlock : Solid {
        private Level level;
        private float yLerp;
        private bool sinks;
        private float sinkTimer;

        public bool HasGroup { get; private set; }
        public bool MasterOfGroup { get; private set; }

        public List<patch_StarJumpBlock> Group;
        public Point GroupBoundsMax;
        public Point GroupBoundsMin;
        public List<StarJumpThru> Jumpthrus;
        public Dictionary<Platform, Vector2> Moves;

        private patch_StarJumpBlock master;

        private bool awake;

        private bool hasRail;

        public patch_StarJumpBlock(Vector2 position, float width, float height, bool sinks) : base(position, width, height, sinks) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(EntityData data, Vector2 offset);
        [MonoModConstructor]
        public void ctor(EntityData data, Vector2 offset) {
            orig_ctor(data, offset);

            hasRail = data.Bool("hasRail", true);
        }

        private void AddToGroupAndFindChildren(patch_StarJumpBlock from) {
            if (from.X < GroupBoundsMin.X) {
                GroupBoundsMin.X = (int) from.X;
            }
            if (from.Y < GroupBoundsMin.Y) {
                GroupBoundsMin.Y = (int) from.Y;
            }
            if (from.Right > GroupBoundsMax.X) {
                GroupBoundsMax.X = (int) from.Right;
            }
            if (from.Bottom > GroupBoundsMax.Y) {
                GroupBoundsMax.Y = (int) from.Bottom;
            }
            from.HasGroup = true;
            Group.Add(from);
            Moves.Add(from, from.Position);
            if (from != this) {
                from.master = this;
            }
            foreach (StarJumpThru item in Scene.CollideAll<StarJumpThru>(new Rectangle((int) from.X - 1, (int) from.Y, (int) from.Width + 2, (int) from.Height))) {
                if (!Jumpthrus.Contains(item)) {
                    AddJumpThru(item);
                }
            }
            foreach (StarJumpThru item in Scene.CollideAll<StarJumpThru>(new Rectangle((int) from.X, (int) from.Y - 1, (int) from.Width, (int) from.Height + 2))) {
                if (!Jumpthrus.Contains(item)) {
                    AddJumpThru(item);
                }
            }
            foreach (patch_StarJumpBlock block in Scene.Tracker.GetEntities<patch_StarJumpBlock>()) {
                if (!block.HasGroup && block.sinks == sinks && (Scene.CollideCheck(new Rectangle((int) from.X - 1, (int) from.Y, (int) from.Width + 2, (int) from.Height), block) || Scene.CollideCheck(new Rectangle((int) from.X, (int) from.Y - 1, (int) from.Width, (int) from.Height + 2), block))) {
                    AddToGroupAndFindChildren(block);
                }
            }
        }

        private void AddJumpThru(StarJumpThru jp) {
            Jumpthrus.Add(jp);
            Moves.Add(jp, jp.Position);
            foreach (patch_StarJumpBlock block in Scene.Tracker.GetEntities<patch_StarJumpBlock>()) {
                if (!block.HasGroup && block.sinks == sinks && Scene.CollideCheck(new Rectangle((int) jp.X - 1, (int) jp.Y, (int) jp.Width + 2, (int) jp.Height), block)) {
                    AddToGroupAndFindChildren(block);
                }
            }
        }

        private void TryToInitPosition() {
            if (MasterOfGroup) {
                foreach (patch_StarJumpBlock block in Group) {
                    if (!block.awake) {
                        return;
                    }
                }
                MoveToTarget();
            } else {
               master.TryToInitPosition();
            }
        }

        private void MoveToTarget() {
            for (int i = 0; i < 2; i++) {
                foreach (KeyValuePair<Platform, Vector2> move in Moves) {
                    JumpThru jumpThru = move.Key as JumpThru;
                    Solid solid = move.Key as Solid;

                    bool hasRider = false;
                    if ((jumpThru != null && jumpThru.HasRider()) || (solid != null && solid.HasRider())) {
                        hasRider = true;
                    }

                    if ((hasRider || i != 0) && (!hasRider || i != 1)) {
                        float y = MathHelper.Lerp(move.Value.Y, move.Value.Y + 12f, Ease.SineInOut(yLerp));
                        move.Key.MoveToY(y);
                    }
                }
            }
        }

        [MonoModReplace]
        public bool Open(float x, float y) {
            patch_StarJumpBlock block = Scene.CollideFirst<patch_StarJumpBlock>(new Vector2(X + x + 4f, Y + y + 4f));
            return !(block != null && block.sinks == sinks);
        }

        public extern void orig_Awake(Scene scene);
        public override void Awake(Scene scene) {
            if ((scene as Level).Session.Area.GetLevelSet() == "Celeste") {
                orig_Awake(scene);
                return;
            }

            // This method could've been patched via an IL hook, but eh.
            // The vanilla method is missing support for inner corners.

            // TODO: Inner corner textures? Or keep them empty as-is?

            base.Awake(scene);
            awake = true;
            
            if (!HasGroup) {
                MasterOfGroup = true;
                Moves = new Dictionary<Platform, Vector2>();
                Group = new List<patch_StarJumpBlock>();
                Jumpthrus = new List<StarJumpThru>();
                GroupBoundsMin = new Point((int) X, (int) Y);
                GroupBoundsMax = new Point((int) Right, (int) Bottom);
                AddToGroupAndFindChildren(this);
            }
            TryToInitPosition();

            level = SceneAs<Level>();

            List<MTexture> railsL = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/leftrailing");
            List<MTexture> rails = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/railing");
            List<MTexture> railsR = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/rightrailing");
            List<MTexture> edgesH = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/edgeH");
            List<MTexture> edgesV = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/edgeV");
            List<MTexture> corners = GFX.Game.GetAtlasSubtextures("objects/starjumpBlock/corner");

            // Top and bottom edges.
            for (int i = 8; i < Width - 8f; i += 8) {
                if (Open(i, -8f)) {
                    Image edge = new Image(Calc.Random.Choose(edgesH));
                    edge.CenterOrigin();
                    edge.Position = new Vector2(i + 4, 4f);
                    Add(edge);

                    if (hasRail) {
                        Image rail = new Image(rails[mod((int) (X + i) / 8, rails.Count)]);
                        rail.Position = new Vector2(i, -8f);
                        Add(rail);
                    }
                }

                if (Open(i, Height)) {
                    Image edge = new Image(Calc.Random.Choose(edgesH));
                    edge.CenterOrigin();
                    edge.Scale.Y = -1f;
                    edge.Position = new Vector2(i + 4, Height - 4f);
                    Add(edge);
                }
            }

            // Left and right edges.
            for (int i = 8; i < Height - 8f; i += 8) {
                if (Open(-8f, i)) {
                    Image edge = new Image(Calc.Random.Choose(edgesV));
                    edge.CenterOrigin();
                    edge.Scale.X = -1f;
                    edge.Position = new Vector2(4f, i + 4);
                    Add(edge);
                }

                if (Open(Width, i)) {
                    Image edge = new Image(Calc.Random.Choose(edgesV));
                    edge.CenterOrigin();
                    edge.Position = new Vector2(Width - 4f, i + 4);
                    Add(edge);
                }
            }


            // Corners / additional edges.

            Image img;

            img = null;
            if (Open(-8f, 0f) && Open(0f, -8f)) {
                img = new Image(Calc.Random.Choose(corners));
                img.Scale.X = -1f;

                if (hasRail) {
                    Image rail = new Image(railsL[mod((int) X / 8, railsL.Count)]);
                    rail.Position = new Vector2(0f, -8f);
                    Add(rail);
                }

            } else if (Open(-8f, 0f)) {
                img = new Image(Calc.Random.Choose(edgesV));
                img.Scale.X = -1f;

            } else if (Open(0f, -8f)) {
                img = new Image(Calc.Random.Choose(edgesH));

                if (hasRail) {
                    Image rail = new Image(rails[mod((int) X / 8, rails.Count)]);
                    rail.Position = new Vector2(0f, -8f);
                    Add(rail);
                }
            }

            if (img != null) {
                img.CenterOrigin();
                img.Position = new Vector2(4f, 4f);
                Add(img);
            }


            img = null;
            if (Open(Width, 0f) && Open(Width - 8f, -8f)) {
                img = new Image(Calc.Random.Choose(corners));

                if (hasRail) {
                    Image rail = new Image(railsR[mod((int) (X + Width) / 8 - 1, railsR.Count)]);
                    rail.Position = new Vector2(Width - 8f, -8f);
                    Add(rail);
                }

            } else if (Open(Width, 0f)) {
                img = new Image(Calc.Random.Choose(edgesV));

            } else if (Open(Width - 8f, -8f)) {
                img = new Image(Calc.Random.Choose(edgesH));

                if (hasRail) {
                    Image rail = new Image(rails[mod((int) (X + Width) / 8 - 1, rails.Count)]);
                    rail.Position = new Vector2(Width - 8f, -8f);
                    Add(rail);
                }
            }

            if (img != null) {
                img.CenterOrigin();
                img.Position = new Vector2(Width - 4f, 4f);
                Add(img);
            }


            img = null;
            if (Open(-8f, Height - 8f) && Open(0f, Height)) {
                img = new Image(Calc.Random.Choose(corners));
                img.Scale.X = -1f;

            } else if (Open(-8f, Height - 8f)) {
                img = new Image(Calc.Random.Choose(edgesV));
                img.Scale.X = -1f;

            } else if (Open(0f, Height)) {
                img = new Image(Calc.Random.Choose(edgesH));
            }

            if (img != null) {
                img.Scale.Y = -1f;
                img.CenterOrigin();
                img.Position = new Vector2(4f, Height - 4f);
                Add(img);
            }


            img = null;
            if (Open(Width, Height - 8f) && Open(Width - 8f, Height)) {
                img = new Image(Calc.Random.Choose(corners));

            } else if (Open(Width, Height - 8f)) {
                img = new Image(Calc.Random.Choose(edgesV));

            } else if (Open(Width - 8f, Height)) {
                img = new Image(Calc.Random.Choose(edgesH));
            }

            if (img != null) {
                img.Scale.Y = -1f;
                img.CenterOrigin();
                img.Position = new Vector2(Width - 4f, Height - 4f);
                Add(img);
            }
        }

        public extern void orig_Update();
        public override void Update() {
            if (SceneAs<Level>().Session.Area.GetLevelSet() == "Celeste") {
                orig_Update();
                return;
            }

            base.Update();
            if (MasterOfGroup && sinks) {
                bool hasRider = false;
                foreach (patch_StarJumpBlock block in Group) {
                    if (block.HasPlayerRider()) {
                        hasRider = true;
                        break;
                    }
                }
                if (!hasRider) {
                    foreach(StarJumpThru jumpThru in Jumpthrus) {
                        if (jumpThru.HasPlayerRider()) {
                            hasRider = true;
                            break;
                        }
                    }
                }
                if (hasRider) {
                    sinkTimer = 0.1f;
                } else if (sinkTimer > 0f) {
                    sinkTimer -= Engine.DeltaTime;
                }
                if (sinkTimer > 0f) {
                    yLerp = Calc.Approach(yLerp, 1f, 1f * Engine.DeltaTime);
                } else {
                    yLerp = Calc.Approach(yLerp, 0f, 1f * Engine.DeltaTime);
                }
                MoveToTarget();
            }
        }

        public extern void orig_Render();
        public override void Render() {
            if (SceneAs<Level>().Session.Area.GetLevelSet() == "Celeste") {
                orig_Render();
                return;
            }

            // Allow the StarJumpBlock to use an Everest StarJumpGraphicsController instead,
            // which is color-customizable and doesn't include forced music.
            // Prioritizes the Everest controller if found, though you shouldn't be using both at once.

            StarJumpController vanillaController = Scene.Tracker.GetEntity<StarJumpController>();
            StarClimbGraphicsController everestController = Scene.Tracker.GetEntity<StarClimbGraphicsController>();

            Vector2 cameraPos = level.Camera.Position.Floor();
            VirtualRenderTarget blockFill = null;

            if (everestController != null)
                blockFill = StarClimbGraphicsController.BlockFill;
            else if (vanillaController != null)
                blockFill = vanillaController.BlockFill;

            if (blockFill != null) {
                Draw.SpriteBatch.Draw(
                    blockFill,
                    Position,
                    new Rectangle?(new Rectangle((int) (X - cameraPos.X), (int) (Y - cameraPos.Y), (int) Width, (int) Height)),
                    Color.White
                );
            }

            base.Render();
        }

        [MonoModIgnore]
        private extern int mod(int v, int count);
    }
}
