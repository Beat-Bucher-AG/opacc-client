using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Opacc.Client.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BoPropertyNotAvailableInQueryAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = new(
        id: "OPACC001",
        title: "Property not available in Query",
        messageFormat: "'{0}' is marked [BoPropertyNotAvailableInQuery] and cannot be used in a Query request — use GetBo instead",
        category: "Opacc.Client",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Properties decorated with [BoPropertyNotAvailableInQuery] are not supported by the Opacc Query service. Remove them from Select/Where/OrderBy or switch to GetBo.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext ctx)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;

        // Resolve the called method
        if (ctx.SemanticModel.GetSymbolInfo(invocation, ctx.CancellationToken).Symbol
                is not IMethodSymbol method)
            return;

        // Only care about methods on IOpaccQuery<T> or IOpaccProjectedQuery<T,TResult>
        if (!IsQueryBuilderMethod(method))
            return;

        // Inspect every lambda argument
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.Expression is LambdaExpressionSyntax lambda)
                CheckLambda(lambda, ctx);
        }
    }

    private static void CheckLambda(LambdaExpressionSyntax lambda, SyntaxNodeAnalysisContext ctx)
    {
        foreach (var memberAccess in lambda.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            if (ctx.SemanticModel.GetSymbolInfo(memberAccess, ctx.CancellationToken).Symbol
                    is not IPropertySymbol property)
                continue;

            if (HasBoPropertyNotAvailableInQuery(property))
            {
                ctx.ReportDiagnostic(
                    Diagnostic.Create(Rule, memberAccess.GetLocation(), property.Name));
            }
        }
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private static bool IsQueryBuilderMethod(IMethodSymbol method)
    {
        // Check the declaring type itself (handles calls through interface references)
        var declaringType = method.ContainingType.OriginalDefinition;
        if (IsQueryInterface(declaringType))
            return true;

        // Check if the concrete class implements the interface (calls through concrete type)
        foreach (var iface in declaringType.AllInterfaces)
        {
            if (IsQueryInterface(iface.OriginalDefinition))
                return true;
        }

        return false;
    }

    private static bool IsQueryInterface(INamedTypeSymbol type)
    {
        var name = type.Name;
        return name is "IOpaccQuery" or "IOpaccProjectedQuery";
    }

    private static bool HasBoPropertyNotAvailableInQuery(IPropertySymbol property)
    {
        foreach (var attr in property.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "BoPropertyNotAvailableInQuery")
                return true;
        }
        return false;
    }
}
