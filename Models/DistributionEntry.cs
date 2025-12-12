using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace Boutique.Models;

public sealed class DistributionEntry
{
    public IOutfitGetter? Outfit { get; set; }
    public List<FormKey> NpcFormKeys { get; set; } = [];
    public List<FormKey> FactionFormKeys { get; set; } = [];
    public List<FormKey> KeywordFormKeys { get; set; } = [];
    public List<FormKey> RaceFormKeys { get; set; } = [];

    /// <summary>
    /// SPID trait filters (position 5): gender, unique, summonable, child, leveled, teammate, dead.
    /// </summary>
    public SpidTraitFilters TraitFilters { get; set; } = new();

    /// <summary>
    /// Chance percentage (0-100) for distribution. Null means 100% (default).
    /// </summary>
    public int? Chance { get; set; }
}
