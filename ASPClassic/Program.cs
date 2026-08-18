using ASPClassic.Infrastructure;
using ASPClassic.Infrastructure.Audit;
using ASPClassic.Infrastructure.Caching;
using ASPClassic.Infrastructure.Data;
using ASPClassic.Infrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Serilog;
using System.Threading.RateLimiting;

// ─── Serilog bootstrap ──────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ─── Serilog (full config from appsettings) ──────────────────────────────
    builder.Host.UseSerilog((context, services, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration)
                     .ReadFrom.Services(services)
                     .Enrich.FromLogContext());

    // ─── Razor Pages + Blazor Server ────────────────────────────────────────
    builder.Services.AddRazorPages();
    builder.Services.AddServerSideBlazor();

    // ─── MudBlazor ──────────────────────────────────────────────────────────
    builder.Services.AddMudServices();

    // ─── HttpContext accessor ────────────────────────────────────────────────
    builder.Services.AddHttpContextAccessor();

    // ─── EF Core — AddDbContextFactory ONLY (never AddDbContext for the same type) ─
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                           ?? "Data Source=aspclassic.db";

    builder.Services.AddDbContextFactory<ASPClassicVBScriptDbContext>(options =>
    {
        options.UseSqlite(connectionString, sql => sql.CommandTimeout(120));
    });

    // ─── Health Checks ───────────────────────────────────────────────────────
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<ASPClassicVBScriptDbContext>("database");

    // ─── Rate Limiter ────────────────────────────────────────────────────────
    builder.Services.AddRateLimiter(options =>
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.User.Identity?.Name
                              ?? httpContext.Request.Headers.Host.ToString(),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit        = 200,
                    Window             = TimeSpan.FromMinutes(1)
                }));

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    // ─── Antiforgery ─────────────────────────────────────────────────────────
    builder.Services.AddAntiforgery();

    // ─── Infrastructure — Singleton / Scoped helpers ─────────────────────────
    // AppState is Scoped — one instance per Blazor circuit (per user), NOT Singleton
    builder.Services.AddScoped<AppState>();

    // Infrastructure services consumed by application services
    builder.Services.AddScoped<DataViewCacheService>();
    builder.Services.AddScoped<JsonValueFormatter>();
    builder.Services.AddScoped<ASPClassic.Infrastructure.Engines.DataViewQueryEngine>();

    // ─── Validate DI on build (catches missing registrations at startup) ──────
    builder.Services.BuildServiceProvider(new ServiceProviderOptions
    {
        ValidateScopes  = true,
        ValidateOnBuild = false   // set false to allow AddDbContextFactory + health-check coexistence
    });

    // ── Application services (added deterministically — every type below is injected by the
    // generated code and would otherwise throw at runtime on first page load). Scoped, never
    // Singleton: in Blazor Server a Singleton is shared across every user's circuit.
    builder.Services.AddScoped<ASPClassic.Application.Services.Page.IPage404Service, ASPClassic.Application.Services.Page.Page404Service>();
    builder.Services.AddScoped<ASPClassic.Application.Services.Admin.IAdminDataviewfieldsService, ASPClassic.Application.Services.Admin.AdminDataviewfieldsService>();
    builder.Services.AddScoped<ASPClassic.Application.Services.Admin.IAdminDataviewsService, ASPClassic.Application.Services.Admin.AdminDataviewsService>();
    builder.Services.AddScoped<ASPClassic.Application.Services.Ajax.IAjaxDataview, ASPClassic.Application.Services.Ajax.AjaxDataview>();
    builder.Services.AddScoped<ASPClassic.Application.Services.Browse.IBrowseService, ASPClassic.Application.Services.Browse.BrowseService>();
    builder.Services.AddScoped<ASPClassic.Application.Services.Dataview.IDataviewService, ASPClassic.Application.Services.Dataview.DataviewService>();
    builder.Services.AddScoped<ASPClassic.Application.Services.Dataview.IDataviewNgdtService, ASPClassic.Application.Services.Dataview.DataviewNgdtService>();
    builder.Services.AddScoped<ASPClassic.Application.Services.View.IViewService, ASPClassic.Application.Services.View.ViewService>();
    builder.Services.AddScoped<ASPClassic.Application.Services.Inc.IIncFooterJscriptsService, ASPClassic.Application.Services.Inc.IncFooterJscriptsService>();
    builder.Services.AddScoped<ASPClassic.Application.Services.Inc.IIncConfigService, ASPClassic.Application.Services.Inc.IncConfigService>();
    builder.Services.AddScoped<ASPClassic.Application.Services.Data.IDataViewLookupClassExtendable, ASPClassic.Application.Services.Data.DataViewLookupClassExtendable>();
    builder.Services.AddScoped<ASPClassic.Application.Services.Data.IDataViewLookupCollectionClass, ASPClassic.Application.Services.Data.DataViewLookupCollectionClass>();
    builder.Services.AddScoped<ASPClassic.Application.Services.Inc.IIncCrudeconstantsService, ASPClassic.Application.Services.Inc.IncCrudeconstantsService>();
    builder.Services.AddScoped<ASPClassic.Application.Services.Inc.ISanitizerClass, ASPClassic.Application.Services.Inc.SanitizerClass>();
    builder.Services.AddScoped<ASPClassic.Application.Services.Inc.IIncFunctionsService, ASPClassic.Application.Services.Inc.IncFunctionsService>();
    builder.Services.AddScoped<ASPClassic.Infrastructure.Navigation.NavigationTreeBuilder>();

    // Write rules — what a particular table requires beyond being structurally complete. Registered
    // as a set so the generic writer never names one.
    builder.Services.AddScoped<ASPClassic.Application.Validation.IRecordWriteRule,
                               ASPClassic.Application.Validation.DataViewFieldWriteRule>();

    // ─── Build ────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ─── EnsureCreated, then apply the seed harvested from the source database ──
    using (var scope = app.Services.CreateScope())
    {
        var dbFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<ASPClassicVBScriptDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();

        // The rows the legacy database deployed with. In a data-driven application these are
        // its configuration, not sample data — without them every page renders empty. The
        // statements are guarded, so this neither overwrites nor deletes anything.
        var seedScript = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Persistence", "seed-data.sql");
        if (File.Exists(seedScript))
        {
            // Through a raw command, not ExecuteSqlRaw: that reads its SQL as a composite
            // format string, and seeded values legitimately contain braces.
            var seedConnection = db.Database.GetDbConnection();
            if (seedConnection.State != System.Data.ConnectionState.Open)
                await seedConnection.OpenAsync();

            await using var seedCommand = seedConnection.CreateCommand();
            seedCommand.CommandText = await File.ReadAllTextAsync(seedScript);
            await seedCommand.ExecuteNonQueryAsync();
        }

        // Demo data, kept separate from the harvested seed because Modernizer regenerates that file
        // on every run and would overwrite anything added to it. Defines one ordinary data view so
        // add and edit can be tried against a table the portal does not depend on.
        var demoScript = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Persistence", "demo-data.sql");
        if (File.Exists(demoScript))
        {
            var demoConnection = db.Database.GetDbConnection();
            if (demoConnection.State != System.Data.ConnectionState.Open)
                await demoConnection.OpenAsync();

            await using var demoCommand = demoConnection.CreateCommand();
            demoCommand.CommandText = await File.ReadAllTextAsync(demoScript);
            await demoCommand.ExecuteNonQueryAsync();
        }
    }

    // ─── HTTP pipeline ────────────────────────────────────────────────────────
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/aspclassic-vbscript/page500");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();

    app.UseSerilogRequestLogging();
    app.UseMiddleware<ASPClassic.Middleware.SecurityHeadersMiddleware>();

    app.UseRateLimiter();
    app.UseAntiforgery();

    app.MapHealthChecks("/health");

    // ─── Data view export ────────────────────────────────────────────────────
    // A download has to come from a real HTTP response: a Blazor circuit is a websocket and cannot
    // hand the browser a file. The page opens this endpoint, which streams the view's rows.
    app.MapGet("/aspclassic-vbscript/export/{viewId:int}", async (
        int viewId,
        string? format,
        string? search,
        HttpRequest request,
        ASPClassic.Application.Services.Dataview.IDataviewService dataview) =>
    {
        // The export has to be of what the screen is SHOWING. Handed no filters it returns the
        // whole table, so exporting one view's fields silently produced every field row in the
        // portal — a file that looks complete and answers a different question than the one asked.
        var filters = request.Query
            .Where(q => q.Key.StartsWith("filter.", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(q => q.Key["filter.".Length..], q => q.Value.ToString(),
                          StringComparer.OrdinalIgnoreCase);

        var rows = await dataview.GetDataViewRowsAsync(
            viewId, 0, int.MaxValue, search ?? string.Empty, false, filters);

        if (rows.Error is not null) return Results.Problem(rows.Error);
        if (rows.Data.Count == 0) return Results.NotFound("This view has no rows to export.");

        var columns = rows.Data[0].Keys.ToList();
        var view = await dataview.GetDataViewAsync(viewId);
        var name = string.Concat((view?.Title ?? ("view-" + viewId))
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-'));

        // A number written as text cannot be summed or sorted in a spreadsheet, which is most of
        // the reason to open one.
        static string Cell(string value) =>
            double.TryParse(value, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out _)
                ? "<Cell><Data ss:Type=\"Number\">" + System.Security.SecurityElement.Escape(value) + "</Data></Cell>"
                : "<Cell><Data ss:Type=\"String\">" + System.Security.SecurityElement.Escape(value) + "</Data></Cell>";

        if (string.Equals(format, "excel", StringComparison.OrdinalIgnoreCase))
        {
            // SpreadsheetML, which Excel opens natively. A real .xlsx is a zip container and would
            // need a library; this needs none, and is not a CSV wearing an .xls extension.
            var xml = new System.Text.StringBuilder();
            xml.AppendLine("<?xml version=\"1.0\"?>");
            xml.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
            xml.AppendLine("<Worksheet ss:Name=\"" + System.Security.SecurityElement.Escape(name) + "\"><Table>");

            xml.Append("<Row>");
            foreach (var c in columns)
                xml.Append("<Cell><Data ss:Type=\"String\">" + System.Security.SecurityElement.Escape(c) + "</Data></Cell>");
            xml.AppendLine("</Row>");

            foreach (var row in rows.Data)
            {
                xml.Append("<Row>");
                foreach (var c in columns)
                    xml.Append(Cell(row.TryGetValue(c, out var v) ? v ?? string.Empty : string.Empty));
                xml.AppendLine("</Row>");
            }

            xml.AppendLine("</Table></Worksheet></Workbook>");

            return Results.File(
                System.Text.Encoding.UTF8.GetBytes(xml.ToString()),
                "application/vnd.ms-excel", name + ".xls");
        }

        static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

        var csv = new System.Text.StringBuilder();
        csv.AppendLine(string.Join(",", columns.Select(Quote)));

        foreach (var row in rows.Data)
            csv.AppendLine(string.Join(",", columns.Select(c =>
                Quote(row.TryGetValue(c, out var v) ? v ?? string.Empty : string.Empty))));

        // The BOM is what makes Excel read the file as UTF-8 rather than the local codepage.
        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(csv.ToString())).ToArray();

        return Results.File(bytes, "text/csv", name + ".csv");
    });
    app.MapBlazorHub();
    app.MapFallbackToPage("/_Host");

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "ASPClassic host terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
