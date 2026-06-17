# Opacc.Client

A typed .NET client for the Opacc ERP WebService (OXAS). Provides a strongly-typed
API over the generic Opacc WCF endpoint for reading, querying and writing Business
Objects, with dependency-injection integration and a bundled Roslyn analyzer that
validates your BO model classes at compile time.

## Installation

```xml
<PackageReference Include="Opacc.Client" Version="*" />
```

## Quick start

```csharp
services.AddOpaccClient(options =>
{
    options.ServiceUrl      = "YourOpaccWebserviceUrl";
    options.ClientId        = "1";
    options.ApplicationId   = "MyApp";
    options.DefaultUserId   = 100;
    options.DefaultPassword = "...";
});
```

Inject `IOpaccClient` (or the lower-level `IOpaccTransport`) wherever you need it.

## Related packages

- **Opacc.Client.Sentry** — Sentry performance tracing for every Opacc operation.
- **Opacc.Client.CLI** (`opacc`) — a dotnet tool that scaffolds C# model classes from a live Opacc instance.

## Analyzer

This package ships a Roslyn analyzer (`analyzers/dotnet/cs`) that runs automatically
once installed — no extra reference required. It validates `[Bo]` / `[BoProperty]`
model definitions and reports common mapping mistakes as build diagnostics.
