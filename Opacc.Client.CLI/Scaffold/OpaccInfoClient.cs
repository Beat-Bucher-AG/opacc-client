using Opacc.Client.Transport;
using OpaccWebservice;

namespace Opacc.Client.CLI.Scaffold;

/// <summary>
/// Ruft Opacc-Metadaten ab:
///   GetInfoBo  (Biz) → alle Business Objects
///   GetInfoBoAttr (Biz) → alle Attribute eines BO
/// </summary>
internal class OpaccInfoClient(IOpaccTransport transport)
{
    // ================================================================
    // GetInfoBo  →  alle BO-Namen
    // ================================================================

    public async Task<List<BoInfo>> GetAllBosAsync(CancellationToken ct = default)
    {
        var response = await transport.SendRawAsync("Biz", "GetInfoBo", [], ct);
        if (response == null)
            return [];

        var records = ParseRecords(response);
        var result = new List<BoInfo>(records.Count);

        foreach (var row in records)
        {
            // Spalte "Bo" enthält den BO-Namen
            var name = GetString(row, "Bo");
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var desc = GetString(row, "BoNameMlsAttr");
            int.TryParse(GetString(row, "BoIdIndex"), out var boIdIndex);
            result.Add(new BoInfo(name.Trim(), desc?.Trim(), boIdIndex));
        }

        return result;
    }

    // ================================================================
    // GetInfoBoAttr  →  alle Attribute eines BO
    // ================================================================

    public async Task<List<BoAttrInfo>> GetBoAttributesAsync(string boName, CancellationToken ct = default)
    {
        var response = await transport.SendRawAsync("Biz", "GetInfoBoAttr", [boName], ct);
        if (response == null)
            return [];

        var records = ParseRecords(response);
        var result = new List<BoAttrInfo>(records.Count);

        foreach (var row in records)
        {
            // "BoAttr" enthält die vollqualifizierte Expression, z.B. "Addr.Number"
            var expr = GetString(row, "BoAttr");
            if (string.IsNullOrWhiteSpace(expr))
                continue;

            expr = expr.Trim();

            // Kurzname = Teil nach dem letzten Punkt, ohne Modifikatoren
            var shortName = expr.Contains('.')
                ? expr[(expr.LastIndexOf('.') + 1)..]
                : expr;
            shortName = shortName.Split('(')[0].TrimEnd('!', '@');

            var dataTypeCd    = GetString(row, "DataTypeCd") ?? "A";
            var format        = GetString(row, "Format");
            var description   = GetString(row, "BoNameMlsAttr", "DefaultValue");
            var isNullOk      = GetString(row, "IsNullOk") == "1";
            var boIndexRaw    = GetString(row, "BoIndex");
            var viewStateCd   = GetString(row, "ViewItemStateCd") ?? "AV_PUB";
            int.TryParse(boIndexRaw, out var boIndex);

            result.Add(new BoAttrInfo(expr, shortName.Trim(), dataTypeCd.Trim(), format?.Trim(), description?.Trim(), isNullOk, boIndex, viewStateCd.Trim()));
        }

        return result;
    }

    // ================================================================
    // GetInfoService  →  alle verfügbaren Services
    // ================================================================

    /// <summary>
    /// Ruft alle verfügbaren Opacc-Services ab (Port + Operation).
    /// Gibt eine geordnete Liste von (PortId, OperationId) zurück.
    /// </summary>
    public async Task<List<ServiceInfo>> GetAllServicesAsync(CancellationToken ct = default)
    {
        FlatResponseData? response;
        try
        {
            response = await transport.SendRawAsync("Biz", "GetInfoService", [], ct);
        }
        catch
        {
            return [];
        }

        if (response == null) return [];

        var records = ParseRecords(response);
        var result  = new List<ServiceInfo>(records.Count);

        foreach (var row in records)
        {
            // GetInfoService response has column "Service" for the operation name; all services are on port "Biz"
            var portId      = "Biz";
            var operationId = GetString(row, "Service", "TextKey");

            if (string.IsNullOrWhiteSpace(operationId))
                continue;

            result.Add(new ServiceInfo(portId.Trim(), operationId.Trim()));
        }

        // Deduplizieren und sortieren
        return result
            .DistinctBy(s => $"{s.PortId}.{s.OperationId}")
            .OrderBy(s => s.PortId)
            .ThenBy(s => s.OperationId)
            .ToList();
    }

    // ================================================================
    // GetInfoServiceAoAttr  →  Input-Attribute eines Service
    // ================================================================

    /// <summary>
    /// Gibt alle Input-Attribute eines Services zurück (z.B. für ArtSal_GetPrice → ArtNo, CustNo, …).
    /// Attribute mit RelationshipCd = "R" sind Result-/Output-Parameter und werden eingeschlossen
    /// damit der Aufrufer selbst filtern kann.
    /// </summary>
    public async Task<List<ServiceAoAttrInfo>> GetServiceAttributesAsync(
        string serviceName, CancellationToken ct = default)
    {
        FlatResponseData? response;
        try
        {
            response = await transport.SendRawAsync("Biz", "GetInfoServiceAoAttr", [serviceName], ct);
        }
        catch { return []; }

        if (response == null) return [];

        var records = ParseRecords(response);
        var result  = new List<ServiceAoAttrInfo>(records.Count);

        foreach (var row in records)
        {
            var name = GetString(row, "ServiceAoAttr", "Name");
            if (string.IsNullOrWhiteSpace(name)) continue;

            var dataTypeCd = GetString(row, "DataTypeCd") ?? "A";
            var format     = GetString(row, "Format");
            var isNullOk   = GetString(row, "IsNullOk") != "0";  // default nullable
            var relCd      = GetString(row, "RelationshipCd") ?? "0";

            result.Add(new ServiceAoAttrInfo(name.Trim(), dataTypeCd.Trim(), format?.Trim(), isNullOk, relCd.Trim()));
        }

        return result;
    }

    // ================================================================
    // GetInfoBoIndex  →  alle Index-Segmente eines BO
    // ================================================================

    /// <summary>
    /// Gibt alle Indices eines BO zurück, gemappt auf ihre geordneten Segment-Ausdrücke.
    /// Beispiel für SalDoc: { 7 → ["SalDoc.InternalNo"], 1 → ["SalDoc.SalProcLevelCd", "SalDoc.Number"], … }
    /// </summary>
    public async Task<Dictionary<int, List<string>>> GetAllIndexSegmentsAsync(
        string boName, CancellationToken ct = default)
    {
        var response = await transport.SendRawAsync("Biz", "GetInfoBoIndex", [boName], ct);
        if (response == null)
            return [];

        var records = ParseRecords(response);
        var result  = new Dictionary<int, List<string>>();

        foreach (var row in records)
        {
            if (!int.TryParse(GetString(row, "BoIndex"), out var idx))
                continue;

            int.TryParse(GetString(row, "NumberOfIndexSegs"), out var count);
            var segments = new List<string>(count);

            for (int i = 1; i <= count; i++)
            {
                var seg = GetString(row, $"IndexSeg{i}");
                if (!string.IsNullOrWhiteSpace(seg))
                    segments.Add(seg.Trim());
            }

            result[idx] = segments;
        }

        return result;
    }

    // ================================================================
    // GetInfoQuery  →  Query-verfügbare Attribute eines BO
    // ================================================================

    /// <summary>
    /// Ruft via GetInfoQuery ab, welche Attribute des BO in der Query-Operation
    /// vorhanden sind und welche davon öffentlich verfügbar sind (ViewItemStateCd = "AV_PUB").
    ///
    /// Gibt null zurück wenn GetInfoQuery nicht antwortet (Service nicht verfügbar) —
    /// in diesem Fall werden keine Attribute ausgeschlossen oder markiert.
    /// </summary>
    public async Task<QueryFieldAvailability?> GetQueryFieldAvailabilityAsync(string boName, CancellationToken ct = default)
    {
        FlatResponseData? response;
        try
        {
            // Parameter: [ViewItemStateCd-Filter (leer = alle), BO-Name]
            response = await transport.SendRawAsync("Biz", "GetInfoQuery", ["", boName], ct);
        }
        catch
        {
            return null;
        }

        if (response == null || response.RowCount == 0)
            return null;

        var records = ParseRecords(response);
        var available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allKnown  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in records)
        {
            // "View"-Spalte enthält den BO-Namen — nur Zeilen für das gesuchte BO
            var view = GetString(row, "View");
            if (!string.Equals(view?.Trim(), boName, StringComparison.OrdinalIgnoreCase))
                continue;

            // MlsTextKey = vollqualifiziert "SalDoc.AbaDoesSplit"
            // Name       = Kurzname         "AbaDoesSplit"
            var fullExpr  = GetString(row, "MlsTextKey");
            var shortName = GetString(row, "Name");

            void Register(HashSet<string> set)
            {
                if (!string.IsNullOrWhiteSpace(fullExpr))  set.Add(fullExpr.Trim());
                if (!string.IsNullOrWhiteSpace(shortName)) set.Add(shortName.Trim());
            }

            Register(allKnown);

            var stateCd = GetString(row, "ViewItemStateCd");
            if (string.Equals(stateCd, "AV_PUB", StringComparison.OrdinalIgnoreCase))
                Register(available);
        }

        return new QueryFieldAvailability(available, allKnown);
    }

    // ================================================================
    // Helpers
    // ================================================================

    private static List<Dictionary<string, string?>> ParseRecords(FlatResponseData data)
    {
        var records = new List<Dictionary<string, string?>>();

        if (data.Columns == null || data.Columns.Length == 0 || data.RowCount == 0)
            return records;

        for (int i = 0; i < data.RowCount; i++)
        {
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in data.Columns)
            {
                if (col?.Name == null) continue;
                var value = i < (col.Rows?.Length ?? 0) ? col.Rows![i] : null;
                row.TryAdd(col.Name, value);

                // Auch Kurzname registrieren: "Addr.Bo" → "Bo"
                var dot = col.Name.LastIndexOf('.');
                if (dot >= 0)
                    row.TryAdd(col.Name[(dot + 1)..], value);
            }
            records.Add(row);
        }

        return records;
    }

    private static string? GetString(Dictionary<string, string?> row, params string[] candidates)
    {
        foreach (var key in candidates)
        {
            if (row.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
                return val;
        }
        return null;
    }

    /// <summary>
    /// Gibt alle Spaltennamen der Response aus — für Debugging beim ersten Run.
    /// </summary>
    public static void DumpColumns(FlatResponseData data)
    {
        if (data.Columns == null) return;
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  Columns: {string.Join(", ", data.Columns.Select(c => c?.Name ?? "?"))}");
        Console.ResetColor();
    }
}
