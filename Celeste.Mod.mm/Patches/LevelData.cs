#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using System.Collections.Generic;
using System.Linq;
using MonoMod;

namespace Celeste {
    public class patch_LevelData : LevelData {
        private static readonly string[] BerryNames = {"strawberry", "goldenBerry", "memorialTextController"};

        public patch_LevelData(BinaryPacker.Element data) : base(data) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(BinaryPacker.Element data);

        [MonoModConstructor]
        public void ctor(BinaryPacker.Element data) {
            orig_ctor(data);
            MakeIdUnique();
        }

        private void MakeIdUnique() {
            List<EntityData> others = new List<EntityData>();

            int maxId = 0;
            foreach (EntityData entityData in Entities) {
                // Do not touch the strawberries, because save data need them.
                if (BerryNames.Contains(entityData.Name)) {
                    maxId = Math.Max(maxId, entityData.ID);
                }
                else {
                    others.Add(entityData);
                }
            }

            others.AddRange(Triggers);
            foreach (EntityData entityData in others) {
                maxId++;
                entityData.ID = maxId;
            }
        }
    }
}