using System;
using System.Collections;
using System.Collections.Generic;
using Celeste.Mod;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;

namespace Celeste.Mod
{
    /// <summary>
    /// All registered Strawberries must implement the IStrawberry interface.
    /// This enables the Strawberry Registry and its related modifications
    /// to do the heavy lifting painlessly.
    /// </summary>
    public interface IStrawberry
    {
        // Called by last-ditch collection attempts
        void OnCollect();
    }
}
