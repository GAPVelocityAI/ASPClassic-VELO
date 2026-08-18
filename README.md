# ASPClassic Portal

A Blazor Server modernization of **CrudePortal**, an ASP Classic / VBScript application built on
AdminLTE. Produced by Modernizer from 4,144 lines of VBScript across 11 pages and 9 server-side
includes.

| | |
|---|---|
| Target | .NET 10 · Blazor Server · MudBlazor 8 |
| Data | EF Core 10 · SQLite (the legacy runs on SQL Server) |
| Logging | Serilog → `logs/aspclassic-{Date}.log` and console |
| Docs | [`technical-doc/ASPClassic-Portal-Technical-Documentation.html`](technical-doc/ASPClassic-Portal-Technical-Documentation.html) |

---

## Run it

```bash
dotnet build ASPClassic.sln
dotnet run --project ASPClassic
```

Or **F5** in Visual Studio.

| Scheme | URL |
|---|---|
| HTTPS | `https://localhost:7248` |
| HTTP | `http://localhost:5248` |

No database server is needed. On first run `EnsureCreated` builds the schema, then two scripts fill
it. Deleting `ASPClassic/aspclassic-dev.db` is the supported reset — schema, seed and demo data all
rebuild on the next start.

> **If the app will not start and Visual Studio says "unable to connect to web server"**, check
> whether the port is inside a Windows-reserved range:
> `netsh interface ipv4 show excludedportrange protocol=tcp`. Hyper-V and WinNAT reserve blocks of
> ephemeral ports and reshuffle them on reboot; a port inside one fails to bind with
> `SocketException 10013` and the host exits before it listens.

Development and Production use **different database files** — `aspclassic-dev.db` and
`aspclassic.db`. Running with `--no-launch-profile` omits `ASPNETCORE_ENVIRONMENT=Development` and
therefore opens the production one, which is a common source of "my data disappeared".

---

## What this application is

A **metadata-driven CRUD portal**. It has no screens of its own in the usual sense: a screen is a row
in a table.

| Table | Holds |
|---|---|
| `DataView` | One row per **screen** — which table it edits (`MainTable`), its key, what is permitted (`Flags`) |
| `DataViewField` | One row per **column on that screen** — label, which DB column (`FieldSource`), type, where it appears (`FieldFlags`) |
| `DataViewAction` | Toolbar and per-row **buttons** |
| `Navigation` | The curated sidebar **menu** |

One generic page reads that metadata and produces the grid, the form, the filters and the buttons.
Adding a screen means adding rows, not writing code.

### The portal edits itself

Four built-in views have **negative IDs** and point at the portal's own metadata tables:

```
-1  Manage Data Views   → edits DataView        (the screens)
-2  Data View Fields    → edits DataViewField   (the columns)
-3  Data View Actions   → edits DataViewAction  (the buttons)
-4  Navigation          → edits Navigation      (the menu)
```

This is the single most useful thing to know when reading either codebase. It is also why guards of
the form `viewId > 0` are wrong throughout — **a view id is non-zero, not positive.**

---

## Adding a screen

**1. Create the table.** The portal describes tables; it does not create them.

```sql
CREATE TABLE Inventory (
    ItemID INTEGER PRIMARY KEY, SKU TEXT NOT NULL, ItemName TEXT NOT NULL,
    Quantity INTEGER, UnitPrice REAL, InStock INTEGER, Notes TEXT );
```

There is no EF entity for such a table and it appears in no `DbSet` — the portal reads and writes it
dynamically from the view definition. That is the point of the design.

**2. Define the screen.** *Manage Data Views* → Add. `MainTable` and `Primarykey` tie it to the
table; `Flags` decides what is permitted. It appears in the sidebar once published.

**3. Describe the columns.** *Manage Fields* → **Auto-Initialize** reads the table and generates a
field row for every column not already described. Then set labels, tick **Show in Items List** for
grid columns, and point lookup fields at their linked table.

A field **describes** a column; it does not create one. Naming a column that does not exist is
allowed — you may be defining fields ahead of the DDL — but it warns, and offers to add the column
for you.

---

## Flag bitmasks

The lookup tables are authoritative and the legacy code agrees with them throughout.

**`DataViewField.FieldFlags`**

| Bit | Meaning |
|---|---|
| 1 | Show in Form |
| 2 | Required |
| 4 | Read Only |
| 8 | Show in Items List |
| 16 | Show in Search |

**`DataView.Flags`**

| Bit | Meaning |
|---|---|
| 1 | Allow Edit |
| 2 | Allow Add |
| 4 | Allow Delete |
| 8 | Allow Clone |
| 16 | Enable Form |
| 32 | Enable Items List |
| 64 | Enable Charts |
| 128 | Enable Custom Actions |
| 256 | Enable Browse Module |

There is no sixth or seventh field flag, and no "allow search" or "RTE" view flag.

---

## Three names that are easily confused

| Column | Is | Is not |
|---|---|---|
| `DataView.MainTable` | The table the screen edits | Created by the portal |
| `DataViewField.FieldSource` | The **database column** | The name used in URLs or form inputs |
| `DataViewField.FieldIdentifier` | The **client-side name** — form input, grid column, and what a `[search]` URL parameter refers to | A database column |

Conflating the last two is the most common mistake in this codebase.

---

## Data

Two scripts run at startup, in order.

**`Infrastructure/Persistence/seed-data.sql`** — 169 rows across 13 tables, harvested from the legacy
database project. **Regenerated by the migration tooling on every run, so do not hand-edit it.** These
are not sample rows; they are the application's own configuration. Without them the portal starts,
serves every route and renders nothing, with no error to explain it.

**`Infrastructure/Persistence/demo-data.sql`** — hand-written demo content, kept separate precisely
because the seed is overwritten. Runs after the seed, so it can also remove seeded rows. Contains a
demo view over `Navigation`, the `Inventory` worked example, and a column deliberately left without a
field so Auto-Initialize has something to do.

Both use guarded inserts — nothing is deleted, nothing is overwritten, and re-running is harmless.

---

## Layout

```
ASPClassic/
  Pages/            routable components, one per legacy .asp
    Inc/            ports of the inc_*.asp includes
  Shared/           MainLayout, dialogs, RichTextEditor
  Application/
    Services/       one per legacy page or include
    Validation/     write rules — see below
    DTOs/
  Infrastructure/
    Engines/        DataViewQueryEngine — the dynamic grid query
    Navigation/     sidebar tree
    Persistence/    seed-data.sql, demo-data.sql
    Data/           DbContext and configurations
  wwwroot/js/       richtext.js
ASPClassic.Tests/   smoke tests
technical-doc/      technical documentation (HTML)
```

### Write rules

The generic writer edits whatever table a view names and knows nothing about what any of them mean —
that is deliberate. Table-specific requirements live in `Application/Validation` as
`IRecordWriteRule` implementations, registered in `Program.cs`. The writer asks whether any rule
claims the table it is about to write; it never names one. Remove every rule and it behaves exactly
as before.

---

## Known divergences from the legacy

Each is commented at the point it occurs.

| Divergence | Why |
|---|---|
| Sidebar lists published non-system views, derived from `DataView` | The legacy menu is the curated `Navigation` table only, so a new view was invisible until a menu entry was added by hand |
| `admin-dataviews` has Open and Clone row buttons; ID column hidden | Requested. The legacy list has only Manage Fields / Edit / Delete, and does show the ID |
| Auto-Initialize button removed from the toolbar | Requested. The generation itself is still available |
| Field source is validated, with a warning and an "add the column" button | The legacy accepts any name silently and fails later on an unrelated screen |
| Rich text is a MudBlazor editor rather than Summernote | Summernote needs jQuery and Bootstrap for one field |
| Clipboard / PDF / Print export unavailable | Client-side DataTables features; the menu says so rather than reporting a file that never arrives |
| Stored `javascript` actions are not executed | They operate on the DataTables API, which does not exist here |

## Open items

- **No authentication or authorization.** Not present in the legacy and not invented here;
  `DataViewSecurityHelper` is the hook.
- Date field types (7, 8, 13) render as text inputs rather than pickers.
- Charts and action parameters are modelled and seeded but not implemented.
- Auto-Initialize types every textual column as Text Area, because SQLite reports `VARCHAR` and
  `TEXT` identically.
