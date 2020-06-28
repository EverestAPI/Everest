#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;

namespace Celeste {
    static class patch_Input {

        public static extern void orig_Initialize();
        public static void Initialize() {
            orig_Initialize();

            foreach (EverestModule mod in Everest._Modules)
                mod.OnInputInitialize();
                    
            Everest.Events.Input.Initialize();
        }

        public static extern void orig_Deregister();
        public static void Deregister() {
            orig_Deregister();

            foreach (EverestModule mod in Everest._Modules)
                mod.OnInputDeregister();

            Everest.Events.Input.Deregister();
        }

    }
}
