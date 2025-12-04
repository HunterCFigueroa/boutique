using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Boutique.Models;

namespace Boutique.Services;

public interface INpcScanningService
{
    /// <summary>
    /// Scans all NPCs from the modlist using the LinkCache
    /// </summary>
    Task<IReadOnlyList<NpcRecord>> ScanNpcsAsync(CancellationToken cancellationToken = default);
}

