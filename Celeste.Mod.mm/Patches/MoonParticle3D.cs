using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System.Collections.Generic;

namespace Celeste {
    class patch_MoonParticle3D : MoonParticle3D {
        private MountainModel model;
        private List<Particle> particles;

        public patch_MoonParticle3D(MountainModel model, Vector3 center, Color[] starColors1, Color[] starColors2) : base(model, center) {
            // dummy constructor
        }

        [MonoModLinkTo("Monocle.Entity", "System.Void .ctor()")]
        [MonoModIgnore]
        public extern void EntityCtor();

        [MonoModConstructor]
        public void ctor(MountainModel model, Vector3 center, Color[] starColors1, Color[] starColors2) {
            particles = new List<Particle>();
            EntityCtor();

            this.model = model;
            Visible = false;
            Matrix matrix = Matrix.CreateRotationZ(0.4f);
            if (starColors1.Length != 0) {
                for (int i = 0; i < 20; i++) {
                    Add(new Particle(OVR.Atlas["star"], Calc.Random.Choose(starColors1), center, 1f, matrix));
                }
                for (int i = 0; i < 30; i++) {
                    Add(new Particle(OVR.Atlas["snow"], Calc.Random.Choose(starColors1), center, 0.3f, matrix));
                }
            }
            matrix = Matrix.CreateRotationZ(0.8f) * Matrix.CreateRotationX(0.4f);
            if (starColors2.Length != 0) {
                for (int i = 0; i < 20; i++) {
                    Add(new Particle(OVR.Atlas["star"], Calc.Random.Choose(starColors2), center, 1f, matrix));
                }
                for (int i = 0; i < 30; i++) {
                    Add(new Particle(OVR.Atlas["snow"], Calc.Random.Choose(starColors2), center, 0.3f, matrix));
                }
            }
        }
    }
}
