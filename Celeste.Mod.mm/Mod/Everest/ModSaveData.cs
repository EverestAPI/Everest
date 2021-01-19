using FMOD.Studio;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace Celeste.Mod {
    public class ModSaveData {
        public List<LevelSetStats> LevelSets = new List<LevelSetStats>();
        public List<LevelSetStats> LevelSetRecycleBin = new List<LevelSetStats>();
        public AreaKey LastArea_Safe;
        public Session CurrentSession_Safe;

        public ModSaveData() { }

        public ModSaveData(patch_SaveData data) {
            LevelSets = data.LevelSets;
            LevelSetRecycleBin = data.LevelSetRecycleBin;
            LastArea_Safe = data.LastArea_Safe;
            CurrentSession_Safe = data.CurrentSession_Safe;
        }

        public void CopyToCelesteSaveData(patch_SaveData data) {
            data.LevelSets = LevelSets;
            data.LevelSetRecycleBin = LevelSetRecycleBin;
            data.LastArea_Safe = LastArea_Safe;
            data.CurrentSession_Safe = CurrentSession_Safe;
        }
    }
}
