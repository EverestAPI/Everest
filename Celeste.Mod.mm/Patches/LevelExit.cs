#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Celeste.Mod;
using MonoMod;
using System.Collections;
using Monocle;
using System.IO;
using System.Xml;
using Celeste.Mod.Meta;

namespace Celeste {
    class patch_LevelExit : LevelExit {

        // We're effectively in LevelExit, but still need to "expose" private fields to our mod.
		private Session session;
        private XmlElement completeXml;
        private Atlas completeAtlas;
        private bool completeLoaded;

        private MapMetaCompleteScreen completeMeta;

        public patch_LevelExit(Mode mode, Session session, HiresSnow snow = null)
            : base(mode, session, snow) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModReplace]
        private void LoadCompleteThread() {
            AreaData area = AreaData.Get(session);

            if ((completeMeta = area.GetCompleteScreenMeta()) != null && completeMeta.Atlas != null) {
                completeAtlas = Atlas.FromAtlas(Path.Combine("Graphics", "Atlases", completeMeta.Atlas), Atlas.AtlasDataFormat.PackerNoAtlas);

            } else if ((completeXml = area.CompleteScreenXml) != null && completeXml.HasAttr("atlas")) {
                completeAtlas = Atlas.FromAtlas(Path.Combine("Graphics", "Atlases", completeXml.Attr("atlas")), Atlas.AtlasDataFormat.PackerNoAtlas);
            }

            completeLoaded = true;
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchLevelExitRoutine] // ... except for slapping an additional parameter to / updating newobj AreaComplete
        private extern IEnumerator Routine();

    }
}
