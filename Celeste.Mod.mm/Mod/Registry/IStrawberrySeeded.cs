using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Celeste.Mod.Entities;

namespace Celeste.Mod {
    /// <summary>
    /// If your strawberry has seeds and you want to use GenericStrawberrySeed,
    /// implement this interface. Everest will do the rest.
    /// </summary>
    public interface IStrawberrySeeded {
        // Called by CSGEN_GenericStrawberrySeeds
        void CollectedSeeds();

        // Needed to make CSGEN_GenericStrawberrySeeds happy
        List<GenericStrawberrySeed> Seeds { get; }

        // Related to seeds. Should ensure this.
        string gotSeedFlag { get; }
        bool WaitingOnSeeds { get; }
    }
}
