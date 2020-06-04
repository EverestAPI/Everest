using System;

namespace Celeste.Mod.Entities {
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class CustomEventAttribute : Attribute {
        public string[] IDs;

        public CustomEventAttribute(params string[] ids) {
            IDs = ids;
        }
    }
}
