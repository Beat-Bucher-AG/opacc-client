# Opacc.Client.Sentry

Sentry performance tracing integration for [Opacc.Client](../Opacc.Client). Adds spans for every Opacc operation so they appear as child spans inside your Sentry transactions.

## Installation

Add both packages to your application project:

```xml
<PackageReference Include="Opacc.Client" Version="*" />
<PackageReference Include="Opacc.Client.Sentry" Version="*" />
```

## Setup

### 1. Initialize Sentry in your application

**ASP.NET Core:**
```csharp
builder.WebHost.UseSentry(options =>
{
    options.Dsn = "https://your-key@sentry.io/your-project-id";
    options.TracesSampleRate = 1.0; // required for spans to be captured
});
```

**Worker / console:**
```csharp
using var _ = SentrySdk.Init(options =>
{
    options.Dsn = "https://your-key@sentry.io/your-project-id";
    options.TracesSampleRate = 1.0;
});
```

### 2. Register Opacc.Client, then add Sentry instrumentation

```csharp
services.AddOpaccClient(options =>
{
    options.ServiceUrl   = "YourOpaccWebserviceUrl";
    options.ClientId     = "1";
    options.ApplicationId = "MyApp";
    options.DefaultUserId = 100;
    options.DefaultPassword = "...";
});

services.AddOpaccSentry(); // must come after AddOpaccClient()
```

That's it. No further configuration is required.

## What gets traced

Each operation becomes a child span under the active Sentry transaction:

| Operation | Span op | Span data |
|-----------|---------|-----------|
| `GetBoAsync` | `opacc.getbo` | `bo_entity`, `start`, `index_no`, `count`, `search_operator`, `filter`, `user_id` |
| `QueryAsync` | `opacc.query` | `bo_entity`, `max_rows`, `filter`, `scrolling`, `is_continuation`, `distinct`, `user_id` |
| `DeleteBoAsync` | `opacc.deletebo` | `bo_entity`, `start_keys`, `index_no`, `is_test`, `filter`, `user_id` |
| `SendRawAsync` | `opacc.raw` | `port_id`, `operation_id` |

Spans are finished with `SpanStatus.Ok` on success and with the exception status on failure.

## Purely optional

`Opacc.Client` has **no dependency on Sentry**. If `AddOpaccSentry()` is never called, nothing changes. If Sentry is initialized but no transaction is active when an Opacc call is made, the spans are silently skipped — zero overhead.
