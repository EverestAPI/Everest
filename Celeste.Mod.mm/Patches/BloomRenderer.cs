using MonoMod;

namespace Celeste {
    public class patch_BloomRenderer : BloomRenderer {

        [MonoModIgnore]
        [MonoModConstructor]
        [MonoModIfFlag("RelinkXNA")]
        [PatchMinMaxBlendFunction]
        public static extern void cctor();
 
    }
}