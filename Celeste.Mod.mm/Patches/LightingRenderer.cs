using MonoMod;

namespace Celeste {
    public class patch_LightingRenderer : LightingRenderer {

        [MonoModIgnore]
        [MonoModConstructor]
        [MonoModIfFlag("RelinkXNA")]
        [PatchMinMaxBlendFunction]
        public static extern void cctor();
 
    }
}