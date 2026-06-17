using System;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Opacc.Client.Enums;
using Opacc.Client.Metadata;

namespace Opacc.Client.Helper;

/// <summary>
/// Translates a <see cref="Expression{Func{T, bool}}"/> predicate into an Opacc filter string.
/// Supports ==, !=, &gt;, &gt;=, &lt;, &lt;=, &amp;&amp; (AND), || (OR), ! (NOT), list.Contains().
/// </summary>
internal static class PredicateTranslator
{
    /// <param name="isQuerySyntax">
    /// When true, list.Contains() is emitted as <c>Prop=[v1,v2]</c> (Opacc Query native IN syntax).
    /// When false, it falls back to an OR chain for GetBo/SaveBo/DeleteBo filters.
    /// </param>
    public static string Translate<T>(Expression<Func<T, bool>> predicate, EntityMetadata metadata, bool isQuerySyntax = false)
    {
        return TranslateExpression(predicate.Body, predicate.Parameters[0], metadata, isQuerySyntax);
    }

    private static string TranslateExpression(Expression expr, ParameterExpression param, EntityMetadata metadata, bool isQuerySyntax)
    {
        switch (expr)
        {
            case BinaryExpression bin:
                return TranslateBinary(bin, param, metadata, isQuerySyntax);

            case UnaryExpression { NodeType: ExpressionType.Not, Operand: var op }:
                return $"not ({TranslateExpression(op, param, metadata, isQuerySyntax)})";

            // Boolean property: x => x.IsPassive
            case MemberExpression m when m.Type == typeof(bool) && m.Expression == param:
                return $"{ResolveOo(m.Member.Name, metadata)} = 1";

            // Boxed boolean property (UnaryExpression wrapping value type)
            case UnaryExpression { NodeType: ExpressionType.Convert, Operand: MemberExpression m2 }
                when m2.Type == typeof(bool) && m2.Expression == param:
                return $"{ResolveOo(m2.Member.Name, metadata)} = 1";

            // list.Contains(x.Prop) — instance method on ICollection
            case MethodCallExpression { Method.Name: "Contains" } call
                when call.Object != null && IsParameterMember(call.Arguments[0], param):
                return TranslateContains(call.Object, call.Arguments[0], param, metadata, isQuerySyntax);

            // Enumerable.Contains(collection, x.Prop) — static extension method
            case MethodCallExpression { Method.Name: "Contains" } call
                when call.Object == null && call.Arguments.Count == 2 && IsParameterMember(call.Arguments[1], param):
                return TranslateContains(call.Arguments[0], call.Arguments[1], param, metadata, isQuerySyntax);

            default:
                throw new NotSupportedException(
                    $"Expression type '{expr.NodeType}' is not supported in Where predicate. Expression: {expr}");
        }
    }

    private static string TranslateContains(Expression collectionExpr, Expression propExpr, ParameterExpression param, EntityMetadata metadata, bool isQuerySyntax)
    {
        var collection = EvaluateExpression(collectionExpr) as IEnumerable
            ?? throw new NotSupportedException($"Could not evaluate collection in Contains expression: {collectionExpr}");

        var propName = ExtractPropertyName(propExpr);
        var ooExpr = ResolveOo(propName, metadata);
        metadata.Properties.TryGetValue(propName, out var propMeta);

        var values = new List<string>();
        foreach (var item in collection)
        {
            if (item != null)
                values.Add(FormatValue(item, propMeta));
        }

        if (values.Count == 0)
            throw new NotSupportedException("Contains() called with an empty collection — Opacc would return an error for a filter that can never match.");

        if (isQuerySyntax)
            // Opacc Query native IN syntax: Addr.Number=[10305,10403] or Addr.CountrySc=['CH','DE']
            return $"{ooExpr}=[{string.Join(",", values)}]";

        // GetBo/SaveBo/DeleteBo: OR chain
        if (values.Count == 1)
            return $"{ooExpr} = {values[0]}";

        return "(" + string.Join(" or ", values.Select(v => $"{ooExpr} = {v}")) + ")";
    }

private static string TranslateBinary(BinaryExpression expr, ParameterExpression param, EntityMetadata metadata, bool isQuerySyntax)
    {
        if (expr.NodeType == ExpressionType.AndAlso)
            return $"({TranslateExpression(expr.Left, param, metadata, isQuerySyntax)}) and ({TranslateExpression(expr.Right, param, metadata, isQuerySyntax)})";

        if (expr.NodeType == ExpressionType.OrElse)
            return $"({TranslateExpression(expr.Left, param, metadata, isQuerySyntax)}) or ({TranslateExpression(expr.Right, param, metadata, isQuerySyntax)})";

        var op = expr.NodeType switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "<>",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            _ => throw new NotSupportedException($"Binary operator '{expr.NodeType}' is not supported in Where predicate"),
        };

        bool leftIsParam = IsParameterMember(expr.Left, param);
        var (propExpr, valueExpr) = leftIsParam ? (expr.Left, expr.Right) : (expr.Right, expr.Left);

        // If value is on the left, flip directional operators
        var actualOp = leftIsParam ? op : FlipOperator(op);

        var propName = ExtractPropertyName(propExpr);
        var value = EvaluateExpression(valueExpr);

        // Null comparisons
        if (value == null)
        {
            return expr.NodeType == ExpressionType.Equal
                ? $"{ResolveOo(propName, metadata)} = ''"
                : $"{ResolveOo(propName, metadata)} <> ''";
        }

        var ooExpr = ResolveOo(propName, metadata);
        metadata.Properties.TryGetValue(propName, out var propMeta);
        var formattedValue = FormatValue(value, propMeta);

        return $"{ooExpr} {actualOp} {formattedValue}";
    }

    private static bool IsParameterMember(Expression expr, ParameterExpression param)
    {
        if (expr is MemberExpression m && m.Expression == param) return true;
        if (expr is UnaryExpression { Operand: MemberExpression m2 } && m2.Expression == param) return true;
        return false;
    }

    private static string ExtractPropertyName(Expression expr) => expr switch
    {
        MemberExpression m => m.Member.Name,
        UnaryExpression { Operand: MemberExpression m } => m.Member.Name,
        _ => throw new ArgumentException($"Cannot extract property name from expression: {expr}"),
    };

    private static object? EvaluateExpression(Expression expr) => expr switch
    {
        // Inline constant — no compilation needed (e.g. x => x.CustNo == 10403)
        ConstantExpression c => c.Value,

        // Closure capture — x => x.CustNo == localVar compiles to MemberAccess(Constant(closure), field)
        MemberExpression { Expression: ConstantExpression ce, Member: FieldInfo fi } => fi.GetValue(ce.Value),
        MemberExpression { Expression: ConstantExpression ce, Member: PropertyInfo pi } => pi.GetValue(ce.Value),

        // Fallback: full compile for any other expression shape
        _ => Expression.Lambda<Func<object?>>(Expression.Convert(expr, typeof(object))).Compile()(),
    };

    private static string ResolveOo(string propName, EntityMetadata metadata)
    {
        if (!metadata.Properties.TryGetValue(propName, out var propMeta))
            throw new ArgumentException($"Property '{propName}' not found in metadata for '{metadata.BoEntity}'");

        var ooExpr = propMeta.OoExpression;
        if (!ooExpr.Contains('.'))
            ooExpr = metadata.BoEntity + "." + ooExpr;
        return ooExpr;
    }

    private static string FlipOperator(string op) => op switch
    {
        ">" => "<",
        ">=" => "<=",
        "<" => ">",
        "<=" => ">=",
        _ => op,
    };

    private static string FormatValue(object value, PropertyMeta? propMeta) => value switch
    {
        string s => $"'{s.Replace("'", "''")}'",
        bool b => b ? "1" : "0",
        DateTime dt when propMeta?.DataType == OpaccDataType.Date => $"'{dt:yyyyMMdd}'",
        DateTime dt => $"'{dt:yyyyMMddHHmmss}'",
        int or long or short or byte => value.ToString()!,
        decimal d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
        double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
        float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => $"'{value.ToString()!.Replace("'", "''")}'",
    };
}
