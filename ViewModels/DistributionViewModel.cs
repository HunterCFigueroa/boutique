using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using Boutique.Models;
using Boutique.Services;
using Mutagen.Bethesda.Skyrim;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Serilog;

namespace Boutique.ViewModels;

public enum DistributionTab
{
    Files = 0,
    Edit = 1,
    Npcs = 2
}

public class DistributionViewModel : ReactiveObject
{
    private readonly ILogger _logger;
    private readonly SettingsViewModel _settings;

    public DistributionViewModel(
        IDistributionDiscoveryService discoveryService,
        IDistributionFileWriterService fileWriterService,
        INpcScanningService npcScanningService,
        INpcOutfitResolutionService npcOutfitResolutionService,
        IDistributionConflictDetectionService conflictDetectionService,
        SettingsViewModel settings,
        IArmorPreviewService armorPreviewService,
        IMutagenService mutagenService,
        ILogger logger)
    {
        _settings = settings;
        _logger = logger.ForContext<DistributionViewModel>();

        // Create tab ViewModels
        FilesTab = new DistributionFilesTabViewModel(
            discoveryService,
            armorPreviewService,
            mutagenService,
            settings,
            logger);

        EditTab = new DistributionEditTabViewModel(
            fileWriterService,
            npcScanningService,
            conflictDetectionService,
            armorPreviewService,
            mutagenService,
            settings,
            logger);

        NpcsTab = new DistributionNpcsTabViewModel(
            npcScanningService,
            npcOutfitResolutionService,
            discoveryService,
            armorPreviewService,
            mutagenService,
            settings,
            logger);

        // Wire up preview interactions - forward tab previews to main preview
        FilesTab.ShowPreview.RegisterHandler(async interaction =>
        {
            await ShowPreview.Handle(interaction.Input);
            interaction.SetOutput(Unit.Default);
        });
        EditTab.ShowPreview.RegisterHandler(async interaction =>
        {
            await ShowPreview.Handle(interaction.Input);
            interaction.SetOutput(Unit.Default);
        });
        NpcsTab.ShowPreview.RegisterHandler(async interaction =>
        {
            await ShowPreview.Handle(interaction.Input);
            interaction.SetOutput(Unit.Default);
        });

        // Wire up Files tab refresh to update Edit and NPCs tabs
        // Subscribe to Files collection changes to update other tabs
        FilesTab.WhenAnyValue(vm => vm.Files)
            .Subscribe(files =>
            {
                var fileList = files.ToList();
                EditTab.SetDistributionFiles(fileList);
                EditTab.SetDistributionFilesInternal(fileList);
                NpcsTab.SetDistributionFilesInternal(fileList);
            });

        // Wire up Edit tab file save to refresh Files tab
        EditTab.FileSaved += async filePath =>
        {
            await FilesTab.RefreshCommand.Execute();
            // Update Edit tab with refreshed files
            EditTab.SetDistributionFiles(FilesTab.Files.ToList());
            EditTab.SetDistributionFilesInternal(FilesTab.Files.ToList());
        };

        // Aggregate loading state from tabs
        this.WhenAnyValue(
            vm => vm.FilesTab.IsLoading,
            vm => vm.EditTab.IsLoading,
            vm => vm.NpcsTab.IsLoading,
            (files, edit, npcs) => files || edit || npcs)
            .Subscribe(loading => IsLoading = loading);

        // Aggregate status messages from tabs (prioritize non-empty messages)
        this.WhenAnyValue(
            vm => vm.FilesTab.StatusMessage,
            vm => vm.EditTab.StatusMessage,
            vm => vm.NpcsTab.StatusMessage,
            (files, edit, npcs) => 
                !string.IsNullOrWhiteSpace(edit) ? edit :
                !string.IsNullOrWhiteSpace(npcs) ? npcs :
                !string.IsNullOrWhiteSpace(files) ? files :
                "Ready")
            .Subscribe(msg => StatusMessage = msg);

        // Handle tab changes
        this.WhenAnyValue(vm => vm.SelectedTabIndex)
            .Subscribe(index =>
            {
                this.RaisePropertyChanged(nameof(IsEditMode));

                if (index == (int)DistributionTab.Edit)
                {
                    // Initialize Edit tab when selected
                    EditTab.SetDistributionFiles(FilesTab.Files.ToList());
                    EditTab.SetDistributionFilesInternal(FilesTab.Files.ToList());
                    
                    // Select "New File" by default if nothing selected
                    if (EditTab.SelectedDistributionFile == null)
                    {
                        var newFileItem = EditTab.AvailableDistributionFiles.FirstOrDefault(f => f.IsNewFile);
                        if (newFileItem != null)
                        {
                            EditTab.SelectedDistributionFile = newFileItem;
                        }
                    }
                }
                else if (index == (int)DistributionTab.Npcs)
                {
                    // Update NPCs tab with files from Files tab
                    NpcsTab.SetDistributionFilesInternal(FilesTab.Files.ToList());
                    
                    // Auto-scan NPC outfits when NPCs tab is selected (if not already scanned)
                    if (NpcsTab.NpcOutfitAssignments.Count == 0 && !NpcsTab.IsLoading)
                    {
                        _logger.Debug("NPCs tab selected, triggering auto-scan");
                        _ = Task.Run(() => NpcsTab.ScanNpcOutfitsCommand.Execute());
                    }
                }
            });
    }

    /// <summary>
    /// Exposes SettingsViewModel for data binding in SettingsPanelView.
    /// This ensures consistent settings state across all tabs.
    /// </summary>
    public SettingsViewModel Settings => _settings;

    [Reactive] public int SelectedTabIndex { get; set; }

    public bool IsEditMode => SelectedTabIndex == (int)DistributionTab.Edit;

    [Reactive] public bool IsLoading { get; private set; }

    [Reactive] public string StatusMessage { get; private set; } = "Ready";

    public DistributionFilesTabViewModel FilesTab { get; }

    public DistributionEditTabViewModel EditTab { get; }

    public DistributionNpcsTabViewModel NpcsTab { get; }

    public Interaction<ArmorPreviewScene, Unit> ShowPreview { get; } = new();

    // Expose tab properties for backward compatibility with XAML bindings
    // These delegate to the appropriate tab ViewModel

    // Files tab properties
    public ObservableCollection<DistributionFileViewModel> Files => FilesTab.Files;
    public DistributionFileViewModel? SelectedFile
    {
        get => FilesTab.SelectedFile;
        set => FilesTab.SelectedFile = value;
    }
    public string LineFilter
    {
        get => FilesTab.LineFilter;
        set => FilesTab.LineFilter = value;
    }
    public IEnumerable<DistributionLine> FilteredLines => FilesTab.FilteredLines;
    public ReactiveCommand<Unit, Unit> RefreshCommand => FilesTab.RefreshCommand;
    public ReactiveCommand<DistributionLine, Unit> PreviewLineCommand => FilesTab.PreviewLineCommand;

    // Edit tab properties
    public ObservableCollection<DistributionEntryViewModel> DistributionEntries => EditTab.DistributionEntries;
    public DistributionEntryViewModel? SelectedEntry
    {
        get => EditTab.SelectedEntry;
        set => EditTab.SelectedEntry = value;
    }
    public ObservableCollection<NpcRecordViewModel> AvailableNpcs => EditTab.AvailableNpcs;
    public ObservableCollection<NpcRecordViewModel> FilteredNpcs => EditTab.FilteredNpcs;
    public ObservableCollection<FactionRecordViewModel> AvailableFactions => EditTab.AvailableFactions;
    public ObservableCollection<FactionRecordViewModel> FilteredFactions => EditTab.FilteredFactions;
    public ObservableCollection<KeywordRecordViewModel> AvailableKeywords => EditTab.AvailableKeywords;
    public ObservableCollection<KeywordRecordViewModel> FilteredKeywords => EditTab.FilteredKeywords;
    public ObservableCollection<RaceRecordViewModel> AvailableRaces => EditTab.AvailableRaces;
    public ObservableCollection<RaceRecordViewModel> FilteredRaces => EditTab.FilteredRaces;
    public string NpcSearchText
    {
        get => EditTab.NpcSearchText;
        set => EditTab.NpcSearchText = value;
    }
    public string FactionSearchText
    {
        get => EditTab.FactionSearchText;
        set => EditTab.FactionSearchText = value;
    }
    public string KeywordSearchText
    {
        get => EditTab.KeywordSearchText;
        set => EditTab.KeywordSearchText = value;
    }
    public string RaceSearchText
    {
        get => EditTab.RaceSearchText;
        set => EditTab.RaceSearchText = value;
    }
    public ObservableCollection<IOutfitGetter> AvailableOutfits => EditTab.AvailableOutfits;
    public ObservableCollection<DistributionFileSelectionItem> AvailableDistributionFiles => EditTab.AvailableDistributionFiles;
    public DistributionFileSelectionItem? SelectedDistributionFile
    {
        get => EditTab.SelectedDistributionFile;
        set => EditTab.SelectedDistributionFile = value;
    }
    public bool IsCreatingNewFile => EditTab.IsCreatingNewFile;
    public bool ShowNewFileNameInput => EditTab.ShowNewFileNameInput;
    public string NewFileName
    {
        get => EditTab.NewFileName;
        set => EditTab.NewFileName = value;
    }
    public string DistributionFilePath => EditTab.DistributionFilePath;
    public string DistributionPreviewText => EditTab.DistributionPreviewText;
    public bool HasConflicts => EditTab.HasConflicts;
    public bool ConflictsResolvedByFilename => EditTab.ConflictsResolvedByFilename;
    public string ConflictSummary => EditTab.ConflictSummary;
    public string SuggestedFileName => EditTab.SuggestedFileName;
    public ReactiveCommand<Unit, Unit> AddDistributionEntryCommand => EditTab.AddDistributionEntryCommand;
    public ReactiveCommand<DistributionEntryViewModel, Unit> RemoveDistributionEntryCommand => EditTab.RemoveDistributionEntryCommand;
    public ReactiveCommand<DistributionEntryViewModel, Unit> SelectEntryCommand => EditTab.SelectEntryCommand;
    public ReactiveCommand<Unit, Unit> AddSelectedNpcsToEntryCommand => EditTab.AddSelectedNpcsToEntryCommand;
    public ReactiveCommand<Unit, Unit> AddSelectedFactionsToEntryCommand => EditTab.AddSelectedFactionsToEntryCommand;
    public ReactiveCommand<Unit, Unit> AddSelectedKeywordsToEntryCommand => EditTab.AddSelectedKeywordsToEntryCommand;
    public ReactiveCommand<Unit, Unit> AddSelectedRacesToEntryCommand => EditTab.AddSelectedRacesToEntryCommand;
    public ReactiveCommand<Unit, Unit> SaveDistributionFileCommand => EditTab.SaveDistributionFileCommand;
    public ReactiveCommand<Unit, Unit> LoadDistributionFileCommand => EditTab.LoadDistributionFileCommand;
    public ReactiveCommand<Unit, Unit> ScanNpcsCommand => EditTab.ScanNpcsCommand;
    public ReactiveCommand<Unit, Unit> SelectDistributionFilePathCommand => EditTab.SelectDistributionFilePathCommand;
    public ReactiveCommand<DistributionEntryViewModel, Unit> PreviewEntryCommand => EditTab.PreviewEntryCommand;
    public void EnsureOutfitsLoaded() => EditTab.EnsureOutfitsLoaded();

    // NPCs tab properties
    public ObservableCollection<NpcOutfitAssignmentViewModel> NpcOutfitAssignments => NpcsTab.NpcOutfitAssignments;
    public NpcOutfitAssignmentViewModel? SelectedNpcAssignment
    {
        get => NpcsTab.SelectedNpcAssignment;
        set => NpcsTab.SelectedNpcAssignment = value;
    }
    public string NpcOutfitSearchText
    {
        get => NpcsTab.NpcOutfitSearchText;
        set => NpcsTab.NpcOutfitSearchText = value;
    }
    public ObservableCollection<NpcOutfitAssignmentViewModel> FilteredNpcOutfitAssignments => NpcsTab.FilteredNpcOutfitAssignments;
    public string SelectedNpcOutfitContents => NpcsTab.SelectedNpcOutfitContents;
    public ReactiveCommand<Unit, Unit> ScanNpcOutfitsCommand => NpcsTab.ScanNpcOutfitsCommand;
    public ReactiveCommand<NpcOutfitAssignmentViewModel, Unit> PreviewNpcOutfitCommand => NpcsTab.PreviewNpcOutfitCommand;

    public bool IsInitialized => EditTab.IsInitialized || NpcsTab.IsInitialized;

    public string DataPath => _settings.SkyrimDataPath;
}
