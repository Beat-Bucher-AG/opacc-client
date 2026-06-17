# Opacc.Client.CLI

Command-line tool for the `Opacc.Client` library. Currently provides the `scaffold` command, which introspects a live Opacc instance and generates ready-to-use C# model classes.

---

## Installation

### Local (development)

Run directly from the project directory without installing:

```bash
cd opacc-client/Opacc.Client.CLI
dotnet run -- <command> [options]
```

The `--` separator is required — everything after it is passed to the CLI, not to `dotnet run`.

### Global .NET tool

```bash
dotnet pack
dotnet tool install --global --add-source ./nupkg Opacc.Client.CLI
```

Once installed, the tool is available as `opacc`:

```bash
opacc scaffold [options]
```

---

## Commands

### `scaffold`

Connects to an Opacc instance, reads all Business Object metadata via `GetInfoBo` and `GetInfoBoAttr`, and writes one `.cs` file per BO into the output directory.

#### Options

| Option | Short | Required | Description |
|---|---|---|---|
| `--output <path>` | `-o` | Yes | Directory where `.cs` files are written. Created if it does not exist. |
| `--url <url>` | `-u` | Yes | Opacc WebService endpoint URL. |
| `--client-id <id>` | | Yes | Opacc Mandant / Client ID (e.g. `"1"`). |
| `--app-id <name>` | | Yes | Application / Consumer name (appears in Opacc logs). |
| `--user-id <id>` | | Yes | Opacc user ID used to authenticate. |
| `--password <pwd>` | `-p` | Yes | Password for the given user. Accepts both plain text and the encrypted format Opacc uses internally. |
| `--bo <name>` | `-b` | No | Scaffold a single BO (e.g. `Addr`). When omitted, **all** BOs are scaffolded. |
| `--namespace <ns>` | `-n` | No | Namespace for the generated classes. When omitted, derived automatically (see below). |
| `--verbose` | `-v` | No | Print each attribute's raw `DataTypeCd`, `Format`, and description before generating. Useful for diagnosing unexpected type mappings. |

#### Namespace resolution

The namespace is determined in this order:

1. **Explicit** — `--namespace Opacc.Client.Models` is used as-is.
2. **Automatic** — the tool walks up from the output directory to find the nearest `.csproj` file.
   - Reads `<RootNamespace>` from that file, or falls back to the project filename without extension.
   - Appends the relative path from the project root to the output directory as additional segments.

Example:
```
.csproj   →  opacc-client/Opacc.Client/Opacc.Client.csproj
              RootNamespace = "Opacc.Client"
--output  →  opacc-client/Opacc.Client/Models
relative  →  Models
result    →  namespace Opacc.Client.Models;
```

The resolved namespace is printed before scaffolding begins so you can verify it before all files are written.

---

## Examples

**Scaffold a single BO for quick verification:**

```bash
dotnet run -- scaffold \
  --url "YourOpaccWebserviceUrl" \
  --client-id "1" \
  --app-id "MyApp" \
  --user-id "500" \
  --password "..." \
  --bo Addr \
  --output "./Models" \
  --verbose
```

**Scaffold all BOs into the library's Models folder:**

```bash
dotnet run -- scaffold \
  --url "YourOpaccWebserviceUrl" \
  --client-id "1" \
  --app-id "MyApp" \
  --user-id "500" \
  --password "..." \
  --output "C:/Projects/opacc-client/Opacc.Client/Models"
```

**Override namespace explicitly:**

```bash
dotnet run -- scaffold ... \
  --output "./out" \
  --namespace "MyCompany.Erp.Models"
```

---

## Generated output

Each BO produces one file, e.g. `Addr.cs`:

```csharp
using Opacc.Client.Attributes;
using Opacc.Client.Enums;

namespace Opacc.Client.Models;

[Bo("Addr")]
[BoIndex(1)]
public class Addr
{
    public int Number { get; set; }

    public string FullName { get; set; } = "";

    public string City { get; set; } = "";

    [BoProperty("Addr.SomeDate", OpaccDataType.Date)]
    public DateTime SomeDate { get; set; }

    /// <summary>Customer BoId reference</summary>
    [BoProperty("Addr.CustBoId")]
    public string CustBoId { get; set; } = "";
}
```

### Attribute rules

| Condition | Generated attribute |
|---|---|
| Expression matches `BoName.PropertyName` by convention | None — property name alone is sufficient |
| Expression differs from convention (e.g. cross-BO like `Cust.Remark`) | `[BoProperty("Cust.Remark")]` |
| Date field (DataTypeCd = `D`) | `[BoProperty("...", OpaccDataType.Date)]` |
| Date field that also follows convention | `[BoProperty("Addr.SomeDate", OpaccDataType.Date)]` |

### Data type mapping

| Opacc `DataTypeCd` | Format example | C# type |
|---|---|---|
| `A` | `50` | `string` |
| `N` | `8.0` | `int` |
| `N` | `8.2` | `decimal` |
| `D` | — | `DateTime` + `OpaccDataType.Date` |
| `B` | — | `bool` |
| `T` | — | `DateTime` |
| anything else | — | `string` (safe fallback) |

Numeric format `"8.0"` means 8 digits, 0 decimal places → `int`. Format `"8.2"` means 2 decimal places → `decimal`.

---

## How it works internally

```
scaffold
  │
  ├── Biz.GetInfoBo()
  │     → returns all BO names (column: "Bo")
  │
  └── for each BO:
        Biz.GetInfoBoAttr(boName)
          → returns all attributes (columns: "BoAttr", "DataTypeCd", "Format", ...)
          → TypeMapper maps DataTypeCd + Format to C# type
          → ModelGenerator emits the .cs file
```

Both service calls go through `IOpaccTransport.SendRawAsync`, the same authenticated WCF session pool used by the main library. Sessions are created on first use and reused for the duration of the command.

---

## Troubleshooting

**`namespace Models;` instead of a proper namespace**
The tool could not find a `.csproj` above the output directory. Either move the output inside a project folder, or pass `--namespace` explicitly.

**BO scaffolded with 0 attributes — skipped**
`GetInfoBoAttr` returned an empty response for that BO. This is normal for internal system BOs that have no accessible attributes.

**`FaultException` on a specific attribute**
The generated model contains a property that no longer exists in this Opacc version. Remove the property or mark it with a custom ignore attribute. Run `--verbose` to see the raw attribute list and compare against your Opacc instance.

**Unknown column names in verbose output**
The `--verbose` flag prints `DataTypeCd/Format` for each attribute. If these look unexpected, the `OpaccInfoClient` has a `DumpColumns()` helper you can call in code to print the raw response column names for diagnostics.
