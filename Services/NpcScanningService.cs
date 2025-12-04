using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Boutique.Models;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace Boutique.Services;

public class NpcScanningService : INpcScanningService
{
    private readonly IMutagenService _mutagenService;
    private readonly ILogger _logger;

    public NpcScanningService(IMutagenService mutagenService, ILogger logger)
    {
        _mutagenService = mutagenService;
        _logger = logger.ForContext<NpcScanningService>();
    }

    public async Task<IReadOnlyList<NpcRecord>> ScanNpcsAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run<IReadOnlyList<NpcRecord>>(() =>
        {
            if (_mutagenService.LinkCache is not ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache)
            {
                _logger.Warning("LinkCache not available for NPC scanning.");
                return Array.Empty<NpcRecord>();
            }

            var npcs = new List<NpcRecord>();

            try
            {
                var npcRecords = linkCache.WinningOverrides<INpcGetter>();

                foreach (var npc in npcRecords)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Filter out invalid NPCs
                    if (npc.FormKey == FormKey.Null)
                        continue;

                    // Skip NPCs without EditorID (likely invalid)
                    if (string.IsNullOrWhiteSpace(npc.EditorID))
                        continue;

                    var name = GetNpcName(npc);
                    
                    // Find the original master (topmost in load order) that first introduced this NPC
                    var originalModKey = FindOriginalMaster(linkCache, npc.FormKey);

                    var npcRecord = new NpcRecord(
                        npc.FormKey,
                        npc.EditorID,
                        name,
                        originalModKey);

                    npcs.Add(npcRecord);
                }

                _logger.Information("Scanned {Count} NPCs from modlist.", npcs.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.Information("NPC scanning cancelled.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to scan NPCs.");
            }

            return npcs;
        }, cancellationToken);
    }

    /// <summary>
    /// Finds the original master mod (topmost in load order) that first introduced the NPC,
    /// rather than the leaf-most mod that last edited it.
    /// </summary>
    private ModKey FindOriginalMaster(ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache, FormKey formKey)
    {
        try
        {
            // Resolve all contexts for this FormKey - they are returned in load order
            // The first context is the original master that first introduced the record
            var contexts = linkCache.ResolveAllContexts<INpc, INpcGetter>(formKey);
            
            // Get the first context (original master)
            var firstContext = contexts.FirstOrDefault();
            if (firstContext != null)
            {
                return firstContext.ModKey;
            }
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Failed to resolve contexts for FormKey {FormKey}, falling back to FormKey.ModKey", formKey);
        }

        // Fallback to FormKey.ModKey if context resolution fails
        return formKey.ModKey;
    }

    private static string? GetNpcName(INpcGetter npc)
    {
        return npc.Name?.String;
    }
}

