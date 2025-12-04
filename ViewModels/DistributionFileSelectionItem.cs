namespace Boutique.ViewModels;

public class DistributionFileSelectionItem
{
    public bool IsNewFile { get; }
    public DistributionFileViewModel? File { get; }

    public DistributionFileSelectionItem(bool isNewFile, DistributionFileViewModel? file)
    {
        IsNewFile = isNewFile;
        File = file;
    }

    public string DisplayName
    {
        get
        {
            if (IsNewFile)
                return "<New File>";
            return File?.FileName ?? string.Empty;
        }
    }

    public override string ToString() => DisplayName;
}

