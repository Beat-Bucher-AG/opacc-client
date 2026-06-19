using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Opacc.Client.Enums;
using Opacc.Client.Operations.Pagination;
using Opacc.Client.Relations;

namespace Opacc.Client.Operations.GetBo;

public interface IOpaccGetBo<T>
    where T : class, IOpaccModel, new()
{
    /// <summary>Startwert für die Suche (Primary Key oder Index-Wert)</summary>
    IOpaccGetBo<T> Start(object value);

    /// <summary>
    /// Composes the start key from the model instance using the effective index's key properties
    /// (ordered by segment number, comma-separated for multi-segment indices).
    /// </summary>
    IOpaccGetBo<T> Start(T model);

    /// <summary>
    /// Composes the start key from explicit segment values, in index-segment order, comma-joined.
    /// Use for multi-segment indices when the key cannot be derived from a single value or model.
    /// Example: <c>.Start(salDocInternalNo, salDocItemNo, poolNo)</c>.
    /// </summary>
    IOpaccGetBo<T> Start(params object[] segments);

    /// <summary>Suchmodus (Default: Equal)</summary>
    IOpaccGetBo<T> SearchOperator(SearchOperator op);

    /// <summary>Index-Nummer überschreiben (Default: aus [OOBoIndex])</summary>
    IOpaccGetBo<T> Index(int indexNo, int? segment = null);

    /// <summary>
    /// Opacc-Filter-Expression (Tags in {PropertyName} werden übersetzt).
    /// Mehrere Aufrufe werden mit AND verknüpft.
    /// </summary>
    IOpaccGetBo<T> Filter(string opaccFilter);

    /// <summary>
    /// Typsichere Filter-Bedingung mit Lambda-Predicate.
    /// Mehrere Aufrufe werden mit AND verknüpft.
    /// Beispiel: .Where(x => x.IsPassive == false)
    /// </summary>
    IOpaccGetBo<T> Where(Expression<Func<T, bool>> predicate);

    /// <summary>Anzahl Datensätze (Default: 1 für First, muss für ToList gesetzt werden)</summary>
    IOpaccGetBo<T> Take(int count);

    /// <summary>
    /// Überspringt die ersten N Datensätze.
    /// Hinweis: Lädt intern N+Take Datensätze und verwirft die ersten N.
    /// Für effiziente Paginierung ToPageAsync() verwenden.
    /// </summary>
    IOpaccGetBo<T> Skip(int count);

    /// <summary>Alias für Take().</summary>
    IOpaccGetBo<T> Limit(int count);

    /// <summary>
    /// Felder-Selektion: Einzelne Lambdas (Lade-Hinweis, Ergebnis bleibt T).
    /// Beispiel: .Select(x => x.FullName, x => x.City)
    /// </summary>
    IOpaccGetBo<T> Select(params Expression<Func<T, object>>[] properties);

    /// <summary>
    /// Lädt eine vordefinierte Relation explizit, unabhängig von den selektierten Properties.
    /// Nützlich wenn Relations-Properties im Filter oder in virtuellen Attributen benötigt werden.
    /// Beispiel: .Include(Addr.Relations.Cust)
    /// </summary>
    IOpaccGetBo<T> Include(params RelationSpec<T>[] relations);

    /// <summary>
    /// Lädt eine vordefinierte Relation per Alias-String.
    /// Beispiel: .Include("Cust")
    /// </summary>
    IOpaccGetBo<T> Include(string alias);

    /// <summary>Zusätzliche virtuelle Attribute</summary>
    IOpaccGetBo<T> VirtualAttributes(string attributes);

    /// <summary>User-spezifische Session verwenden</summary>
    IOpaccGetBo<T> WithCredentials(int userId, string? password = null);

    /// <summary>BOF-Script Verbindung statt WebService</summary>
    IOpaccGetBo<T> UseBofScript(bool use = true);

    /// <summary>Response cachen</summary>
    IOpaccGetBo<T> Cache(bool cache = true);

    /// <summary>
    /// Projiziert das Ergebnis auf <typeparamref name="TResult"/> (inkl. anonyme Typen).
    /// Beispiel: .Select(x => new { x.FullName, x.City })
    /// </summary>
    IOpaccProjectedGetBo<T, TResult> Select<TResult>(Expression<Func<T, TResult>> selector);

    /// <summary>Ersten Datensatz laden (setzt Take(1) implizit)</summary>
    Task<T?> FirstAsync(CancellationToken ct = default);

    /// <summary>Liste laden</summary>
    Task<List<T>> ToListAsync(CancellationToken ct = default);

    /// <summary>Ersten Datensatz als DTO laden</summary>
    Task<TResult?> FirstAsync<TResult>(CancellationToken ct = default)
        where TResult : class, new();

    /// <summary>Liste als DTOs laden</summary>
    Task<List<TResult>> ToListAsync<TResult>(CancellationToken ct = default)
        where TResult : class, new();

    /// <summary>
    /// Cursor-basierte Paginierung.
    /// Beispiel: var page1 = await getbo.Take(25).ToPageAsync();
    ///           var page2 = await getbo.Take(25).ToPageAsync(page1.NextCursor);
    /// </summary>
    Task<OpaccPage<T>> ToPageAsync(string? cursor = null, CancellationToken ct = default);

    /// <summary>Cursor-basierte Paginierung mit Projektion.</summary>
    Task<OpaccPage<TResult>> ToPageAsync<TResult>(string? cursor = null, CancellationToken ct = default)
        where TResult : class, new();
}
