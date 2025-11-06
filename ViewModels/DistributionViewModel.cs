using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using Boutique.Services;
using ReactiveUI;
using Serilog;

namespace Boutique.ViewModels;

public class DistributionViewModel : ReactiveObject
{
    private readonly IDistributionDiscoveryService _discoveryService;
    private readonly ILogger _logger;
    private readonly SettingsViewModel _settings;

    private ObservableCollection<DistributionFileViewModel> _files = new();
    private bool _isLoading;
    private DistributionFileViewModel? _selectedFile;
    private string _statusMessage = "Distribution files not loaded.";

    public DistributionViewModel(
        IDistributionDiscoveryService discoveryService,
        SettingsViewModel settings,
        ILogger logger)
    {
        _discoveryService = discoveryService;
        _settings = settings;
        _logger = logger.ForContext<DistributionViewModel>();

        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);

        _settings.WhenAnyValue(x => x.SkyrimDataPath)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(DataPath)));
    }

    public ObservableCollection<DistributionFileViewModel> Files
    {
        get => _files;
        private set => this.RaiseAndSetIfChanged(ref _files, value);
    }

    public DistributionFileViewModel? SelectedFile
    {
        get => _selectedFile;
        set => this.RaiseAndSetIfChanged(ref _selectedFile, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public string DataPath => _settings.SkyrimDataPath;

    public async Task RefreshAsync()
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;
            var dataPath = _settings.SkyrimDataPath;

            if (string.IsNullOrWhiteSpace(dataPath))
            {
                Files = [];
                StatusMessage = "Set the Skyrim data path in Settings to scan distribution files.";
                return;
            }

            if (!Directory.Exists(dataPath))
            {
                Files = [];
                StatusMessage = $"Skyrim data path does not exist: {dataPath}";
                return;
            }

            StatusMessage = "Scanning for distribution files...";
            var discovered = await _discoveryService.DiscoverAsync(dataPath);
            var outfitFiles = discovered
                .Where(file => file.OutfitDistributionCount > 0)
                .ToList();

            var viewModels = outfitFiles
                .Select(file => new DistributionFileViewModel(file))
                .ToList();

            Files = new ObservableCollection<DistributionFileViewModel>(viewModels);
            SelectedFile = Files.FirstOrDefault();

            StatusMessage = Files.Count == 0
                ? "No outfit distributions found."
                : $"Found {Files.Count} outfit distribution file(s).";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to refresh distribution files.");
            StatusMessage = $"Error loading distribution files: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
