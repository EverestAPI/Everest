using FMOD.Studio;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace Celeste.Mod.Core {
    public class CoreModuleSaveData : EverestModuleBinarySaveData {
        public List<LevelSetStats> LevelSets = new List<LevelSetStats>();
        public List<LevelSetStats> LevelSetRecycleBin = new List<LevelSetStats>();
        public AreaKey LastArea_Safe;
        public Session CurrentSession_Safe;
        public bool Modded = false;

        public void CopyFromCelesteSaveData(patch_SaveData data) {
            LevelSets = data.LevelSets;
            LevelSetRecycleBin = data.LevelSetRecycleBin;
            LastArea_Safe = data.LastArea_Safe;
            CurrentSession_Safe = data.CurrentSession_Safe;
            Modded = data.Modded;
        }

        public void CopyToCelesteSaveData(patch_SaveData data) {
            data.LevelSets = LevelSets;
            data.LevelSetRecycleBin = LevelSetRecycleBin;
            data.LastArea_Safe = LastArea_Safe;
            data.CurrentSession_Safe = CurrentSession_Safe;
            data.Modded = Modded;
        }

        public override void Read(BinaryReader reader) {
            CoreModuleSaveData fromSave = (CoreModuleSaveData) new XmlSerializer(typeof(CoreModuleSaveData)).Deserialize(reader.BaseStream);
            LevelSets = fromSave.LevelSets;
            LevelSetRecycleBin = fromSave.LevelSetRecycleBin;
            LastArea_Safe = fromSave.LastArea_Safe;
            CurrentSession_Safe = fromSave.CurrentSession_Safe;
            Modded = fromSave.Modded;
        }

        public override void Write(BinaryWriter writer) {
            byte[] contents = UserIO.Serialize(this);
            writer.Write(contents, 0, contents.Length);
        }
    }
}
