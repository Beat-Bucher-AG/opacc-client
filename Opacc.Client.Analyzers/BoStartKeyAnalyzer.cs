using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Opacc.Client.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BoStartKeyAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = new(
        id: "OPACC002",
        title: "Incomplete Start key for BO index",
        messageFormat: "Index {0} requires '{1}' (segment {2}) to be initialized in the Start key",
        category: "Opacc.Client",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When calling .Start(T model), all key-segment properties for the selected BO index " +
                     "must be explicitly set in the object initializer. " +
                     "Required properties are identified by [BoId(indexNo, segmentNo)] on the model.");

    public static readonly DiagnosticDescriptor MissingStartKeyRule = new(
        id: "OPACC003",
        title: "Missing start key for SaveBo Update/CreateOrUpdate",
        messageFormat: "SaveBo {0} ({1}) on index {2} needs a start key but its leading key segment is " +
                       "not set (index key {3})",
        category: "Opacc.Client",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "SaveBo Update and CreateOrUpdate must identify the target record. Set the leading key " +
                     "segment(s) of the effective index via .Set(...), or supply .Start(...) / .Where() / .Filter(). " +
                     "Some BOs require an explicit .Start(...) because the leading segments come from a composite " +
                     "BoId field rather than the individual segment properties.");

    public static readonly DiagnosticDescriptor StartKeyOnCreateRule = new(
        id: "OPACC005",
        title: "Start key ignored for SaveBo Create",
        messageFormat: "SaveBo {0} (Create) ignores Start, because Create sends key fields as assignments instead",
        category: "Opacc.Client",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Create must not receive a start key. The key fields are written as assignments instead.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule, MissingStartKeyRule, StartKeyOnCreateRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeSaveChain, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext ctx)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;
        if (memberAccess.Name.Identifier.ValueText != "Start")
            return;
        if (invocation.ArgumentList.Arguments.Count != 1)
            return;

        if (ctx.SemanticModel.GetSymbolInfo(invocation, ctx.CancellationToken).Symbol
                is not IMethodSymbol method)
            return;

        if (!IsBuilderMethod(method))
            return;

        var modelType = GetBuilderModelType(method);
        if (modelType == null)
            return;

        // Distinguish Start(T model) from Start(object value) by checking the argument type
        var argExpr = invocation.ArgumentList.Arguments[0].Expression;
        var argType = ctx.SemanticModel.GetTypeInfo(argExpr, ctx.CancellationToken).Type;
        if (!SymbolEqualityComparer.Default.Equals(argType, modelType))
            return;

        // Only analyze inline object creation — tracking variable state is out of scope
        var initializer = GetObjectInitializer(argExpr);
        if (initializer == null)
            return;

        // Find .Index(n) anywhere in the fluent chain; fall back to [BoDefaultIndex]
        var indexNo = FindIndexInChain(invocation, ctx.SemanticModel)
                      ?? GetDefaultIndex(modelType);

        var keyProps = GetKeyProperties(modelType, indexNo);
        if (keyProps.Count == 0)
            return;

        var setProps = GetInitializedPropertyNames(initializer);

        foreach (var (propName, segNo) in keyProps)
        {
            if (!setProps.Contains(propName))
            {
                ctx.ReportDiagnostic(
                    Diagnostic.Create(Rule, argExpr.GetLocation(), indexNo, propName, segNo));
            }
        }
    }

    // ── SaveBo chain analysis (OPACC003/004/005) ──────────────────────────────

    /// <summary>
    /// Analyzes a full, inline <c>SaveBoAsync&lt;T&gt;()....ExecuteAsync()</c> chain to verify the
    /// start key is derivable. Only inline chains anchored on a <c>SaveBoAsync</c> creation call are
    /// inspected; indirect usages (builder stored in a variable / passed around) fall through to the
    /// runtime guard, avoiding false positives.
    /// </summary>
    private static void AnalyzeSaveChain(SyntaxNodeAnalysisContext ctx)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax execAccess)
            return;
        if (execAccess.Name.Identifier.ValueText != "ExecuteAsync")
            return;

        if (ctx.SemanticModel.GetSymbolInfo(invocation, ctx.CancellationToken).Symbol
                is not IMethodSymbol method)
            return;

        var modelType = GetSaveBoModelType(method);
        if (modelType == null)
            return;

        // Walk the receiver chain inward, collecting the fluent calls.
        var setProps = new HashSet<string>();
        string? operation = null;
        bool hasStart = false, hasWhereOrFilter = false, hasSetRaw = false, hasSetFrom = false;
        bool sawOrigin = false;
        int? indexNo = null;
        Location? startLocation = null;

        var receiver = execAccess.Expression;
        while (receiver is InvocationExpressionSyntax inv
               && inv.Expression is MemberAccessExpressionSyntax ma)
        {
            switch (ma.Name.Identifier.ValueText)
            {
                case "SaveBoAsync":
                    sawOrigin = true;
                    break;
                case "Create":
                    operation ??= "Create";
                    break;
                case "Update":
                    operation ??= "Update";
                    break;
                case "CreateOrUpdate":
                    operation ??= "CreateOrUpdate";
                    break;
                case "Start":
                    hasStart = true;
                    startLocation = ma.Name.GetLocation();
                    break;
                case "Where":
                case "Filter":
                    hasWhereOrFilter = true;
                    break;
                case "SetRaw":
                    hasSetRaw = true;
                    break;
                case "SetFrom":
                    hasSetFrom = true;
                    break;
                case "Index" when inv.ArgumentList.Arguments.Count >= 1:
                {
                    var v = ctx.SemanticModel.GetConstantValue(inv.ArgumentList.Arguments[0].Expression);
                    if (v.HasValue && v.Value is int n) indexNo = n;
                    break;
                }
                case "Set":
                {
                    foreach (var name in GetSetPropertyNames(inv))
                        setProps.Add(name);
                    break;
                }
            }

            receiver = ma.Expression;
        }

        // Only analyze complete inline chains created via SaveBoAsync(); otherwise defer to runtime.
        if (!sawOrigin)
            return;

        operation ??= "CreateOrUpdate"; // builder default

        if (operation == "Create")
        {
            if (hasStart && startLocation != null)
                ctx.ReportDiagnostic(Diagnostic.Create(
                    StartKeyOnCreateRule, startLocation, modelType.Name));
            return;
        }

        // Update and CreateOrUpdate (incl. the builder default) both need a start key to locate the
        // record. Suppressed when the record is located/keyed by other means.
        if (hasStart || hasWhereOrFilter || hasSetRaw || hasSetFrom)
            return;

        var effectiveIndex = indexNo ?? GetDefaultIndex(modelType);
        var keyProps = GetKeyProperties(modelType, effectiveIndex);
        if (keyProps.Count == 0)
            return; // unknown index / no key metadata — stay silent

        // A start key is a leading prefix of the index, so the first key segment must be set.
        if (!setProps.Contains(keyProps[0].PropName))
        {
            var segList = string.Join(", ", keyProps.Select(k => k.PropName));
            ctx.ReportDiagnostic(Diagnostic.Create(
                MissingStartKeyRule, execAccess.Name.GetLocation(), modelType.Name, operation, effectiveIndex, segList));
        }
    }

    private static INamedTypeSymbol? GetSaveBoModelType(IMethodSymbol method)
    {
        var containingType = method.ContainingType;
        var iface = containingType.OriginalDefinition.Name == "IOpaccSaveBo"
            ? containingType
            : containingType.AllInterfaces.FirstOrDefault(i => i.OriginalDefinition.Name == "IOpaccSaveBo");

        return iface?.TypeArguments.FirstOrDefault() as INamedTypeSymbol;
    }

    /// <summary>
    /// Returns the property names targeted by a <c>Set(...)</c> call. Handles both the single-field
    /// form (<c>x =&gt; x.Prop</c>) and the block form (<c>x =&gt; new T { A = ..., B = ... }</c>).
    /// </summary>
    private static IEnumerable<string> GetSetPropertyNames(InvocationExpressionSyntax setInvocation)
    {
        if (setInvocation.ArgumentList.Arguments.Count < 1)
            yield break;

        var arg = setInvocation.ArgumentList.Arguments[0].Expression;
        var body = arg switch
        {
            SimpleLambdaExpressionSyntax l        => l.Body as ExpressionSyntax,
            ParenthesizedLambdaExpressionSyntax p => p.Body as ExpressionSyntax,
            _ => null,
        };

        // Single-field form: x => x.Prop
        if (body is MemberAccessExpressionSyntax m)
        {
            yield return m.Name.Identifier.ValueText;
            yield break;
        }

        // Block form: x => new T { A = ..., B = ... }
        if (body != null && GetObjectInitializer(body) is { } init)
            foreach (var name in GetInitializedPropertyNames(init))
                yield return name;
    }

    // ── Chain walking ────────────────────────────────────────────────────────

    /// <summary>
    /// Searches for .Index(n) in both directions of the fluent chain relative to the Start() call.
    /// Inward  = earlier calls (.Index(n).Start(...))
    /// Outward = later  calls (.Start(...).Index(n).FirstAsync())
    /// </summary>
    private static int? FindIndexInChain(InvocationExpressionSyntax startCall, SemanticModel semanticModel)
    {
        // Walk inward: receiver chain of the Start() call
        var inner = (startCall.Expression as MemberAccessExpressionSyntax)?.Expression;
        while (inner is InvocationExpressionSyntax innerInvoc)
        {
            if ((innerInvoc.Expression as MemberAccessExpressionSyntax)?.Name.Identifier.ValueText == "Index"
                && innerInvoc.ArgumentList.Arguments.Count >= 1)
            {
                var v = semanticModel.GetConstantValue(innerInvoc.ArgumentList.Arguments[0].Expression);
                if (v.HasValue && v.Value is int n) return n;
            }
            inner = (innerInvoc.Expression as MemberAccessExpressionSyntax)?.Expression;
        }

        // Walk outward: parent invocations that wrap Start()
        var current = (SyntaxNode)startCall;
        while (current.Parent is MemberAccessExpressionSyntax ma)
        {
            if (ma.Name.Identifier.ValueText == "Index"
                && ma.Parent is InvocationExpressionSyntax outerInvoc
                && outerInvoc.ArgumentList.Arguments.Count >= 1)
            {
                var v = semanticModel.GetConstantValue(outerInvoc.ArgumentList.Arguments[0].Expression);
                if (v.HasValue && v.Value is int n) return n;
            }

            if (ma.Parent is InvocationExpressionSyntax next)
                current = next;
            else
                break;
        }

        return null;
    }

    // ── Symbol / attribute helpers ───────────────────────────────────────────

    private static bool IsBuilderMethod(IMethodSymbol method)
    {
        var type = method.ContainingType.OriginalDefinition;
        return IsBuilderInterface(type)
               || type.AllInterfaces.Any(i => IsBuilderInterface(i.OriginalDefinition));
    }

    private static bool IsBuilderInterface(INamedTypeSymbol type) =>
        type.Name is "IOpaccGetBo" or "IOpaccSaveBo" or "IOpaccDeleteBo";

    private static INamedTypeSymbol? GetBuilderModelType(IMethodSymbol method)
    {
        var containingType = method.ContainingType;
        var builderIface = IsBuilderInterface(containingType.OriginalDefinition)
            ? containingType
            : containingType.AllInterfaces.FirstOrDefault(
                i => IsBuilderInterface(i.OriginalDefinition));

        return builderIface?.TypeArguments.FirstOrDefault() as INamedTypeSymbol;
    }

    private static int GetDefaultIndex(INamedTypeSymbol modelType)
    {
        foreach (var attr in modelType.GetAttributes())
        {
            if (attr.AttributeClass?.Name is "BoDefaultIndexAttribute" or "BoDefaultIndex"
                && attr.ConstructorArguments.Length > 0
                && attr.ConstructorArguments[0].Value is int n)
                return n;
        }
        return 1;
    }

    /// <summary>Returns (propertyName, segmentNo) for the given index, ordered by segmentNo.</summary>
    private static List<(string PropName, int SegNo)> GetKeyProperties(
        INamedTypeSymbol modelType, int indexNo)
    {
        var result = new List<(string PropName, int SegNo)>();

        foreach (var member in modelType.GetMembers().OfType<IPropertySymbol>())
        {
            foreach (var attr in member.GetAttributes())
            {
                if (attr.AttributeClass?.Name is "BoIdAttribute" or "BoId"
                    && attr.ConstructorArguments.Length >= 2
                    && attr.ConstructorArguments[0].Value is int attrIndex
                    && attr.ConstructorArguments[1].Value is int segNo
                    && attrIndex == indexNo)
                {
                    result.Add((member.Name, segNo));
                }
            }
        }

        return result.OrderBy(x => x.SegNo).ToList();
    }

    // ── Syntax helpers ───────────────────────────────────────────────────────

    private static InitializerExpressionSyntax? GetObjectInitializer(ExpressionSyntax expr) =>
        expr switch
        {
            ObjectCreationExpressionSyntax { Initializer: var init }         => init,
            ImplicitObjectCreationExpressionSyntax { Initializer: var init } => init,
            _ => null,
        };

    private static IReadOnlySet<string> GetInitializedPropertyNames(
        InitializerExpressionSyntax initializer)
    {
        var names = new HashSet<string>();
        foreach (var item in initializer.Expressions)
        {
            if (item is AssignmentExpressionSyntax { Left: IdentifierNameSyntax ident })
                names.Add(ident.Identifier.ValueText);
        }
        return names;
    }
}
