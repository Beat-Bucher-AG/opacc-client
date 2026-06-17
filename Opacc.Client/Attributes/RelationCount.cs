namespace Opacc.Client.Attributes;

/// <summary>
/// Controls how many rows the Query engine returns for a <c>Related=</c> relation.
/// </summary>
public enum RelationCount
{
    /// <summary>Returns one row. Similar to <c>1</c> with ordering.</summary>
    One,

    /// <summary>
    /// Use when exactly one related record exists (e.g. SalDocItem → SalDoc).
    /// More efficient than <see cref="One"/> when cardinality is guaranteed 1:1.
    /// </summary>
    ToOne,

    /// <summary>Returns the first row according to the defined <c>OrderArray</c>.</summary>
    First,

    /// <summary>Returns the last row according to the defined <c>OrderArray</c>.</summary>
    Last,

    /// <summary>
    /// Returns all matching rows.
    /// Note: cannot be combined with <c>OrderArray</c>.
    /// </summary>
    All,
}
