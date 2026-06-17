using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Opacc.Client.Operations.Pagination;

namespace Opacc.Client.Operations.Query;

public interface IOpaccQuery<T>
    where T : class, IOpaccModel, new()
{
    /// <summary>
    /// Filter-Bedingung hinzufügen. Property-Tags in {PropertyName} werden
    /// automatisch in Opacc-Expressions übersetzt.
    /// Mehrere Where-Aufrufe werden mit AND verknüpft.
    ///
    /// Beispiel: .Where("{City} = 'Zürich' AND {IsPassive} = 0")
    /// </summary>
    IOpaccQuery<T> Where(string opaccFilter);

    /// <summary>
    /// Typsichere Filter-Bedingung mit 3 Parametern.
    /// Beispiel: .Where(a => a.City, "=", "Zürich")
    /// </summary>
    IOpaccQuery<T> Where(Expression<Func<T, object>> property, string op, object value);

    /// <summary>
    /// Typsichere Filter-Bedingung mit Lambda-Predicate.
    /// Beispiel: .Where(a => a.City == "Zürich" &amp;&amp; !a.IsPassive)
    /// </summary>
    IOpaccQuery<T> Where(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Felder-Selektion: Einzelne Lambdas (Lade-Hinweis, Ergebnis bleibt T).
    /// Beispiel: .Select(x => x.FullName, x => x.City)
    /// </summary>
    IOpaccQuery<T> Select(params Expression<Func<T, object>>[] properties);

    /// <summary>
    /// Projiziert das Ergebnis auf <typeparamref name="TResult"/> (inkl. anonyme Typen).
    /// Beispiel: .Select(x => new { x.FullName, x.City })
    /// Beispiel: .Select(x => new AddressDto { Name = x.FullName })
    /// </summary>
    IOpaccProjectedQuery<T, TResult> Select<TResult>(Expression<Func<T, TResult>> selector);

    /// <summary>
    /// Maximale Anzahl Datensätze. Ohne Take() wird "All" gesendet.
    /// </summary>
    IOpaccQuery<T> Take(int count);

    /// <summary>
    /// Sortierung hinzufügen. Mehrere OrderBy-Aufrufe sind möglich.
    /// Beispiel: .OrderBy(a => a.FullName)
    /// </summary>
    IOpaccQuery<T> OrderBy(Expression<Func<T, object>> property, bool descending = false);

    /// <summary>
    /// Sortierung als Opacc-Expression.
    /// Beispiel: .OrderBy("Addr.FullName ASC")
    /// </summary>
    IOpaccQuery<T> OrderBy(string opaccOrderBy);

    /// <summary>
    /// Sortiert ein String-Feld als Datum.
    /// Beispiel: .OrderByAsDate(a => a.BirthDateString)
    /// </summary>
    IOpaccQuery<T> OrderByAsDate(Expression<Func<T, object>> property, bool descending = false);

    /// <summary>
    /// Sortiert ein String-Feld als Zahl.
    /// Beispiel: .OrderByAsNmb(a => a.NumberString)
    /// </summary>
    IOpaccQuery<T> OrderByAsNmb(Expression<Func<T, object>> property, bool descending = false);

    /// <summary>
    /// Duplikate unterdrücken.
    /// Beispiel: .Distinct()
    /// </summary>
    IOpaccQuery<T> Distinct(bool distinct = true);

    /// <summary>
    /// Wiederverwendbare Expression definieren.
    /// Name ohne @-Prefix angeben, dieser wird automatisch hinzugefügt.
    /// Beispiel: .Define("CustNo", "2020") → Filter kann @CustNo verwenden.
    /// </summary>
    IOpaccQuery<T> Define(string name, string expression);

    /// <summary>
    /// Scrolling-Parameter für Paginierung.
    /// Wird nur wirksam wenn auch OrderBy gesetzt ist.
    /// </summary>
    IOpaccQuery<T> Scrolling(string scrollingToken);

    /// <summary>
    /// Überspringt die ersten N Datensätze.
    /// Hinweis: Lädt intern N+Take Datensätze und verwirft die ersten N.
    /// Für effiziente Paginierung ToPageAsync() verwenden.
    /// </summary>
    IOpaccQuery<T> Skip(int count);

    /// <summary>Alias für Take(). Maximale Anzahl Datensätze.</summary>
    IOpaccQuery<T> Limit(int count);

    /// <summary>
    /// Cursor-basierte Paginierung (effizient, verwendet Opacc Scrolling).
    /// Erfordert mindestens einen OrderBy()-Aufruf.
    /// Beispiel: var page1 = await query.Take(25).ToPageAsync();
    ///           var page2 = await query.Take(25).ToPageAsync(page1.NextCursor);
    /// </summary>
    Task<OpaccPage<T>> ToPageAsync(string? cursor = null, CancellationToken ct = default);

    /// <summary>Cursor-basierte Paginierung mit Projektion.</summary>
    Task<OpaccPage<TResult>> ToPageAsync<TResult>(string? cursor = null, CancellationToken ct = default)
        where TResult : class, new();

    /// <summary>
    /// Fügt eine typisierte Custom-Relation hinzu.
    /// Die Relation wird als Related=-Parameter gesendet.
    /// Columns der Relation können im Select() via alias.Col(a => a.Property) referenziert werden.
    ///
    /// Beispiel:
    ///   var abladeOrt = new RelationAlias&lt;Addr&gt;("AbladeOrt");
    ///   query
    ///     .Related(abladeOrt, (addr, doc) => addr.Number == doc.Free7)
    ///     .Select(doc => new { doc.InternalNo, Name = abladeOrt.Col(a => a.FullName) })
    /// </summary>
    IOpaccQuery<T> Related<TRelated>(
        RelationAlias<TRelated> alias,
        Expression<Func<TRelated, T, bool>> filter,
        RelationCount count = RelationCount.Default,
        string? orderArray = null)
        where TRelated : class, IOpaccModel, new();

    /// <summary>User-spezifische Session verwenden.</summary>
    IOpaccQuery<T> WithCredentials(int userId, string? password = null);

    /// <summary>BOF-Script statt WebService.</summary>
    IOpaccQuery<T> UseBofScript(bool use = true);

    /// <summary>Response cachen.</summary>
    IOpaccQuery<T> Cache(bool cache = true);

    /// <summary>Ersten Datensatz laden.</summary>
    Task<T?> FirstAsync(CancellationToken ct = default);

    /// <summary>Alle Datensätze laden.</summary>
    Task<List<T>> ToListAsync(CancellationToken ct = default);

    /// <summary>Ersten Datensatz als DTO laden.</summary>
    Task<TResult?> FirstAsync<TResult>(CancellationToken ct = default)
        where TResult : class, new();

    /// <summary>Alle Datensätze als DTOs laden.</summary>
    Task<List<TResult>> ToListAsync<TResult>(CancellationToken ct = default)
        where TResult : class, new();

    /// <summary>Anzahl Datensätze ermitteln (MaxRows=0, Count-Column).</summary>
    Task<int> CountAsync(CancellationToken ct = default);
}
