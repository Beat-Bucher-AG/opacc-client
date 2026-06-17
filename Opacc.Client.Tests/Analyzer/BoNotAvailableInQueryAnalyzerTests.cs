using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Opacc.Client.Analyzers;
using Xunit;

namespace Opacc.Client.Tests.Analyzer;

/// <summary>
/// Tests for OPACC001: [BoPropertyNotAvailableInQuery] properties must not appear
/// inside lambdas passed to IOpaccQuery / IOpaccProjectedQuery methods.
///
/// Source strings use the {|OPACC001:expr|} markup to assert exactly
/// where the diagnostic fires.
/// </summary>
public class BoPropertyNotAvailableInQueryAnalyzerTests
{
    // DefaultVerifier throws InvalidOperationException for failures —
    // xUnit captures any exception as a test failure, so this works fine.
    private static Task Verify(string source) =>
        new CSharpAnalyzerTest<BoPropertyNotAvailableInQueryAnalyzer, DefaultVerifier>
        {
            TestCode = source
        }.RunAsync();

    // ------------------------------------------------------------------
    // Shared stubs — all usings FIRST, then namespace/type definitions,
    // then test class appended by each test without extra using directives.
    // ------------------------------------------------------------------

    private const string Stubs = """
        using System;
        using System.Linq.Expressions;
        using Opacc.Client.Attributes;
        using Opacc.Client.Operations.Query;
        using TestModels;

        namespace Opacc.Client.Attributes
        {
            [AttributeUsage(AttributeTargets.Property)]
            public class BoPropertyNotAvailableInQuery : Attribute { }
        }

        namespace Opacc.Client.Operations.Query
        {
            public interface IOpaccQuery<T> where T : class
            {
                IOpaccQuery<T> Where(Expression<Func<T, bool>> predicate);
                IOpaccQuery<T> Where(Expression<Func<T, object>> property, string op, object value);
                IOpaccQuery<T> Select(params Expression<Func<T, object>>[] properties);
                IOpaccQuery<T> OrderBy(Expression<Func<T, object>> property, bool descending = false);
                IOpaccQuery<T> OrderByAsDate(Expression<Func<T, object>> property, bool descending = false);
                IOpaccQuery<T> OrderByAsNmb(Expression<Func<T, object>> property, bool descending = false);
                IOpaccProjectedQuery<T, TResult> Select<TResult>(Expression<Func<T, TResult>> selector);
            }

            public interface IOpaccProjectedQuery<T, TResult> where T : class
            {
                IOpaccProjectedQuery<T, TResult> Where(Expression<Func<T, bool>> predicate);
                IOpaccProjectedQuery<T, TResult> OrderBy(Expression<Func<T, object>> property, bool descending = false);
            }
        }

        namespace TestModels
        {
            public class Item
            {
                public string Available { get; set; } = "";

                [BoPropertyNotAvailableInQuery]
                public string Restricted { get; set; } = "";

                [BoPropertyNotAvailableInQuery]
                public int RestrictedInt { get; set; }
            }
        }

        """;

    // ------------------------------------------------------------------
    // No-diagnostic cases
    // ------------------------------------------------------------------

    [Fact]
    public async Task NoDiagnostic_AvailableProperty_In_Where_Predicate()
    {
        var source = Stubs + """
            class Test
            {
                void Run(IOpaccQuery<Item> q)
                    => q.Where(x => x.Available == "ok");
            }
            """;

        await Verify(source);
    }

    [Fact]
    public async Task NoDiagnostic_AvailableProperty_In_Select()
    {
        var source = Stubs + """
            class Test
            {
                void Run(IOpaccQuery<Item> q)
                    => q.Select(x => x.Available);
            }
            """;

        await Verify(source);
    }

    [Fact]
    public async Task NoDiagnostic_AvailableProperty_In_OrderBy()
    {
        var source = Stubs + """
            class Test
            {
                void Run(IOpaccQuery<Item> q)
                    => q.OrderBy(x => x.Available);
            }
            """;

        await Verify(source);
    }

    [Fact]
    public async Task NoDiagnostic_RestrictedProperty_Outside_Query_Context()
    {
        // Accessing a [BoPropertyNotAvailableInQuery] property in a plain lambda
        // that is NOT passed to a query builder method → no error.
        var source = Stubs + """
            class Test
            {
                void Run(Item item)
                {
                    Func<Item, string> f = x => x.Restricted;
                    var direct = item.Restricted;
                }
            }
            """;

        await Verify(source);
    }

    [Fact]
    public async Task NoDiagnostic_AvailableProperty_In_ProjectedQuery_Where()
    {
        var source = Stubs + """
            class Test
            {
                void Run(IOpaccProjectedQuery<Item, string> q)
                    => q.Where(x => x.Available == "ok");
            }
            """;

        await Verify(source);
    }

    // ------------------------------------------------------------------
    // Diagnostic cases — IOpaccQuery<T>
    // ------------------------------------------------------------------

    [Fact]
    public async Task Diagnostic_RestrictedProperty_In_Where_Predicate()
    {
        var source = Stubs + """
            class Test
            {
                void Run(IOpaccQuery<Item> q)
                    => q.Where(x => {|OPACC001:x.Restricted|} == "bad");
            }
            """;

        await Verify(source);
    }

    [Fact]
    public async Task Diagnostic_RestrictedProperty_In_Where_ThreeParam()
    {
        var source = Stubs + """
            class Test
            {
                void Run(IOpaccQuery<Item> q)
                    => q.Where(x => {|OPACC001:x.Restricted|}, "=", "bad");
            }
            """;

        await Verify(source);
    }

    [Fact]
    public async Task Diagnostic_RestrictedProperty_In_Select_Params()
    {
        var source = Stubs + """
            class Test
            {
                void Run(IOpaccQuery<Item> q)
                    => q.Select(x => {|OPACC001:x.Restricted|});
            }
            """;

        await Verify(source);
    }

    [Fact]
    public async Task Diagnostic_RestrictedProperty_In_AnonymousType_Projection()
    {
        var source = Stubs + """
            class Test
            {
                void Run(IOpaccQuery<Item> q)
                    => q.Select(x => new { x.Available, Restricted = {|OPACC001:x.Restricted|} });
            }
            """;

        await Verify(source);
    }

    [Fact]
    public async Task Diagnostic_RestrictedProperty_In_OrderBy()
    {
        var source = Stubs + """
            class Test
            {
                void Run(IOpaccQuery<Item> q)
                    => q.OrderBy(x => {|OPACC001:x.Restricted|});
            }
            """;

        await Verify(source);
    }

    [Fact]
    public async Task Diagnostic_RestrictedProperty_In_OrderByAsDate()
    {
        var source = Stubs + """
            class Test
            {
                void Run(IOpaccQuery<Item> q)
                    => q.OrderByAsDate(x => {|OPACC001:x.Restricted|});
            }
            """;

        await Verify(source);
    }

    [Fact]
    public async Task Diagnostic_RestrictedProperty_In_OrderByAsNmb()
    {
        var source = Stubs + """
            class Test
            {
                void Run(IOpaccQuery<Item> q)
                    => q.OrderByAsNmb(x => {|OPACC001:x.Restricted|});
            }
            """;

        await Verify(source);
    }

    // ------------------------------------------------------------------
    // Diagnostic cases — IOpaccProjectedQuery<T, TResult>
    // ------------------------------------------------------------------

    [Fact]
    public async Task Diagnostic_RestrictedProperty_In_ProjectedQuery_Where()
    {
        var source = Stubs + """
            class Test
            {
                void Run(IOpaccProjectedQuery<Item, string> q)
                    => q.Where(x => {|OPACC001:x.Restricted|} == "bad");
            }
            """;

        await Verify(source);
    }

    [Fact]
    public async Task Diagnostic_RestrictedProperty_In_ProjectedQuery_OrderBy()
    {
        var source = Stubs + """
            class Test
            {
                void Run(IOpaccProjectedQuery<Item, string> q)
                    => q.OrderBy(x => {|OPACC001:x.Restricted|});
            }
            """;

        await Verify(source);
    }

    // ------------------------------------------------------------------
    // Multiple diagnostics in one call
    // ------------------------------------------------------------------

    [Fact]
    public async Task Multiple_Diagnostics_For_Multiple_Restricted_Properties()
    {
        var source = Stubs + """
            class Test
            {
                void Run(IOpaccQuery<Item> q)
                    => q.Where(x => {|OPACC001:x.Restricted|} == "bad" && {|OPACC001:x.RestrictedInt|} == 0);
            }
            """;

        await Verify(source);
    }

    [Fact]
    public async Task Multiple_Diagnostics_Across_Chained_Calls()
    {
        var source = Stubs + """
            class Test
            {
                void Run(IOpaccQuery<Item> q)
                    => q.Where(x => {|OPACC001:x.Restricted|} == "bad")
                        .OrderBy(x => {|OPACC001:x.Restricted|});
            }
            """;

        await Verify(source);
    }
}
