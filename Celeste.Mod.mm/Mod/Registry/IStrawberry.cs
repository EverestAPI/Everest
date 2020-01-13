using System;
using System.Collections;
using System.Collections.Generic;
using Celeste.Mod;
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

        // Called by CSGEN_StrawberrySeeds
        void CollectedSeeds();

        // If someone has a use for any of these commented stubs in IStrawberry, go ahead and uncomment them.
        // As it is now, there's no good use for any of them.

        //void Added(Scene scene);
        //void Update();
        //void OnDash(Vector2 dir);
        //void OnAnimate(string id);
        //void OnPlayer(Player player);
        //IEnumerator CollectRoutine(int collectIndex);
        //void OnLoseLeader();

    }
}
