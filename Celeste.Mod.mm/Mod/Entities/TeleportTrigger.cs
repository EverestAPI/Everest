using Microsoft.Xna.Framework;
using Monocle;
using System.Linq;

namespace Celeste.Mod.Entities;

[CustomEntity("everest/teleportTrigger")]
public class TeleportTrigger : Trigger {

    private readonly string _nextLevel;
    private readonly Player.IntroTypes _introType;
    private Vector2 _nearestSpawn;

    private readonly bool _onlyOnce, _useDefaultSpawn;
    private readonly string _flag;
    private bool Flag => string.IsNullOrEmpty(_flag) || SceneAs<Level>().Session.GetFlag(_flag);
    private readonly string _onlyOnceFlag;
    
    public TeleportTrigger(EntityData data, Vector2 offset) : base(data, offset)
    {
        
        _nextLevel = data.Attr("nextLevel");
        _introType = data.Enum("introType", Player.IntroTypes.None);
        _nearestSpawn = new Vector2(data.Float("nearestSpawnX"),  data.Float("nearestSpawnY"));
        
        _onlyOnce = data.Bool("onlyOnce");
        _useDefaultSpawn = data.Bool("useDefaultSpawn");
        _flag = data.Attr("flag");
        
        _onlyOnceFlag = $"{data.Level.Name}:{data.ID}_teleport_doNotTrigger";
    }

    public override void Added(Scene scene) {
        base.Added(scene);

        if (scene is not Level level || (_onlyOnce && level.Session.GetFlag(_onlyOnceFlag))) {
            RemoveSelf();
            return;
        }

        if (level.Session.MapData.Levels.FirstOrDefault(l => l.Name == _nextLevel) is not { } nextLevel)
        {
            RemoveSelf();
            Logger.Warn("Teleport Trigger", $"Destination room {_nextLevel} does not exist!");
        }
    }

    public override void OnEnter(Player player) {
        base.OnEnter(player);

        if (string.IsNullOrEmpty(_nextLevel) || !Flag)
            return;
        
        Scene.OnEndOfFrame += () => {
            SceneAs<Level>().TeleportTo(
                player, 
                _nextLevel, 
                _introType, 
                _useDefaultSpawn ? null : _nearestSpawn);
        };
        
        if (_onlyOnce)
            SceneAs<Level>().Session.SetFlag(_onlyOnceFlag);
    }
}