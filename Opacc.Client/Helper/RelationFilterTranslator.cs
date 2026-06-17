using System;
using System.Linq.Expressions;
using Opacc.Client.Metadata;

namespace Opacc.Client.Helper;

/// <summary>
/// Translates a two-parameter join-filter expression to an Opacc Query filter string.
///
/// Example:
///   (addr, doc) => addr.Number == doc.Free7  →  "AbladeOrt.Number = SalDoc.Free7"
///   (addr, doc) => addr.Number == doc.Free7 &amp;&amp; addr.IsPassive == false
///     →  "(AbladeOrt.Number = SalDoc.Free7) and (AbladeOrt.IsPassive = 0)"
/// </summary>
internal static class RelationFilterTranslator
{
    public static string Translate<TRelated, TMain>(
        Expression<Func<TRelated, TMain, bool>> filter,
        string relatedAlias,
        EntityMetadata relatedMeta,
        EntityMetadata mainMeta)
    {
        return TranslateNode(
            filter.Body,
            filter.Parameters[0], relatedAlias, relatedMeta,
            filter.Parameters[1], mainMeta.BoEntity, mainMeta);
    }

    private static string TranslateNode(
        Expression expr,
        ParameterExpression relatedParam, string relatedAlias, EntityMetadata relatedMeta,
        ParameterExpression mainParam,    string mainPrefix,   EntityMetadata mainMeta)
    {
        // Unwrap boxing
        if (expr is UnaryExpression { NodeType: ExpressionType.Convert } convert)
            return TranslateNode(convert.Operand, relatedParam, relatedAlias, relatedMeta, mainParam, mainPrefix, mainMeta);

        switch (expr)
        {
            case BinaryExpression bin:
            {
                var left  = TranslateNode(bin.Left,  relatedParam, relatedAlias, relatedMeta, mainParam, mainPrefix, mainMeta);
                var right = TranslateNode(bin.Right, relatedParam, relatedAlias, relatedMeta, mainParam, mainPrefix, mainMeta);
                var op = bin.NodeType switch
                {
                    ExpressionType.Equal              => "=",
                    ExpressionType.NotEqual           => "<>",
                    ExpressionType.GreaterThan        => ">",
                    ExpressionType.GreaterThanOrEqual => ">=",
                    ExpressionType.LessThan           => "<",
                    ExpressionType.LessThanOrEqual    => "<=",
                    ExpressionType.AndAlso            => "and",
                    ExpressionType.OrElse             => "or",
                    _ => throw new NotSupportedException(
                        $"Operator '{bin.NodeType}' is not supported in a relation filter.")
                };
                return bin.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse
                    ? $"({left}) {op} ({right})"
                    : $"{left} {op} {right}";
            }

            // Property on the related entity: addr.Number → AbladeOrt.Number
            case MemberExpression m when m.Expression == relatedParam:
                return ResolveMember(m.Member.Name, relatedAlias, relatedMeta);

            // Property on the main entity: doc.Free7 → SalDoc.Free7
            case MemberExpression m when m.Expression == mainParam:
                return ResolveMember(m.Member.Name, mainPrefix, mainMeta);

            case ConstantExpression constant:
                return constant.Value switch
                {
                    null    => "''",
                    string s => $"'{s.Replace("'", "''")}'",
                    bool b  => b ? "1" : "0",
                    _       => constant.Value.ToString()!,
                };

            // Captured variable (closure) — evaluate at parse time
            case MemberExpression closureMember:
            {
                var value = Expression.Lambda(closureMember).Compile().DynamicInvoke();
                return value switch
                {
                    null    => "''",
                    string s => $"'{s.Replace("'", "''")}'",
                    bool b  => b ? "1" : "0",
                    _       => value.ToString()!,
                };
            }

            default:
                throw new NotSupportedException(
                    $"Expression '{expr}' ({expr.NodeType}) is not supported in a relation filter. " +
                    "Use simple property comparisons, e.g. (addr, doc) => addr.Number == doc.Free7.");
        }
    }

    private static string ResolveMember(string clrName, string prefix, EntityMetadata meta)
    {
        if (!meta.Properties.TryGetValue(clrName, out var propMeta))
            throw new ArgumentException(
                $"Property '{clrName}' not found on '{meta.BoEntity}'. " +
                "Make sure the property exists on the model.");

        var ooExpr = propMeta.OoExpression;
        // Strip existing BO prefix if present: "SalDoc.Free7" → "Free7"
        var dotIdx = ooExpr.IndexOf('.');
        var shortExpr = dotIdx >= 0 ? ooExpr[(dotIdx + 1)..] : ooExpr;
        return prefix + "." + shortExpr;
    }
}
