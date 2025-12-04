using Boutique.Models;
using Mutagen.Bethesda.Plugins;
using ReactiveUI;

namespace Boutique.ViewModels;

public class NpcRecordViewModel : ReactiveObject
{
    private readonly string _searchCache;
    private bool _isSelected;

    public NpcRecordViewModel(NpcRecord npcRecord)
    {
        NpcRecord = npcRecord;
        _searchCache = $"{DisplayName} {EditorID} {ModDisplayName} {FormKeyString}".ToLowerInvariant();
    }

    public NpcRecord NpcRecord { get; }

    public string EditorID => NpcRecord.EditorID ?? "(No EditorID)";
    public string DisplayName => NpcRecord.DisplayName;
    public string ModDisplayName => NpcRecord.ModDisplayName;
    public string FormKeyString => NpcRecord.FormKeyString;
    public FormKey FormKey => NpcRecord.FormKey;

    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    public bool MatchesSearch(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return true;

        return _searchCache.Contains(searchTerm.Trim().ToLowerInvariant());
    }
}

