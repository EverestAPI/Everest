#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;

namespace Celeste {
    public class patch_AutoSplitterInfo {

        public extern void orig_Update();        
        public void Update() {
            orig_Update();

            // Update our own AutoSplitter info struct
            AutoSplitter.Update();
        }

    }
}