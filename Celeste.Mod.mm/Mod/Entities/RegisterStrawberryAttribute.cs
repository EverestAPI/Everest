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
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class RegisterStrawberryAttribute : Attribute {
        public bool isTracked;
        public bool blocksNormalCollection;
        public RegisterStrawberryAttribute(bool tracked, bool blocksCollection) {
            isTracked = tracked;
            blocksNormalCollection = blocksCollection;
        }
    }
}
