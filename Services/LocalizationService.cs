using System.Collections.ObjectModel;
using System.Globalization;
using Serilog;
using WPFLocalizeExtension.Engine;
using WPFLocalizeExtension.Providers;

namespace Boutique.Services;

public record LanguageOption(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public class LocalizationService
{
    private readonly ILogger _logger;
    private readonly GuiSettingsService _guiSettings;

    public LocalizationService(ILogger logger, GuiSettingsService guiSettings)
    {
        _logger = logger.ForContext<LocalizationService>();
        _guiSettings = guiSettings;
    }

    public ObservableCollection<LanguageOption> AvailableLanguages { get; } =
    [
        new LanguageOption("en", "English"),
        new LanguageOption("de", "Deutsch"),
        new LanguageOption("fr", "Français"),
        new LanguageOption("es", "Español"),
        new LanguageOption("pt-BR", "Português (Brasil)"),
        new LanguageOption("ru", "Русский"),
        new LanguageOption("zh-Hans", "简体中文"),
        new LanguageOption("ja", "日本語"),
        new LanguageOption("ko", "한국어")
    ];

    public string CurrentLanguageCode
    {
        get
        {
            try
            {
                return LocalizeDictionary.Instance.Culture?.Name ?? "en";
            }
            catch
            {
                return "en";
            }
        }
    }

    public void Initialize()
    {
        try
        {
            ResxLocalizationProvider.Instance.FallbackAssembly = "Boutique";
            ResxLocalizationProvider.Instance.FallbackDictionary = "Strings";
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to set fallback assembly/dictionary for localization");
        }

        var savedLanguage = _guiSettings.Language;
        if (!string.IsNullOrEmpty(savedLanguage))
        {
            SetLanguage(savedLanguage, save: false);
        }
        else
        {
            var systemCulture = CultureInfo.CurrentUICulture;
            var matchingLanguage = AvailableLanguages.FirstOrDefault(l =>
                l.Code.Equals(systemCulture.Name, StringComparison.OrdinalIgnoreCase) ||
                l.Code.Equals(systemCulture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase));

            if (matchingLanguage != null)
            {
                SetLanguage(matchingLanguage.Code, save: false);
            }
            else
            {
                SetLanguage("en", save: false);
            }
        }

        _logger.Information("Localization initialized with language: {Language}", CurrentLanguageCode);
    }

    public void SetLanguage(string languageCode, bool save = true)
    {
        try
        {
            var culture = new CultureInfo(languageCode);
            LocalizeDictionary.Instance.Culture = culture;
            _logger.Information("Language changed to: {Language}", languageCode);

            if (save)
            {
                _guiSettings.Language = languageCode;
            }
        }
        catch (CultureNotFoundException ex)
        {
            _logger.Warning(ex, "Failed to set language to {Language}, falling back to English", languageCode);
            try
            {
                LocalizeDictionary.Instance.Culture = new CultureInfo("en");
            }
            catch
            {
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to set language to {Language}", languageCode);
        }
    }

    public LanguageOption? GetCurrentLanguageOption() =>
        AvailableLanguages.FirstOrDefault(l =>
            l.Code.Equals(CurrentLanguageCode, StringComparison.OrdinalIgnoreCase) ||
            CurrentLanguageCode.StartsWith(l.Code, StringComparison.OrdinalIgnoreCase));
}
