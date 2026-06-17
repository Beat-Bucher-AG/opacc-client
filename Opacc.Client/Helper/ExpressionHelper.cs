using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Opacc.Client.Helper;

/// <summary>A property reference with optional language/currency suffix.</summary>
internal record SelectColumn(string ClrName, string? Suffix);

/// <summary>
/// A projected member with alias, source property name, optional suffix,
/// and optional relation info (when the column originates from a RelationAlias.Col() call).
/// </summary>
internal record ProjectedSelectColumn(
    string Alias,
    string ClrName,
    string? Suffix,
    string? RelationAlias = null,
    Type? RelatedSourceType = null);

internal static class ExpressionHelper
{
    /// <summary>
    /// Extrahiert den Property-Namen aus einer Expression.
    ///
    /// Unterstützt:
    ///   x => x.FullName                          → ["FullName"]
    ///   x => (object)x.Number                    → ["Number"]  (UnaryExpression bei Value Types)
    /// </summary>
    public static string GetPropertyName<T>(Expression<Func<T, object>> expression)
    {
        var member = expression.Body switch
        {
            MemberExpression m => m,
            UnaryExpression { Operand: MemberExpression m } => m,
            _ => throw new ArgumentException(
                $"Expression '{expression}' does not refer to a property. "
                    + $"Use either Select(x => x.Prop1, x => x.Prop2) or Select(x => new {{ x.Prop1, x.Prop2 }})."
            ),
        };

        return member.Member.Name;
    }

    /// <summary>
    /// Extrahiert Property-Namen aus einer Projection-Expression.
    ///
    /// Unterstützt:
    ///   x => new { x.FullName, x.City }                    → ["FullName", "City"]
    ///   x => new { x.FullName, Address = x.Line2 }         → ["Line2"]  (Source-Property, nicht Alias)
    ///   x => new { x.Number, x.FullName, x.City }          → ["Number", "FullName", "City"]
    /// </summary>
    public static List<string> GetPropertyNamesFromProjection<T>(Expression<Func<T, object>> expression)
    {
        var body = expression.Body;

        // Unwrap Convert (boxing für value types)
        if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            body = unary.Operand;

        if (body is NewExpression newExpr)
        {
            return newExpr.Arguments.Select(ExtractMemberName).Where(n => n != null).Cast<string>().ToList();
        }

        if (body is MemberInitExpression memberInit)
        {
            var names = new List<string>();

            // Aus dem Konstruktor
            foreach (var arg in memberInit.NewExpression.Arguments)
            {
                var name = ExtractMemberName(arg);
                if (name != null)
                    names.Add(name);
            }

            // Aus den Bindings
            foreach (var binding in memberInit.Bindings)
            {
                if (binding is MemberAssignment assignment)
                {
                    var name = ExtractMemberName(assignment.Expression);
                    if (name != null)
                        names.Add(name);
                }
            }

            return names;
        }

        if (body is MemberExpression member)
            return new List<string> { member.Member.Name };

        throw new ArgumentException(
            $"Cannot extract property names from expression '{expression}'. "
                + $"Use Select(x => x.Prop1, x => x.Prop2) or Select(x => new {{ x.Prop1, x.Prop2 }})."
        );
    }

    /// <summary>
    /// Extrahiert den Member-Namen aus einem Argument einer NewExpression.
    /// Handhabt MemberExpression und UnaryExpression (Convert bei value types).
    /// </summary>
    private static string? ExtractMemberName(Expression expression)
    {
        return expression switch
        {
            MemberExpression m => m.Member.Name,
            UnaryExpression { Operand: MemberExpression m } => m.Member.Name,
            _ => null,
        };
    }

    /// <summary>
    /// Extrahiert alle source-Property-Namen (auf T) die in einem Selector-Ausdruck
    /// referenziert werden. Unterstützt einfache Properties, anonyme Typen und DTOs.
    ///
    /// Beispiel:
    ///   x => new { x.FullName, x.City }    → ["FullName", "City"]
    ///   x => new SomeDto { Name = x.FullName } → ["FullName"]
    ///   x => x.City                         → ["City"]
    /// </summary>
    public static List<string> GetSourcePropertyNames<T, TResult>(Expression<Func<T, TResult>> expression)
    {
        var names = new HashSet<string>();
        CollectParameterMembers(expression.Body, expression.Parameters[0], names);
        return names.ToList();
    }

    private static void CollectParameterMembers(Expression expr, ParameterExpression param, HashSet<string> names)
    {
        switch (expr)
        {
            case MemberExpression m when m.Expression == param:
                names.Add(m.Member.Name);
                break;
            case UnaryExpression u:
                CollectParameterMembers(u.Operand, param, names);
                break;
            case NewExpression newExpr:
                foreach (var arg in newExpr.Arguments)
                    CollectParameterMembers(arg, param, names);
                break;
            case MemberInitExpression memberInit:
                foreach (var arg in memberInit.NewExpression.Arguments)
                    CollectParameterMembers(arg, param, names);
                foreach (var binding in memberInit.Bindings.OfType<MemberAssignment>())
                    CollectParameterMembers(binding.Expression, param, names);
                break;
            case ConditionalExpression cond:
                CollectParameterMembers(cond.Test, param, names);
                CollectParameterMembers(cond.IfTrue, param, names);
                CollectParameterMembers(cond.IfFalse, param, names);
                break;
            case BinaryExpression bin:
                CollectParameterMembers(bin.Left, param, names);
                CollectParameterMembers(bin.Right, param, names);
                break;
            case MethodCallExpression call:
                if (call.Object != null) CollectParameterMembers(call.Object, param, names);
                foreach (var arg in call.Arguments)
                    CollectParameterMembers(arg, param, names);
                break;
        }
    }

    // ================================================================
    // Language/Currency modifier extraction
    // ================================================================

    private static readonly HashSet<string> _modifierMethods = new(StringComparer.Ordinal)
    {
        nameof(OpaccSelectExtensions.WithLang),
        nameof(OpaccSelectExtensions.WithLangStrict),
        nameof(OpaccSelectExtensions.WithCurrency),
        nameof(OpaccSelectExtensions.WithCurrencyStrict),
    };

    /// <summary>
    /// Extracts a SelectColumn from a single Select lambda body.
    /// Handles plain property access (t.Name1) and modifier calls (t.Name1.WithLang(2)).
    /// </summary>
    public static SelectColumn ExtractSelectColumn(Expression body)
    {
        // Unwrap Convert (boxing for value types)
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
            body = unary.Operand;

        // Check for modifier method call: t.Prop.WithLang(2)
        if (body is MethodCallExpression call && IsModifierMethod(call))
        {
            var (clrName, suffix) = ParseModifierCall(call);
            return new SelectColumn(clrName, suffix);
        }

        // Plain property access: t.Prop
        if (body is MemberExpression member)
            return new SelectColumn(member.Member.Name, null);

        throw new ArgumentException(
            $"Expression '{body}' is not a property access or modifier call. " +
            "Use Select(x => x.Prop) or Select(x => x.Prop.WithLang(2)).");
    }

    /// <summary>
    /// Parses a projected selector expression and returns structured column info per member.
    /// For each member of the anonymous type / DTO, returns (Alias, SourceClrName, Suffix).
    /// Returns null if no modifiers are present (caller can use the fast path).
    /// </summary>
    public static List<ProjectedSelectColumn>? GetProjectedSelectColumns<T, TResult>(
        Expression<Func<T, TResult>> expression)
    {
        var body = expression.Body;
        var param = expression.Parameters[0];

        List<(string Alias, Expression Expr)> members;

        if (body is NewExpression newExpr && newExpr.Members != null)
        {
            members = newExpr.Members
                .Select((m, i) => (m.Name, newExpr.Arguments[i]))
                .ToList();
        }
        else if (body is MemberInitExpression memberInit)
        {
            members = new List<(string, Expression)>();
            for (int i = 0; i < memberInit.NewExpression.Arguments.Count; i++)
            {
                var ctorParam = memberInit.NewExpression.Constructor?.GetParameters()[i];
                if (ctorParam != null)
                    members.Add((ctorParam.Name!, memberInit.NewExpression.Arguments[i]));
            }
            foreach (var binding in memberInit.Bindings.OfType<MemberAssignment>())
                members.Add((binding.Member.Name, binding.Expression));
        }
        else
        {
            return null; // Single property, no projection
        }

        var hasModifier = false;
        var result = new List<ProjectedSelectColumn>(members.Count);

        foreach (var (alias, expr) in members)
        {
            var unwrapped = expr;
            if (unwrapped is UnaryExpression { NodeType: ExpressionType.Convert } u)
                unwrapped = u.Operand;

            if (unwrapped is MethodCallExpression call && IsModifierMethod(call))
            {
                var (clrName, suffix) = ParseModifierCall(call);
                result.Add(new ProjectedSelectColumn(alias, clrName, suffix));
                hasModifier = true;
            }
            else if (unwrapped is MethodCallExpression relCall && IsRelationColMethod(relCall))
            {
                var (clrName, relAlias, relatedType) = ParseRelationColCall(relCall);
                result.Add(new ProjectedSelectColumn(alias, clrName, null, relAlias, relatedType));
                hasModifier = true;
            }
            else
            {
                var clrName = ExtractSourcePropertyName(unwrapped, param);
                result.Add(new ProjectedSelectColumn(alias, clrName, null));
            }
        }

        return hasModifier ? result : null;
    }

    private static bool IsModifierMethod(MethodCallExpression call)
        => call.Method.DeclaringType == typeof(OpaccSelectExtensions)
           && _modifierMethods.Contains(call.Method.Name);

    // ================================================================
    // RelationAlias.Col() detection
    // ================================================================

    private static bool IsRelationColMethod(MethodCallExpression call)
        => call.Method.Name == "Col"
           && call.Method.DeclaringType is { IsGenericType: true } dt
           && dt.GetGenericTypeDefinition() == typeof(RelationAlias<>);

    /// <summary>
    /// Parses a RelationAlias&lt;T&gt;.Col(a => a.Property) call.
    /// Returns the CLR property name on TSource, the relation alias string, and the TSource type.
    /// </summary>
    private static (string ClrName, string RelAlias, Type RelatedType) ParseRelationColCall(
        MethodCallExpression call)
    {
        // Evaluate the captured RelationAlias<T> instance from the closure
        var relAliasObj = Expression.Lambda(call.Object!).Compile().DynamicInvoke() as IRelationAlias
            ?? throw new ArgumentException(
                $"Could not evaluate RelationAlias from expression '{call.Object}'. " +
                "Make sure the variable is a RelationAlias<T> instance.");

        // The argument is the quoted inner lambda: Quote(a => a.FullName)
        var argExpr = call.Arguments[0];
        if (argExpr is UnaryExpression { NodeType: ExpressionType.Quote } quoted)
            argExpr = quoted.Operand;

        if (argExpr is LambdaExpression { Body: MemberExpression member })
            return (member.Member.Name, relAliasObj.Alias, relAliasObj.SourceType);

        throw new ArgumentException(
            $"Cannot extract property name from Col() argument '{call.Arguments[0]}'. " +
            "Use a simple property access, e.g. Col(a => a.FullName).");
    }

    private static (string ClrName, string Suffix) ParseModifierCall(MethodCallExpression call)
    {
        // Extension method: first arg is 'this' (the property access), second is the code
        var propExpr = call.Arguments[0];
        if (propExpr is UnaryExpression { NodeType: ExpressionType.Convert } u)
            propExpr = u.Operand;

        var clrName = propExpr switch
        {
            MemberExpression m => m.Member.Name,
            _ => throw new ArgumentException(
                $"Cannot extract property name from '{propExpr}' in modifier call '{call.Method.Name}'.")
        };

        var code = (int)((ConstantExpression)call.Arguments[1]).Value!;

        var suffix = call.Method.Name switch
        {
            nameof(OpaccSelectExtensions.WithLang) => $"@{code}",
            nameof(OpaccSelectExtensions.WithLangStrict) => $"@@{code}",
            nameof(OpaccSelectExtensions.WithCurrency) => $"${code}",
            nameof(OpaccSelectExtensions.WithCurrencyStrict) => $"$${code}",
            _ => throw new ArgumentException($"Unknown modifier method: {call.Method.Name}")
        };

        return (clrName, suffix);
    }

    /// <summary>
    /// Extracts the source property name from an expression that references the parameter.
    /// </summary>
    private static string ExtractSourcePropertyName(Expression expr, ParameterExpression param)
    {
        if (expr is MemberExpression m && m.Expression == param)
            return m.Member.Name;
        if (expr is UnaryExpression { Operand: MemberExpression m2 } && m2.Expression == param)
            return m2.Member.Name;

        // Fallback: collect all parameter members (takes first)
        var names = new HashSet<string>();
        CollectParameterMembers(expr, param, names);
        return names.FirstOrDefault()
            ?? throw new ArgumentException($"Cannot extract source property from expression: {expr}");
    }
}
