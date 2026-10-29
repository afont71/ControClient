using System.Text.Json;
using ControClient.Domain.Forms;
using ControClient.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ControClient.Infrastructure.Expirations;

/// <summary>
/// Calcula vencimientos a partir de los campos de fecha de las fichas: licencias,
/// garantías, renovaciones de servicio. Todo se filtra por el alcance del usuario.
/// </summary>
public sealed class ExpirationsService(AppDbContext db)
{
    public sealed record Expiration(
        Guid RecordId,
        Guid ClientId,
        string ClientName,
        string TemplateName,
        string RecordTitle,
        string FieldLabel,
        DateOnly Date,
        int DaysLeft);

    public async Task<IReadOnlyList<Expiration>> GetAsync(
        IReadOnlyCollection<Guid>? visibleClientIds,
        int horizonDays,
        CancellationToken ct = default)
    {
        horizonDays = Math.Clamp(horizonDays, 1, 730);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var limit = today.AddDays(horizonDays);

        var templates = await db.FormTemplates.AsNoTracking()
            .Include(t => t.Fields)
            .ToListAsync(ct);

        var dateFields = templates.ToDictionary(
            t => t.Id,
            t => t.Fields.Where(f => f.Type == FieldType.Date).OrderBy(f => f.Order).ToList());

        if (dateFields.Values.All(list => list.Count == 0))
            return [];

        var query = from r in db.Records.AsNoTracking()
                    join c in db.Clients.AsNoTracking() on r.ClientId equals c.Id
                    select new { r.Id, r.ClientId, r.TemplateId, r.Title, r.ValuesJson, ClientName = c.Name };

        if (visibleClientIds is not null)
            query = query.Where(x => visibleClientIds.Contains(x.ClientId));

        var records = await query.Take(20_000).ToListAsync(ct);
        var templateNames = templates.ToDictionary(t => t.Id, t => t.Name);
        var results = new List<Expiration>();

        foreach (var record in records)
        {
            if (!dateFields.TryGetValue(record.TemplateId, out var fields) || fields.Count == 0)
                continue;

            JsonDocument doc;
            try { doc = JsonDocument.Parse(record.ValuesJson); }
            catch (JsonException) { continue; }

            using (doc)
            {
                foreach (var field in fields)
                {
                    if (!doc.RootElement.TryGetProperty(field.Key, out var property)
                        || property.ValueKind != JsonValueKind.String)
                        continue;

                    if (!DateOnly.TryParseExact(property.GetString(), "yyyy-MM-dd", out var date))
                        continue;

                    if (date > limit)
                        continue;

                    results.Add(new Expiration(
                        record.Id,
                        record.ClientId,
                        record.ClientName,
                        templateNames.GetValueOrDefault(record.TemplateId, "—"),
                        record.Title,
                        field.Label,
                        date,
                        date.DayNumber - today.DayNumber));
                }
            }
        }

        return results.OrderBy(r => r.Date).ThenBy(r => r.ClientName).ToList();
    }
}
