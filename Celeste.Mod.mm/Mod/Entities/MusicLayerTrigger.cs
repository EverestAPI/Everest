using Microsoft.Xna.Framework;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// A trigger allowing turning on or off specific music layers.
    /// 
    /// Attributes:
    /// - `layers`: the list of layers to modify, comma-separated (for example `1,3,4`)
    /// - `enable`: true to enable the specified layers, false to disable them.
    /// </summary>
    [CustomEntity("everest/musicLayerTrigger")]
    public class MusicLayerTrigger : Trigger {

        private int[] layers;
        private bool enable;

        public MusicLayerTrigger(EntityData data, Vector2 offset)
            : base(data, offset) {

            // parse the "layers" attribute (for example "1,3,4") as an int array.
            string[] layersAsStrings = data.Attr("layers").Split(',');
            layers = new int[layersAsStrings.Length];
            for (int i = 0; i < layers.Length; i++) {
                layers[i] = int.Parse(layersAsStrings[i]);
            }

            enable = data.Bool("enable");
        }

        public override void OnEnter(Player player) {
            AudioState audio = SceneAs<Level>().Session.Audio;
            foreach (int layer in layers) {
                audio.Music.Layer(layer, enable);
            }
            audio.Apply();
        }
    }
}
