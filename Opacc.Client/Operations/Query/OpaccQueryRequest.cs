using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Opacc.Client.Operations.Query;

public class OpaccQueryRequest
{
    /// <summary>BO Name (z.B. "Addr", "SalDoc")</summary>
    public required string BoEntity { get; init; }

    /// <summary>Spalten: einzelne "Addr.Number" oder mit Alias "Titel, Addr.Number" (Alias zuerst)</summary>
    public required List<QueryColumn> Columns { get; init; }

    /// <summary>Filter-Expression (bereits übersetzt)</summary>
    public string? Filter { get; init; }

    /// <summary>Maximale Datensätze. null = "All"</summary>
    public int? MaxRows { get; init; }

    /// <summary>Relationen die für die Columns benötigt werden</summary>
    public List<string>? Relations { get; init; }

    /// <summary>Sortierung</summary>
    public List<string>? OrderBy { get; init; }

    /// <summary>Sortiert String-Felder als Datum</summary>
    public List<string>? OrderByAsDate { get; init; }

    /// <summary>Sortiert String-Felder als Zahl</summary>
    public List<string>? OrderByAsNmb { get; init; }

    /// <summary>Duplikate unterdrücken</summary>
    public bool? Distinct { get; init; }

    /// <summary>Wiederverwendbare Expressions (@Name,Expression)</summary>
    public List<string>? Defines { get; init; }

    /// <summary>Scrolling-Token für Paginierung</summary>
    public string? Scrolling { get; init; }

    /// <summary>RedoData-Items aus der Vorgänger-Response (für Scrolling ab Seite 2). Jedes Item wird als separater #RedoData=-Parameter gesendet.</summary>
    public string[]? RedoData { get; init; }

    /// <summary>RedoArgs für Scrolling (z.B. "ne,,25")</summary>
    public string? RedoArgs { get; init; }

    public bool Cache { get; init; }
    public bool UseBofScript { get; init; }

    /// <summary>
    /// Baut die Parameter-Liste im Opacc Query-Format:
    /// Main=Addr, Columns=Addr.Number,Addr.FullName, Filter=..., MaxRows=50, etc.
    /// </summary>
    public string[] BuildParameters()
    {
        var parameters = new List<string>();

        // Main
        parameters.Add("Main=" + BoEntity);

        // Columns — einfache werden zusammengefasst, komplexe einzeln
        var simpleColumns = new List<string>();
        foreach (var col in Columns)
        {
            if (col.HasAlias || col.Expression.Contains(","))
            {
                // Komplexe Column: "Column=Addr.Number, Titel"
                parameters.Add("Column=" + col.Expression);
            }
            else
            {
                simpleColumns.Add(col.Expression);
            }
        }
        if (simpleColumns.Count > 0)
            parameters.Add("Columns=" + string.Join(",", simpleColumns));

        // Relations
        if (Relations != null)
        {
            foreach (var relation in Relations)
            {
                if (!string.IsNullOrWhiteSpace(relation))
                    parameters.Add("Related=" + relation);
            }
        }

        // Defines (vor Filter, damit @Variablen in Filter verfügbar sind)
        if (Defines != null)
        {
            foreach (var define in Defines)
            {
                if (!string.IsNullOrWhiteSpace(define))
                    parameters.Add("Define=" + define);
            }
        }

        // MaxRows
        parameters.Add("MaxRows=" + (MaxRows?.ToString() ?? "All"));

        // Distinct
        if (Distinct.HasValue)
            parameters.Add("Distinct=" + (Distinct.Value ? "1" : "0"));

        // Scrolling (nur wenn OrderBy vorhanden)
        var hasOrderBy = (OrderBy != null && OrderBy.Count > 0)
                      || (OrderByAsDate != null && OrderByAsDate.Count > 0)
                      || (OrderByAsNmb != null && OrderByAsNmb.Count > 0);
        if (!string.IsNullOrWhiteSpace(Scrolling) && hasOrderBy)
            parameters.Add("Scrolling=" + Scrolling);

        // Filter
        if (!string.IsNullOrWhiteSpace(Filter))
            parameters.Add("Filter=" + Filter);

        // OrderBy
        if (OrderBy != null)
        {
            foreach (var orderBy in OrderBy)
            {
                if (!string.IsNullOrWhiteSpace(orderBy))
                    parameters.Add("OrderBy=" + orderBy);
            }
        }

        // OrderByAsDate
        if (OrderByAsDate != null)
        {
            foreach (var orderBy in OrderByAsDate)
            {
                if (!string.IsNullOrWhiteSpace(orderBy))
                    parameters.Add("OrderByAsDate=" + orderBy);
            }
        }

        // OrderByAsNmb
        if (OrderByAsNmb != null)
        {
            foreach (var orderBy in OrderByAsNmb)
            {
                if (!string.IsNullOrWhiteSpace(orderBy))
                    parameters.Add("OrderByAsNmb=" + orderBy);
            }
        }

        // RedoData / RedoArgs (für cursor-based pagination ab Seite 2)
        if (RedoData != null)
        {
            foreach (var item in RedoData)
                parameters.Add("#RedoData=" + item);
        }
        if (!string.IsNullOrWhiteSpace(RedoArgs))
            parameters.Add("#RedoArgs=" + RedoArgs);

        return parameters.ToArray();
    }
}
