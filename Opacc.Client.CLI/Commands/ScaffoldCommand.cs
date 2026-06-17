using System.CommandLine;
using System.CommandLine.Binding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Opacc.Client.CLI.Scaffold;
using Opacc.Client.Extensions;
using Opacc.Client.Transport;

namespace Opacc.Client.CLI.Commands;

// ====================================================================
// Options record — alle Scaffold-Parameter gebündelt
// ====================================================================

internal record ScaffoldOptions(
    string Output,
    string? Bo,
    string? Namespace,
    string Url,
    string ClientId,
    string AppId,
    string UserId,
    string Password,
    bool Verbose
);

// ====================================================================
// Binder — liest Optionen aus dem ParseResult
// ====================================================================

internal class ScaffoldOptionsBinder(
    Option<string>  output,
    Option<string?> bo,
    Option<string?> ns,
    Option<string>  url,
    Option<string>  clientId,
    Option<string>  appId,
    Option<string>  userId,
    Option<string>  password,
    Option<bool>    verbose
) : BinderBase<ScaffoldOptions>
{
    protected override ScaffoldOptions GetBoundValue(BindingContext ctx) => new(
        ctx.ParseResult.GetValueForOption(output)!,
        ctx.ParseResult.GetValueForOption(bo),
        ctx.ParseResult.GetValueForOption(ns),
        ctx.ParseResult.GetValueForOption(url)!,
        ctx.ParseResult.GetValueForOption(clientId)!,
        ctx.ParseResult.GetValueForOption(appId)!,
        ctx.ParseResult.GetValueForOption(userId)!,
        ctx.ParseResult.GetValueForOption(password)!,
        ctx.ParseResult.GetValueForOption(verbose)
    );
}

// ====================================================================
// Command
// ====================================================================

internal static class ScaffoldCommand
{
    public static Command Build()
    {
        var output    = new Option<string> (["--output",    "-o"], "Ausgabeverzeichnis für die generierten Model-Dateien") { IsRequired = true };
        var bo        = new Option<string?>(["--bo",        "-b"], "Einzelnes BO scaffolden (z.B. 'Addr'). Ohne Angabe: alle BOs.");
        var ns        = new Option<string?>(["--namespace", "-n"], "Namespace der generierten Klassen. Standard: wird aus .csproj + Pfad abgeleitet.");
        var url       = new Option<string> (["--url",       "-u"], "Opacc WebService URL") { IsRequired = true };
        var clientId  = new Option<string> (["--client-id"      ], "Opacc Mandant / Client ID") { IsRequired = true };
        var appId     = new Option<string> (["--app-id"         ], "Application ID / Consumer Name") { IsRequired = true };
        var userId    = new Option<string> (["--user-id"        ], "Opacc Benutzer-ID") { IsRequired = true };
        var password  = new Option<string> (["--password",  "-p"], "Opacc Passwort") { IsRequired = true };
        var verbose   = new Option<bool>   (["--verbose",   "-v"], getDefaultValue: () => false, description: "Zeigt Attribut-Rohdaten zur Diagnose an");

        var cmd = new Command("scaffold", "Generiert C#-Model-Klassen aus Opacc-Metadaten");
        cmd.AddOption(output);
        cmd.AddOption(bo);
        cmd.AddOption(ns);
        cmd.AddOption(url);
        cmd.AddOption(clientId);
        cmd.AddOption(appId);
        cmd.AddOption(userId);
        cmd.AddOption(password);
        cmd.AddOption(verbose);

        var binder = new ScaffoldOptionsBinder(output, bo, ns, url, clientId, appId, userId, password, verbose);
        cmd.SetHandler(RunAsync, binder);

        return cmd;
    }

    // ====================================================================
    // Handler
    // ====================================================================

    private static async Task RunAsync(ScaffoldOptions opt)
    {
        Directory.CreateDirectory(opt.Output);

        var resolvedNamespace = !string.IsNullOrWhiteSpace(opt.Namespace)
            ? opt.Namespace
            : ResolveNamespace(Path.GetFullPath(opt.Output));

        Console.WriteLine($"Namespace : {resolvedNamespace}");
        Console.WriteLine($"Output    : {Path.GetFullPath(opt.Output)}");
        Console.WriteLine();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddOpaccClient(options =>
        {
            options.ServiceUrl    = opt.Url;
            options.ClientId      = opt.ClientId;
            options.ApplicationId = opt.AppId;
            options.DefaultUserId = opt.UserId;
            options.DefaultPassword = opt.Password;
        });

        await using var sp = services.BuildServiceProvider();
        var transport  = sp.GetRequiredService<IOpaccTransport>();
        var infoClient = new OpaccInfoClient(transport);

        // BO-Liste immer laden — brauchen wir für BoIdIndex
        Console.Write("Lade BO-Liste... ");
        var allBoInfos = await infoClient.GetAllBosAsync();
        var boIndex = allBoInfos.ToDictionary(b => b.Name, b => b, StringComparer.OrdinalIgnoreCase);
        Console.WriteLine($"{allBoInfos.Count} BOs gefunden.");

        var boNames = !string.IsNullOrWhiteSpace(opt.Bo)
            ? [opt.Bo.Trim()]
            : allBoInfos.Select(b => b.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();

        int success = 0, failed = 0;

        foreach (var boName in boNames)
        {
            Console.Write($"  {boName,-30} ");

            try
            {
                var attrs = await infoClient.GetBoAttributesAsync(boName);

                if (attrs.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("(keine Attribute — übersprungen)");
                    Console.ResetColor();
                    continue;
                }

                // 1. Nur AV_PUB-Attribute aus GetInfoBoAttr scaffolden
                attrs = attrs.Where(a => string.Equals(a.ViewItemStateCd, "AV_PUB", StringComparison.OrdinalIgnoreCase)).ToList();

                // 2. Query-Verfügbarkeit: AV_PUB in GetInfoBoAttr aber nicht in GetInfoQuery
                //    → Property wird inkludiert, aber mit [BoPropertyNotAvailableInQuery] markiert
                var queryInfo = await infoClient.GetQueryFieldAvailabilityAsync(boName);
                if (queryInfo != null)
                {
                    attrs = attrs
                        .Select(a => queryInfo.IsAvailable(a)
                            ? a
                            : a with { NotAvailableInQuery = true })
                        .ToList();
                }

                if (opt.Verbose)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    foreach (var a in attrs)
                    {
                        var queryFlag = a.NotAvailableInQuery ? " [GetBo only]" : "";
                        Console.WriteLine($"    {a.AttrExpression,-40} {a.DataTypeCd}/{a.Format,-8} {a.Description}{queryFlag}");
                    }
                    Console.ResetColor();
                    Console.Write($"  {boName,-30} ");
                }

                var defaultIndexNo   = boIndex.TryGetValue(boName, out var info) ? info.BoIdIndex : 1;
                var allIndexSegments = await infoClient.GetAllIndexSegmentsAsync(boName);
                var code = ModelGenerator.Generate(boName, attrs, resolvedNamespace, defaultIndexNo, allIndexSegments);
                await File.WriteAllTextAsync(Path.Combine(opt.Output, boName + ".cs"), code);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"{attrs.Count} Attribute → {boName}.cs");
                Console.ResetColor();
                success++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"FEHLER: {ex.Message}");
                Console.ResetColor();
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Abgeschlossen: {success} erfolgreich, {failed} Fehler");

        // Services scaffolden (OpaccService.cs)
        await ScaffoldServicesAsync(infoClient, opt.Output, resolvedNamespace);
    }

    private static async Task ScaffoldServicesAsync(
        OpaccInfoClient infoClient, string outputDir, string namespaceName)
    {
        Console.WriteLine();
        Console.Write("Lade Service-Liste (GetInfoService)... ");

        var services = await infoClient.GetAllServicesAsync();

        if (services.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("(keine Services gefunden — OpaccService.cs wird nicht generiert)");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"{services.Count} Services gefunden.");

        // OpaccService.cs — Konstanten
        var code     = ServiceGenerator.Generate(services, namespaceName);
        var filePath = Path.Combine(outputDir, "OpaccService.cs");
        await File.WriteAllTextAsync(filePath, code);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  OpaccService.cs → {services.Count} Konstanten generiert.");
        Console.ResetColor();

        // Services/ — typisierte Request-Klassen
        await ScaffoldServiceRequestsAsync(infoClient, services, outputDir, namespaceName);
    }

    private static async Task ScaffoldServiceRequestsAsync(
        OpaccInfoClient infoClient,
        List<ServiceInfo> services,
        string outputDir,
        string namespaceName)
    {
        var servicesDir = Path.Combine(outputDir, "Services");
        Directory.CreateDirectory(servicesDir);

        Console.Write($"  Generiere Service-Request-Klassen ({services.Count} Services)... ");

        int success = 0, skipped = 0, failed = 0;

        foreach (var svc in services)
        {
            try
            {
                var attrs = await infoClient.GetServiceAttributesAsync(svc.OperationId);
                var requestCode = ServiceRequestGenerator.Generate(svc.OperationId, attrs, namespaceName);

                if (requestCode == null)
                {
                    skipped++;
                    continue;
                }

                await File.WriteAllTextAsync(
                    Path.Combine(servicesDir, svc.OperationId + ".cs"),
                    requestCode);
                success++;
            }
            catch
            {
                failed++;
            }
        }

        Console.ForegroundColor = failed > 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
        Console.WriteLine($"{success} Klassen, {skipped} ohne Input-Parameter, {failed} Fehler.");
        Console.ResetColor();
    }

    // ====================================================================
    // Namespace-Ableitung
    // ====================================================================

    /// <summary>
    /// Leitet den Namespace aus dem Output-Pfad ab:
    /// 1. Sucht das nächste .csproj im Elternpfad
    /// 2. Liest RootNamespace daraus (oder benutzt den Projektnamen)
    /// 3. Hängt den relativen Teilpfad vom Projektroot zum Output als Segmente an
    ///
    /// Beispiel:
    ///   .csproj  → C:\Projects\opacc-client\Opacc.Client\Opacc.Client.csproj
    ///   RootNamespace → "Opacc.Client"
    ///   output   → C:\Projects\opacc-client\Opacc.Client\Models
    ///   relPath  → Models
    ///   result   → "Opacc.Client.Models"
    /// </summary>
    private static string ResolveNamespace(string outputPath)
    {
        var (csprojPath, projectRoot) = FindCsproj(outputPath);

        string rootNamespace;
        if (csprojPath != null)
        {
            rootNamespace = ReadRootNamespace(csprojPath)
                ?? Path.GetFileNameWithoutExtension(csprojPath);
        }
        else
        {
            // Kein .csproj gefunden → Ordnernamen als Fallback
            rootNamespace = SanitizeSegment(
                Path.GetFileName(outputPath.TrimEnd(Path.DirectorySeparatorChar)));
        }

        // Relativer Pfad vom Projektroot zum Output → zusätzliche Namespace-Segmente
        if (projectRoot != null && outputPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
        {
            var rel = outputPath[projectRoot.Length..].Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.IsNullOrWhiteSpace(rel))
            {
                var segments = rel
                    .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar])
                    .Select(SanitizeSegment)
                    .Where(s => !string.IsNullOrWhiteSpace(s));
                rootNamespace = rootNamespace + "." + string.Join(".", segments);
            }
        }

        return rootNamespace;
    }

    private static (string? csprojPath, string? projectRoot) FindCsproj(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            var csproj = dir.GetFiles("*.csproj").FirstOrDefault();
            if (csproj != null)
                return (csproj.FullName, dir.FullName + Path.DirectorySeparatorChar);
            dir = dir.Parent;
        }
        return (null, null);
    }

    private static string? ReadRootNamespace(string csprojPath)
    {
        try
        {
            var xml = File.ReadAllText(csprojPath);
            var match = System.Text.RegularExpressions.Regex.Match(
                xml, @"<RootNamespace>(.*?)</RootNamespace>");
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }
        catch { return null; }
    }

    private static string SanitizeSegment(string segment)
    {
        var s = string.Concat(segment.Select(c => char.IsLetterOrDigit(c) || c == '.' ? c : '_'));
        return s.Trim('_', '.');
    }
}
