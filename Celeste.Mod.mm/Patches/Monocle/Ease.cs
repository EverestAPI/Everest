using MonoMod;
using System;

namespace Monocle {
    // A lot of the easer implementations are heavily affected by .NET Framwork higher-precision jank
    // Patch those to work with doubles to at least remedy the situation 
    [MonoModPatch("Monocle.Ease/<>c")]
    class EaserPatch {

        // We can't MonoModReplace the easers, because of the dot in the name ._.

        private const string EaserClassFName = "Monocle.Ease/<>c";
        private const double B1 = 0.363636374f, B2 = 0.727272749f, B3 = 0.545454562f, B4 = 0.909090936f, B5 = 0.8181818f, B6 = 0.954545438f;

        [MonoModLinkFrom($"System.Single {EaserClassFName}::<.cctor>b__38_1(System.Single)")]
        public float SineIn(float t) => (float) (-(double) (float) Math.Cos((float) (Math.PI/2) * (double) t) + 1.0);

        [MonoModLinkFrom($"System.Single {EaserClassFName}::<.cctor>b__38_2(System.Single)")]
        public float SineOut(float t) => (float) Math.Sin((float) (Math.PI/2) * (double) t);

        [MonoModLinkFrom($"System.Single {EaserClassFName}::<.cctor>b__38_3(System.Single)")]
        public float SineInOut(float t) => (float) (-(double) (float) Math.Cos(Math.PI * (double) t) / 2.0 + 0.5);

        [MonoModLinkFrom($"System.Single {EaserClassFName}::<.cctor>b__38_4(System.Single)")]
        public float QuadIn(float t) => (float) ((double) t * (double) t);

        [MonoModLinkFrom($"System.Single {EaserClassFName}::<.cctor>b__38_5(System.Single)")]
        public float CubeIn(float t) => (float) ((double) t * (double) t * (double) t);

        [MonoModLinkFrom($"System.Single {EaserClassFName}::<.cctor>b__38_6(System.Single)")]
        public float QuintIn(float t) => (float) ((double) t * (double) t * (double) t * (double) t * (double) t);

        [MonoModLinkFrom($"System.Single {EaserClassFName}::<.cctor>b__38_7(System.Single)")]
        public float ExpoIn(float t) => (float) Math.Pow(2.0, 10.0 * ((double) t - 1.0));

        [MonoModLinkFrom($"System.Single {EaserClassFName}::<.cctor>b__38_8(System.Single)")]
        public float BackIn(float t) => (float) ((double) t * (double) t * (2.70158f * (double) t - 1.70158f));

        [MonoModLinkFrom($"System.Single {EaserClassFName}::<.cctor>b__38_9(System.Single)")]
        public float BigBackIn(float t) => (float) ((double) t * (double) t * (4f * (double) t - 3f));

        [MonoModLinkFrom($"System.Single {EaserClassFName}::<.cctor>b__38_10(System.Single)")]
        public float ElasticIn(float t) {
            double t2 = (float) ((double) t * (double) t), t3 = (float) (t2 * (double) t);
            return (float) (33.0*t2*t3 + -59.0*t2*t2 + 32.0*t3 + -5.0*t2);
        }

        [MonoModLinkFrom($"System.Single {EaserClassFName}::<.cctor>b__38_11(System.Single)")]
        public float ElasticOut(float t) {
            double t2 = (float) ((double) t * (double) t), t3 = (float) (t2 * (double) t);
            return (float) (33.0*t2*t3 + -106.0*t2*t2 + 126.0*t3 + -67.0*t2 + 15.0*t);
        }

        [MonoModLinkFrom($"System.Single {EaserClassFName}::<.cctor>b__38_12(System.Single)")]
        public float BounceIn(float t) {
            double td = 1.0 - t;
            if (td < B1)
                return (float) (1.0 - 1.0/(B1*B1) * td * td);
            else if (td < B2)
                return (float) (1.0 - (1.0/(B1*B1) * (td - B3) * (td - B3) + (1.0 - 1.0 / 4)));
            else if (td < B4)
                return (float) (1.0 - (1.0/(B1*B1) * (td - B5) * (td - B5) + (1.0 - 1.0 / (4*4))));
            else
                return (float) (1.0 - (1.0/(B1*B1) * (td - B6) * (td - B6) + (1.0 - 1.0 / (4*4*4))));
        }

        [MonoModLinkFrom($"System.Single {EaserClassFName}::<.cctor>b__38_13(System.Single)")]
        public float BounceOut(float t) {
            double td = t;
            if (td < B1)
                return (float) (1.0/(B1*B1) * td * td);
            else if (td < B2)
                return (float) (1.0/(B1*B1) * (td - B3) * (td - B3) + (1.0 - 1.0 / 4));
            else if (td < B4)
                return (float) (1.0/(B1*B1) * (td - B5) * (td - B5) + (1.0 - 1.0 / (4*4)));
            else
                return (float) (1.0/(B1*B1) * (td - B6) * (td - B6) + (1.0 - 1.0 / (4*4*4)));
        }

        [MonoModLinkFrom($"System.Single {EaserClassFName}::<.cctor>b__38_14(System.Single)")]
        public float BounceInOut(float t) {
            if (t < 0.5f) {
                double td = 1.0 - t * 2.0;
                if (td < B1)
                    return (float) ((1.0 - 1.0/(B1*B1) * td * td) / 2.0);
                else if (td < B2)
                    return (float) ((1.0 - (1.0/(B1*B1) * (td - B3) * (td - B3) + (1.0 - 1.0 / 4))) / 2.0);
                else if (td < B4)
                    return (float) ((1.0 - (1.0/(B1*B1) * (td - B5) * (td - B5) + (1.0 - 1.0 / (4*4)))) / 2.0);
                else
                    return (float) ((1.0 - (1.0/(B1*B1) * (td - B6) * (td - B6) + (1.0 - 1.0 / (4*4*4)))) / 2.0);
            } else {
                double td = t * 2.0 - 1.0;
                if (td < B1)
                    return (float) ((1.0/(B1*B1) * td * td) / 2.0 + 0.5);
                else if (td < B2)
                    return (float) ((1.0/(B1*B1) * (td - B3) * (td - B3) + (1.0 - 1.0 / 4)) / 2.0 + 0.5);
                else if (td < B4)
                    return (float) ((1.0/(B1*B1) * (td - B5) * (td - B5) + (1.0 - 1.0 / (4*4))) / 2.0 + 0.5);
                else
                    return (float) ((1.0/(B1*B1) * (td - B6) * (td - B6) + (1.0 - 1.0 / (4*4*4))) / 2.0 + 0.5);
            }
        }

    }
}
