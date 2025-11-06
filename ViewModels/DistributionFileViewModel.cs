using System.IO;
using Boutique.Models;
using ReactiveUI;

namespace Boutique.ViewModels;

public class DistributionFileViewModel(DistributionFile file) : ReactiveObject
{
    public string FileName => file.FileName;
    public string RelativePath => file.RelativePath;
    public string Directory => Path.GetDirectoryName(file.RelativePath) ?? string.Empty;
    public string FullPath => file.FullPath;
    public IReadOnlyList<DistributionLine> Lines => file.Lines;

    public string TypeDisplay => file.Type switch
    {
        DistributionFileType.Spid => "SPID",
        DistributionFileType.SkyPatcher => "SkyPatcher",
        _ => file.Type.ToString()
    };

    public int RecordCount => file.Lines.Count(l => l.Kind == DistributionLineKind.KeyValue);
    public int CommentCount => file.Lines.Count(l => l.Kind == DistributionLineKind.Comment);
}