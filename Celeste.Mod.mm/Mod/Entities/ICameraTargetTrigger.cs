namespace Celeste.Mod.Entities;

/// <summary>
/// To be implemented on custom triggers that affect the camera target, but doesn't extend any of the vanilla / Everest triggers.<br/>
/// This enables <see cref="Level.GetFullCameraTargetAt"/>, which is called on respawn and room transition,<br/>
/// to check for whether the player would collide with this trigger at the given position, thus updating the camera's position properly.
/// </summary>
public interface ICameraTargetTrigger {
    /// <summary>
    /// Called every frame the player remains in the trigger.<br/>
    /// Also called by <see cref="Level.GetFullCameraTargetAt"/> if the player would collide with this trigger at the given position.
    /// </summary>
    void OnStay(Player player);
}
