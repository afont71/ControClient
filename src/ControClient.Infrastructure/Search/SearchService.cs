using ControClient.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Infrastructure.Search;

/// <summary>
/// Búsqueda global por metadatos: clientes, fichas, documentos y credenciales.
/// Nunca busca dentro de valores secretos (SEC-07) y filtra por alcance antes de
/// devolver nada (AC-05, AC-28).
/// </summary>
public sealed class SearchService(AppDbContext db)
{
    public sealed record Hit(string Type, Guid Id, Guid ClientId, string ClientName, string Title, string? Subtitle);

    public async Task<IReadOnlyList<Hit>> SearchAsync(
        IReadOnlyCollection<Guid>? visibleClientIds,
        string term,
        int limit,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term))
            return [];

        var q = term.Trim().ToLowerInvariant();
        limit = Math.Clamp(limit, 1, 100);

        var clientsQuery = db.Clients.AsNoTracking().AsQueryable();
        if (visibleClientIds is not null)
            clientsQuery = clientsQuery.Where(c => visibleClientIds.Contains(c.Id));

        var clients = await clientsQuery
            .Where(c => c.Name.ToLower().Contains(q) || (c.Alias != null && c.Alias.ToLower().Contains(q)))
            .OrderBy(c => c.Name)
            .Take(limit)
            .Select(c => new Hit("Cliente", c.Id, c.Id, c.Name, c.Name, c.Alias))
            .ToListAsync(ct);

        var recordsQuery =
            from r in db.Records.AsNoTracking()
            join t in db.FormTemplates.AsNoTracking() on r.TemplateId equals t.Id
            join c in db.Clients.AsNoTracking() on r.ClientId equals c.Id
            select new { r, TemplateName = t.Name, ClientName = c.Name };

        if (visibleClientIds is not null)
            recordsQuery = recordsQuery.Where(x => visibleClientIds.Contains(x.r.ClientId));

        var records = await recordsQuery
            .Where(x => x.r.Title.ToLower().Contains(q) || x.TemplateName.ToLower().Contains(q))
            .OrderBy(x => x.r.Title)
            .Take(limit)
            .Select(x => new Hit("Ficha", x.r.Id, x.r.ClientId, x.ClientName, x.r.Title, x.TemplateName))
            .ToListAsync(ct);

        var documentsQuery = db.Documents.AsNoTracking().AsQueryable();
        if (visibleClientIds is not null)
            documentsQuery = documentsQuery.Where(d => visibleClientIds.Contains(d.ClientId));

        var documents = await documentsQuery
            .Where(d => d.Name.ToLower().Contains(q)
                        || d.Versions.Any(v => v.FileName.ToLower().Contains(q)))
            .OrderBy(d => d.Name)
            .Take(limit)
            .Select(d => new Hit("Documento", d.Id, d.ClientId,
                db.Clients.Where(c => c.Id == d.ClientId).Select(c => c.Name).FirstOrDefault() ?? "",
                d.Name, null))
            .ToListAsync(ct);

        // Solo metadatos de la bóveda: nombre, usuario, URL y notas no secretas.
        var vaultQuery = db.VaultItems.AsNoTracking().AsQueryable();
        if (visibleClientIds is not null)
            vaultQuery = vaultQuery.Where(v => visibleClientIds.Contains(v.ClientId));

        var vault = await vaultQuery
            .Where(v => v.Name.ToLower().Contains(q)
                        || (v.Username != null && v.Username.ToLower().Contains(q))
                        || (v.Url != null && v.Url.ToLower().Contains(q))
                        || (v.Notes != null && v.Notes.ToLower().Contains(q)))
            .OrderBy(v => v.Name)
            .Take(limit)
            .Select(v => new Hit("Credencial", v.Id, v.ClientId,
                db.Clients.Where(c => c.Id == v.ClientId).Select(c => c.Name).FirstOrDefault() ?? "",
                v.Name, v.Username))
            .ToListAsync(ct);

        return clients.Concat(records).Concat(documents).Concat(vault)
            .OrderBy(h => h.Type).ThenBy(h => h.ClientName).ThenBy(h => h.Title)
            .Take(limit)
            .ToList();
    }
}
