using System.Collections.Generic;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace Boutique.Models;

public sealed class DistributionEntry
{
    public IOutfitGetter? Outfit { get; set; }
    public List<FormKey> NpcFormKeys { get; set; } = new();
    
    // Future: Add faction targeting, race targeting, etc.
    // public List<FormKey>? FactionFormKeys { get; set; }
    // public List<FormKey>? RaceFormKeys { get; set; }
}

