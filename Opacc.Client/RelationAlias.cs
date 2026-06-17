using System;
using System.Linq.Expressions;

namespace Opacc.Client;

/// <summary>Non-generic interface so the expression parser can detect RelationAlias&lt;T&gt; without knowing T.</summary>
internal interface IRelationAlias
{
    string Alias { get; }
    Type SourceType { get; }
}

/// <summary>
/// Typed relation alias for use in QueryAsync() relation definitions and projections.
///
/// Example:
///   var abladeOrt = new RelationAlias&lt;Addr&gt;("AbladeOrt");
///
///   await _opaccClient.QueryAsync&lt;SalDoc&gt;()
///       .Related(abladeOrt, (addr, doc) => addr.Number == doc.Free7)
///       .Select(doc => new {
///           doc.InternalNo,
///           Name = abladeOrt.Col(a => a.FullName),
///           Zip  = abladeOrt.Col(a => a.Zip),
///       })
///       .ToListAsync();
/// </summary>
public class RelationAlias<TSource> : IRelationAlias
    where TSource : class, IOpaccModel, new()
{
    public string Alias { get; }
    Type IRelationAlias.SourceType => typeof(TSource);

    public RelationAlias(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Alias must not be empty.", nameof(alias));
        Alias = alias;
    }

    /// <summary>
    /// References a property on the related entity within a Select() expression.
    /// At runtime this returns default — the value is never used.
    /// The expression tree is intercepted by the query builder to generate the correct OO column expression.
    /// </summary>
    public TProp Col<TProp>(Expression<Func<TSource, TProp>> property) => default!;
}

/// <summary>Number of records to return for a custom relation.</summary>
public enum RelationCount
{
    /// <summary>Empty — Opacc defaults to One.</summary>
    Default,
    One,
    ToOne,
    First,
    Last,
    All,
}
