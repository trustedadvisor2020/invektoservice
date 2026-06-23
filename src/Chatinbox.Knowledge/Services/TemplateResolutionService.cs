using System.Collections.Concurrent;
using Chatinbox.Knowledge.Data;
using Chatinbox.Shared.DTOs.Templates;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Knowledge.Services;

/// <summary>
/// Resolves templates using 3-tier hierarchy: tenant > sector > platform.
/// Caches platform/sector templates with 10-minute TTL.
/// </summary>
public sealed class TemplateResolutionService
{
    private readonly TemplateRepository _templateRepo;
    private readonly JsonLinesLogger _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    // Cache key: "resolve:{slug}:{lang}:{scope}:{sector}" → (dto, expiry)
    private readonly ConcurrentDictionary<string, (TemplateCatalogDto? Dto, DateTime Expiry)> _resolveCache = new();

    // Cache key: "available:{tenantId}:{type}:{lang}" → (list, expiry)
    private readonly ConcurrentDictionary<string, (List<TemplateCatalogDto> Items, DateTime Expiry)> _availableCache = new();

    public TemplateResolutionService(TemplateRepository templateRepo, JsonLinesLogger logger)
    {
        _templateRepo = templateRepo;
        _logger = logger;
    }

    /// <summary>
    /// Resolves a template for a tenant using 3-tier hierarchy with caching.
    /// Returns the resolved template with metadata about which scope it came from.
    /// </summary>
    public async Task<TemplateResolutionResult> ResolveAsync(
        int tenantId, string slug, string lang = "tr", CancellationToken ct = default)
    {
        var dto = await _templateRepo.ResolveAsync(tenantId, slug, lang, ct);
        if (dto == null)
        {
            return new TemplateResolutionResult
            {
                Resolved = false,
                FallbackUsed = lang != "tr"
            };
        }

        return new TemplateResolutionResult
        {
            Template = dto,
            SourceScope = dto.Scope,
            FallbackUsed = dto.Lang != lang,
            Resolved = true
        };
    }

    /// <summary>
    /// Gets all published templates available to a tenant (own + sector + platform).
    /// Cached for 10 minutes per tenant/type/lang combination.
    /// </summary>
    public async Task<List<TemplateCatalogDto>> GetAvailableAsync(
        int tenantId, string? templateType, string? lang, CancellationToken ct = default)
    {
        var cacheKey = $"available:{tenantId}:{templateType ?? "all"}:{lang ?? "all"}";
        if (_availableCache.TryGetValue(cacheKey, out var cached) && cached.Expiry > DateTime.UtcNow)
            return cached.Items;

        var items = await _templateRepo.GetAvailableAsync(tenantId, templateType, lang, ct);
        _availableCache[cacheKey] = (items, DateTime.UtcNow.Add(CacheTtl));

        return items;
    }

    /// <summary>
    /// Resolves a template from a specific scope only (no fallback).
    /// Useful when you need a template from exactly platform or sector scope.
    /// </summary>
    public async Task<TemplateCatalogDto?> ResolveFromScopeAsync(
        string slug, string lang, string scope, string? sector = null,
        int? tenantId = null, CancellationToken ct = default)
    {
        var cacheKey = $"resolve:{slug}:{lang}:{scope}:{sector ?? "none"}";

        // Only cache platform and sector scope (stable data)
        if (scope != "tenant" && _resolveCache.TryGetValue(cacheKey, out var cached) && cached.Expiry > DateTime.UtcNow)
            return cached.Dto;

        // Use the existing repository list method with precise filters
        var filter = new TemplateCatalogFilter
        {
            Scope = scope,
            Sector = sector,
            Lang = lang,
            Search = slug,
            Page = 1,
            Limit = 1
        };

        var (items, _) = await _templateRepo.ListAsync(filter, ct);
        var result = items.FirstOrDefault(t => t.Slug == slug && t.IsPublished);

        if (scope != "tenant")
            _resolveCache[cacheKey] = (result, DateTime.UtcNow.Add(CacheTtl));

        return result;
    }

    /// <summary>
    /// Invalidates all caches. Called after template catalog changes
    /// (new template published, template updated, etc.).
    /// </summary>
    public void InvalidateCache()
    {
        _resolveCache.Clear();
        _availableCache.Clear();
        _logger.SystemInfo("[TemplateResolution] Cache invalidated");
    }

    /// <summary>
    /// Invalidates cache entries for a specific tenant.
    /// </summary>
    public void InvalidateTenantCache(int tenantId)
    {
        var prefix = $"available:{tenantId}:";
        foreach (var key in _availableCache.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                _availableCache.TryRemove(key, out _);
        }
    }
}
