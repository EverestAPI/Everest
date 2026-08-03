using System;

namespace Celeste.Mod.Entities;

/// <summary>
/// Mark this renderer as a custom <see cref="ScreenWipe"/> with an identifier.
/// <br/>
/// This Screen Wipe will be applied if the map's Wipe metadata has a matching value.
/// <br/>
/// If there is no match, then the full type name of the Screen Wipe is checked for.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class CustomWipeAttribute : Attribute {

    /// <summary>
    /// A list of unique identifiers for this Screen Wipe.<br/>
    /// Follows the pattern "ID [= LoadMethodName]".
    /// </summary>
    public string[] IDs;

    /// <summary>
    /// Mark this renderer as a custom <see cref="ScreenWipe"/> with an identifier.<br/>
    /// If there is no match, then the full type name of the Screen Wipe is checked for.
    /// </summary>
    /// <param name="ids">
    /// A list of unique identifiers for this Screen Wipe.<br/>
    /// Follows the pattern "ID [= LoadMethodName]".
    /// </param>
    public CustomWipeAttribute(params string[] ids) {
        IDs = ids;
    }
}
