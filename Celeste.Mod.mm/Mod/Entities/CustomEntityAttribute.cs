using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Entities {
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class CustomEntityAttribute : Attribute {
        public string ID;
        public string Generator;

        public CustomEntityAttribute(string id)
            : this(id, "Load") {
        }

        public CustomEntityAttribute(string id, string generator) {
            ID = id;
            Generator = generator;
        }
    }
}
