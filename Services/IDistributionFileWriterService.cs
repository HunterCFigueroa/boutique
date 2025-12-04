using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Boutique.Models;

namespace Boutique.Services;

public interface IDistributionFileWriterService
{
    /// <summary>
    /// Writes a SkyPatcher distribution file with the given entries
    /// </summary>
    Task WriteDistributionFileAsync(
        string filePath,
        IReadOnlyList<DistributionEntry> entries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads distribution entries from an existing SkyPatcher file
    /// </summary>
    Task<IReadOnlyList<DistributionEntry>> LoadDistributionFileAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}

