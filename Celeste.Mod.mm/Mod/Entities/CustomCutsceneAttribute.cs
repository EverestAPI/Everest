using System;

namespace Celeste.Mod.Entities {
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class CustomCutsceneAttribute : Attribute {
        public string[] IDs;

        public CustomCutsceneAttribute(params string[] ids) {
            IDs = ids;
        }
    }
}
