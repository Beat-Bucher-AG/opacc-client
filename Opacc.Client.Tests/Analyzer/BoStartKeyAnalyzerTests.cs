using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Opacc.Client.Analyzers;
using Xunit;

namespace Opacc.Client.Tests.Analyzer;

/// <summary>
/// Tests for the SaveBo chain rules of <see cref="BoStartKeyAnalyzer"/>:
///   OPACC003 — Update/CreateOrUpdate with no key segment set (empty start key)
///   OPACC004 — start key with a non-contiguous segment gap
///   OPACC005 — .Start(...) on a Create (ignored)
///
/// Source strings use {|OPACCxxx:expr|} markup to assert exactly where the diagnostic fires.
/// Only inline chains created via SaveBoAsync&lt;T&gt;() are analyzed.
/// </summary>
public class BoStartKeyAnalyzerTests
{
    private static Task Verify(string source) =>
        new CSharpAnalyzerTest<BoStartKeyAnalyzer, DefaultVerifier>
        {
            TestCode = source
        }.RunAsync();

    private const string Stubs = """
        using System;
        using System.Linq.Expressions;
        using System.Threading.Tasks;
        using Opacc.Client.Attributes;
        using Opacc.Client.Operations.SaveBo;
        using Data;
        using TestModels;

        namespace Opacc.Client.Attributes
        {
            [AttributeUsage(AttributeTargets.Class)]
            public class BoAttribute : Attribute { public BoAttribute(string name) { } }
            [AttributeUsage(AttributeTargets.Class)]
            public class BoDefaultIndexAttribute : Attribute { public BoDefaultIndexAttribute(int indexNo) { } }
            [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
            public class BoIdAttribute : Attribute { public BoIdAttribute(int indexNo, int segmentNo) { } }
        }

        namespace Opacc.Client.Operations.SaveBo
        {
            public interface IOpaccSaveBo<T>
            {
                IOpaccSaveBo<T> Create();
                IOpaccSaveBo<T> Update();
                IOpaccSaveBo<T> CreateOrUpdate();
                IOpaccSaveBo<T> Start(object value);
                IOpaccSaveBo<T> Index(int indexNo, int? fixedSegments = null);
                IOpaccSaveBo<T> FixedSegments(int count);
                IOpaccSaveBo<T> Set<TValue>(Expression<Func<T, TValue>> property, TValue value);
                IOpaccSaveBo<T> Set(Expression<Func<T, T>> assignments);
                IOpaccSaveBo<T> SetRaw(string ooExpression, string value);
                IOpaccSaveBo<T> SetFrom(T model);
                IOpaccSaveBo<T> Where(Expression<Func<T, bool>> predicate);
                IOpaccSaveBo<T> Filter(string filter);
                Task ExecuteAsync();
            }
        }

        namespace Data
        {
            public static class Db
            {
                public static IOpaccSaveBo<T> SaveBoAsync<T>() => null;
            }
        }

        namespace TestModels
        {
            [Bo("Doc")]
            [BoDefaultIndex(4)]
            public class Doc
            {
                [BoId(4, 1)] public int Seg1 { get; set; }
                [BoId(4, 2)] public int Seg2 { get; set; }
                public string Name { get; set; } = "";
            }
        }

        """;

    // ── No-diagnostic cases ────────────────────────────────────────────

    [Fact]
    public async Task NoDiagnostic_Update_AllSegments()
    {
        var source = Stubs + """
            class Test
            {
                async Task Run()
                    => await Db.SaveBoAsync<Doc>().Update().Set(x => x.Seg1, 1).Set(x => x.Seg2, 2).ExecuteAsync();
            }
            """;
        await Verify(source);
    }

    [Fact]
    public async Task NoDiagnostic_Update_LeadingPartialKey()
    {
        var source = Stubs + """
            class Test
            {
                async Task Run()
                    => await Db.SaveBoAsync<Doc>().Update().Set(x => x.Seg1, 1).Set(x => x.Name, "y").ExecuteAsync();
            }
            """;
        await Verify(source);
    }

    [Fact]
    public async Task NoDiagnostic_Update_WithStart()
    {
        var source = Stubs + """
            class Test
            {
                async Task Run()
                    => await Db.SaveBoAsync<Doc>().Update().Start("1,2").Set(x => x.Name, "y").ExecuteAsync();
            }
            """;
        await Verify(source);
    }

    [Fact]
    public async Task NoDiagnostic_Update_WithWhere()
    {
        var source = Stubs + """
            class Test
            {
                async Task Run()
                    => await Db.SaveBoAsync<Doc>().Update().Where(x => x.Name == "y").Set(x => x.Name, "z").ExecuteAsync();
            }
            """;
        await Verify(source);
    }

    [Fact]
    public async Task NoDiagnostic_Create_WithoutStart()
    {
        var source = Stubs + """
            class Test
            {
                async Task Run()
                    => await Db.SaveBoAsync<Doc>().Create().Set(x => x.Name, "y").ExecuteAsync();
            }
            """;
        await Verify(source);
    }

    [Fact]
    public async Task NoDiagnostic_Update_BlockSetWithLeadingSegment()
    {
        // Block Set that sets the leading key segment → valid start key.
        var source = Stubs + """
            class Test
            {
                async Task Run()
                    => await Db.SaveBoAsync<Doc>().Update().Set(x => new Doc { Seg1 = 1, Seg2 = 2 }).ExecuteAsync();
            }
            """;
        await Verify(source);
    }

    [Fact]
    public async Task NoDiagnostic_CreateOrUpdate_WithStart()
    {
        // The composite-key pattern: leading segments supplied via .Start(...), values via .Set(...).
        var source = Stubs + """
            class Test
            {
                async Task Run()
                    => await Db.SaveBoAsync<Doc>().CreateOrUpdate().Start("1,2,3").Set(x => x.Name, "y").ExecuteAsync();
            }
            """;
        await Verify(source);
    }

    // ── OPACC003 — missing start key ───────────────────────────────────

    [Fact]
    public async Task Diagnostic_Update_NoKeySegments()
    {
        var source = Stubs + """
            class Test
            {
                async Task Run()
                    => await Db.SaveBoAsync<Doc>().Update().Set(x => x.Name, "y").{|OPACC003:ExecuteAsync|}();
            }
            """;
        await Verify(source);
    }

    [Fact]
    public async Task Diagnostic_Update_NonLeadingSegmentOnly()
    {
        // Update with only the second segment set → leading segment missing → no valid start key.
        var source = Stubs + """
            class Test
            {
                async Task Run()
                    => await Db.SaveBoAsync<Doc>().Update().Set(x => x.Seg2, 2).{|OPACC003:ExecuteAsync|}();
            }
            """;
        await Verify(source);
    }

    [Fact]
    public async Task Diagnostic_DefaultCreateOrUpdate_NoKeySegments()
    {
        // CreateOrUpdate (the builder default) also needs a start key to locate the record.
        var source = Stubs + """
            class Test
            {
                async Task Run()
                    => await Db.SaveBoAsync<Doc>().Set(x => x.Name, "y").{|OPACC003:ExecuteAsync|}();
            }
            """;
        await Verify(source);
    }

    [Fact]
    public async Task Diagnostic_Update_BlockSetWithoutLeadingSegment()
    {
        // Block Set that sets no key segment → missing start key.
        var source = Stubs + """
            class Test
            {
                async Task Run()
                    => await Db.SaveBoAsync<Doc>().Update().Set(x => new Doc { Name = "y" }).{|OPACC003:ExecuteAsync|}();
            }
            """;
        await Verify(source);
    }

    [Fact]
    public async Task Diagnostic_CreateOrUpdate_NonLeadingSegmentOnly()
    {
        // CreateOrUpdate with only a non-leading segment set (no .Start) → leading segment missing.
        var source = Stubs + """
            class Test
            {
                async Task Run()
                    => await Db.SaveBoAsync<Doc>().CreateOrUpdate().Set(x => x.Seg2, 2).Set(x => x.Name, "y").{|OPACC003:ExecuteAsync|}();
            }
            """;
        await Verify(source);
    }

    // ── OPACC005 — start on create ─────────────────────────────────────

    [Fact]
    public async Task Diagnostic_Create_WithStart()
    {
        var source = Stubs + """
            class Test
            {
                async Task Run()
                    => await Db.SaveBoAsync<Doc>().Create().{|OPACC005:Start|}("1,2").Set(x => x.Name, "y").ExecuteAsync();
            }
            """;
        await Verify(source);
    }
}
