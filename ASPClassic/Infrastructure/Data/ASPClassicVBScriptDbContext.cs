using Microsoft.EntityFrameworkCore;
using ASPClassic.Domain.Entities.Core;
using ASPClassic.Domain.Entities.Data;
using ASPClassic.Infrastructure.Data;

namespace ASPClassic.Infrastructure.Data;

/// <summary>
/// EF Core DbContext mapping to existing ASPClassicVBScript database tables.
/// <para>Legacy source: New abstraction — database-first mapping; NO migrations, NO EnsureCreated.</para>
/// </summary>
public class ASPClassicVBScriptDbContext : DbContext
{
    public ASPClassicVBScriptDbContext(DbContextOptions<ASPClassicVBScriptDbContext> options)
        : base(options)
    {
    }

    public DbSet<DataView> DataViews => Set<DataView>();
    public DbSet<DataViewAction> DataViewActions => Set<DataViewAction>();
    public DbSet<DataViewField> DataViewFields => Set<DataViewField>();
    public DbSet<ASPClassic.Domain.Entities.Core.Navigation> Navigations
        => Set<ASPClassic.Domain.Entities.Core.Navigation>();
    public DbSet<DataViewDataTableFlags> DataViewDataTableFlags => Set<DataViewDataTableFlags>();
    public DbSet<DataViewFieldFlags> DataViewFieldFlags => Set<DataViewFieldFlags>();
    public DbSet<DataViewFieldTypes> DataViewFieldTypes => Set<DataViewFieldTypes>();
    public DbSet<DataViewFlags> DataViewFlags => Set<DataViewFlags>();
    public DbSet<DataViewModifierButtonStyles> DataViewModifierButtonStyles => Set<DataViewModifierButtonStyles>();
    public DbSet<DataViewPagingTypes> DataViewPagingTypes => Set<DataViewPagingTypes>();
    public DbSet<DataViewUriStyles> DataViewUriStyles => Set<DataViewUriStyles>();
    public DbSet<DataViewActionParameters> DataViewActionParameters => Set<DataViewActionParameters>();
    public DbSet<DataViewChart> DataViewCharts => Set<DataViewChart>();
    public DbSet<DataViewActionTypes> DataViewActionTypes => Set<DataViewActionTypes>();
    public DbSet<DataViewChartTypes> DataViewChartTypes => Set<DataViewChartTypes>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // DataView
        modelBuilder.Entity<DataView>(e =>
        {
            e.ToTable("DataView");
            e.HasKey(x => x.ViewID);
            e.Property(x => x.ViewID).HasColumnName("ViewID").ValueGeneratedOnAdd();
            e.Property(x => x.Title).HasColumnName("Title").HasMaxLength(100).IsRequired();
            e.Property(x => x.DataSource).HasColumnName("DataSource").HasMaxLength(200);
            e.Property(x => x.MainTable).HasColumnName("MainTable").HasMaxLength(300);
            e.Property(x => x.Primarykey).HasColumnName("Primarykey").HasMaxLength(300);
            e.Property(x => x.ModificationProcedure).HasColumnName("ModificationProcedure").HasMaxLength(300);
            e.Property(x => x.ViewProcedure).HasColumnName("ViewProcedure").HasMaxLength(300);
            e.Property(x => x.DeleteProcedure).HasColumnName("DeleteProcedure").HasMaxLength(300);
            e.Property(x => x.ViewDescription).HasColumnName("ViewDescription").HasMaxLength(4000);
            e.Property(x => x.OrderBy).HasColumnName("OrderBy").HasMaxLength(300);
            e.Property(x => x.Flags).HasColumnName("Flags").IsRequired();
            e.Property(x => x.DataTableModifierButtonStyle).HasColumnName("DataTableModifierButtonStyle").IsRequired();
            e.Property(x => x.DataTableFlags).HasColumnName("DataTableFlags").IsRequired();
            e.Property(x => x.DataTableDefaultPageSize).HasColumnName("DataTableDefaultPageSize").IsRequired();
            e.Property(x => x.DataTablePagingStyle).HasColumnName("DataTablePagingStyle").HasMaxLength(20).IsRequired();
            e.Property(x => x.Published).HasColumnName("Published").IsRequired();
            e.Property(x => x.RowReorderColumn).HasColumnName("RowReorderColumn").HasMaxLength(200);
            e.Property(x => x.IsSystemObject).HasColumnName("IsSystemObject").IsRequired();
            e.Property(x => x.CSSTable).HasColumnName("CSSTable").HasMaxLength(100).IsRequired();
        });

        // DataViewAction
        modelBuilder.Entity<DataViewAction>(e =>
        {
            e.ToTable("DataViewAction");
            e.HasKey(x => x.ActionID);
            e.Property(x => x.ActionID).HasColumnName("ActionID").ValueGeneratedOnAdd();
            e.Property(x => x.ViewID).HasColumnName("ViewID").IsRequired();
            e.Property(x => x.ActionLabel).HasColumnName("ActionLabel").HasMaxLength(100).IsRequired();
            e.Property(x => x.ParentActionID).HasColumnName("ParentActionID");
            e.Property(x => x.ActionTooltip).HasColumnName("ActionTooltip").HasMaxLength(300);
            e.Property(x => x.ActionDescription).HasColumnName("ActionDescription").HasMaxLength(1000);
            e.Property(x => x.ActionOrder).HasColumnName("ActionOrder").IsRequired();
            e.Property(x => x.RequireConfirmation).HasColumnName("RequireConfirmation").IsRequired();
            e.Property(x => x.OpenURLInNewWindow).HasColumnName("OpenURLInNewWindow");
            e.Property(x => x.ActionExpression).HasColumnName("ActionExpression");
            e.Property(x => x.GlyphIcon).HasColumnName("GlyphIcon").HasMaxLength(50);
            e.Property(x => x.IsPerRow).HasColumnName("IsPerRow").IsRequired();
            e.Property(x => x.CSSButton).HasColumnName("CSSButton").HasMaxLength(50);
            e.Property(x => x.ActionType).HasColumnName("ActionType").HasMaxLength(20).IsRequired();
            e.Property(x => x.DataViewTitle).HasColumnName("DataViewTitle");

            e.HasOne(x => x.View)
                .WithMany()
                .HasForeignKey(x => x.ViewID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // DataViewField
        modelBuilder.Entity<DataViewField>(e =>
        {
            e.ToTable("DataViewField");
            e.HasKey(x => x.FieldID);
            e.Property(x => x.FieldID).HasColumnName("FieldID").ValueGeneratedOnAdd();
            e.Property(x => x.ViewID).HasColumnName("ViewID").IsRequired();
            e.Property(x => x.FieldLabel).HasColumnName("FieldLabel").HasMaxLength(300).IsRequired();
            e.Property(x => x.FieldSource).HasColumnName("FieldSource").HasMaxLength(300);
            e.Property(x => x.FieldType).HasColumnName("FieldType").HasMaxLength(50).IsRequired();
            e.Property(x => x.FieldFlags).HasColumnName("FieldFlags").IsRequired();
            e.Property(x => x.FieldOrder).HasColumnName("FieldOrder").IsRequired();
            e.Property(x => x.DefaultValue).HasColumnName("DefaultValue").HasMaxLength(1000);
            e.Property(x => x.MaxLength).HasColumnName("MaxLength");
            e.Property(x => x.UriPath).HasColumnName("UriPath").HasMaxLength(1000);
            e.Property(x => x.UriStyle).HasColumnName("UriStyle");
            e.Property(x => x.LinkedTable).HasColumnName("LinkedTable").HasMaxLength(300);
            e.Property(x => x.LinkedTableValueField).HasColumnName("LinkedTableValueField").HasMaxLength(300);
            e.Property(x => x.LinkedTableTitleField).HasColumnName("LinkedTableTitleField").HasMaxLength(300);
            e.Property(x => x.LinkedTableGroupField).HasColumnName("LinkedTableGroupField").HasMaxLength(300);
            e.Property(x => x.LinkedTableGlyphField).HasColumnName("LinkedTableGlyphField").HasMaxLength(300);
            e.Property(x => x.LinkedTableTooltipField).HasColumnName("LinkedTableTooltipField").HasMaxLength(300);
            e.Property(x => x.LinkedTableAddition).HasColumnName("LinkedTableAddition").HasMaxLength(1000);
            e.Property(x => x.Width).HasColumnName("Width");
            e.Property(x => x.Height).HasColumnName("Height");
            e.Property(x => x.FieldDescription).HasColumnName("FieldDescription").HasMaxLength(4000);
            e.Property(x => x.FormatPattern).HasColumnName("FormatPattern").HasMaxLength(100);
            e.Property(x => x.FieldTooltip).HasColumnName("FieldTooltip").HasMaxLength(300);
            e.Property(x => x.FieldIdentifier).HasColumnName("FieldIdentifier").HasMaxLength(100);

            e.HasOne(x => x.View)
                .WithMany()
                .HasForeignKey(x => x.ViewID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Navigation
        modelBuilder.Entity<ASPClassic.Domain.Entities.Core.Navigation>(e =>
        {
            e.ToTable("Navigation");
            e.HasKey(x => x.NavId);
            e.Property(x => x.NavId).HasColumnName("NavId").ValueGeneratedOnAdd();
            e.Property(x => x.NavLabel).HasColumnName("NavLabel").HasMaxLength(300).IsRequired();
            e.Property(x => x.NavParentId).HasColumnName("NavParentId");
            e.Property(x => x.NavOrder).HasColumnName("NavOrder").IsRequired();
            e.Property(x => x.NavUri).HasColumnName("NavUri").HasMaxLength(1000);
            e.Property(x => x.NavGlyph).HasColumnName("NavGlyph").HasMaxLength(100);
            e.Property(x => x.NavTooltip).HasColumnName("NavTooltip").HasMaxLength(300);
            e.Property(x => x.ViewID).HasColumnName("ViewID");
            e.Property(x => x.OpenUriInIFRAME).HasColumnName("OpenUriInIFRAME").IsRequired();
        });

        // DataViewDataTableFlags
        modelBuilder.Entity<DataViewDataTableFlags>(e =>
        {
            e.ToTable("DataViewDataTableFlags");
            e.HasKey(x => x.FlagValue);
            e.Property(x => x.FlagValue).HasColumnName("FlagValue").HasMaxLength(4000).ValueGeneratedNever();
            e.Property(x => x.FlagLabel).HasColumnName("FlagLabel").HasMaxLength(4000);
            e.Property(x => x.FlagTooltip).HasColumnName("FlagTooltip").HasMaxLength(4000);
            e.Property(x => x.FlagGlyph).HasColumnName("FlagGlyph").HasMaxLength(4000);
            e.Property(x => x.FlagDefault).HasColumnName("FlagDefault").HasMaxLength(4000);
        });

        // DataViewFieldFlags
        modelBuilder.Entity<DataViewFieldFlags>(e =>
        {
            e.ToTable("DataViewFieldFlags");
            e.HasKey(x => x.FlagValue);
            e.Property(x => x.FlagValue).HasColumnName("FlagValue").HasMaxLength(4000).ValueGeneratedNever();
            e.Property(x => x.FlagLabel).HasColumnName("FlagLabel").HasMaxLength(4000);
            e.Property(x => x.FlagGlyph).HasColumnName("FlagGlyph").HasMaxLength(4000);
            e.Property(x => x.FlagDefault).HasColumnName("FlagDefault").HasMaxLength(4000);
        });

        // DataViewFieldTypes
        modelBuilder.Entity<DataViewFieldTypes>(e =>
        {
            e.ToTable("DataViewFieldTypes");
            e.HasKey(x => x.TypeValue);
            e.Property(x => x.TypeValue).HasColumnName("TypeValue").HasMaxLength(4000).ValueGeneratedNever();
            e.Property(x => x.TypeLabel).HasColumnName("TypeLabel").HasMaxLength(4000);
            e.Property(x => x.TypeWrappers).HasColumnName("TypeWrappers").HasMaxLength(4000);
            e.Property(x => x.TypeIdentifier).HasColumnName("TypeIdentifier").HasMaxLength(4000);
            e.Property(x => x.TypeGroup).HasColumnName("TypeGroup").HasMaxLength(4000);
        });

        // DataViewFlags
        modelBuilder.Entity<DataViewFlags>(e =>
        {
            e.ToTable("DataViewFlags");
            e.HasKey(x => x.FlagValue);
            e.Property(x => x.FlagValue).HasColumnName("FlagValue").HasMaxLength(4000).ValueGeneratedNever();
            e.Property(x => x.FlagLabel).HasColumnName("FlagLabel").HasMaxLength(4000);
            e.Property(x => x.FlagGlyph).HasColumnName("FlagGlyph").HasMaxLength(4000);
            e.Property(x => x.FlagDefault).HasColumnName("FlagDefault").HasMaxLength(4000);
        });

        // DataViewModifierButtonStyles
        modelBuilder.Entity<DataViewModifierButtonStyles>(e =>
        {
            e.ToTable("DataViewModifierButtonStyles");
            e.HasKey(x => x.StyleValue);
            e.Property(x => x.StyleValue).HasColumnName("StyleValue").HasMaxLength(4000).ValueGeneratedNever();
            e.Property(x => x.StyleLabel).HasColumnName("StyleLabel").HasMaxLength(4000);
            e.Property(x => x.StyleClass).HasColumnName("StyleClass").HasMaxLength(4000);
            e.Property(x => x.ShowText).HasColumnName("ShowText").HasMaxLength(4000);
            e.Property(x => x.ShowGlyph).HasColumnName("ShowGlyph").HasMaxLength(4000);
            e.Property(x => x.StyleDefault).HasColumnName("StyleDefault").HasMaxLength(4000);
        });

        // DataViewPagingTypes
        modelBuilder.Entity<DataViewPagingTypes>(e =>
        {
            e.ToTable("DataViewPagingTypes");
            e.HasKey(x => x.StyleValue);
            e.Property(x => x.StyleValue).HasColumnName("StyleValue").HasMaxLength(4000).ValueGeneratedNever();
            e.Property(x => x.StyleLabel).HasColumnName("StyleLabel").HasMaxLength(4000);
            e.Property(x => x.StyleDefault).HasColumnName("StyleDefault").HasMaxLength(4000);
        });

        // DataViewUriStyles
        modelBuilder.Entity<DataViewUriStyles>(e =>
        {
            e.ToTable("DataViewUriStyles");
            e.HasKey(x => x.StyleValue);
            e.Property(x => x.StyleValue).HasColumnName("StyleValue").HasMaxLength(4000).ValueGeneratedNever();
            e.Property(x => x.StyleLabel).HasColumnName("StyleLabel").HasMaxLength(4000);
            e.Property(x => x.StyleClass).HasColumnName("StyleClass").HasMaxLength(4000);
            e.Property(x => x.StyleGlyph).HasColumnName("StyleGlyph").HasMaxLength(4000);
            e.Property(x => x.StyleDefault).HasColumnName("StyleDefault").HasMaxLength(4000);
        });

        // DataViewActionParameters
        modelBuilder.Entity<DataViewActionParameters>(e =>
        {
            e.ToTable("DataViewActionParameters");
            e.HasKey(x => x.ActionParameterId);
            e.Property(x => x.ActionParameterId).HasColumnName("ActionParameterId").ValueGeneratedOnAdd();
            e.Property(x => x.ActionID).HasColumnName("ActionID").IsRequired();
            e.Property(x => x.ParamSystemName).HasColumnName("ParamSystemName").HasMaxLength(50).IsRequired();
            e.Property(x => x.ParamLabel).HasColumnName("ParamLabel").HasMaxLength(100).IsRequired();
            e.Property(x => x.ParamOrder).HasColumnName("ParamOrder").IsRequired();
            e.Property(x => x.ParamIsRequired).HasColumnName("ParamIsRequired").IsRequired();
            e.Property(x => x.ParamDefaultValue).HasColumnName("ParamDefaultValue").HasMaxLength(1000);
            e.Property(x => x.ParamTooltip).HasColumnName("ParamTooltip").HasMaxLength(255);
            e.Property(x => x.ParamDescription).HasColumnName("ParamDescription").HasMaxLength(1000);
            e.Property(x => x.ParamDataType).HasColumnName("ParamDataType").IsRequired();
            e.Property(x => x.ParamLinkedTable).HasColumnName("ParamLinkedTable").HasMaxLength(1000);
            e.Property(x => x.ParamLinkedTableTitleField).HasColumnName("ParamLinkedTableTitleField").HasMaxLength(200);
            e.Property(x => x.ParamLinkedTableValueField).HasColumnName("ParamLinkedTableValueField").HasMaxLength(200);
            e.Property(x => x.ParamLinkedTableGroupField).HasColumnName("ParamLinkedTableGroupField").HasMaxLength(200);
            e.Property(x => x.ParamLinkedTableAddition).HasColumnName("ParamLinkedTableAddition").HasMaxLength(1000);

            e.HasOne(x => x.Action)
                .WithMany()
                .HasForeignKey(x => x.ActionID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // DataViewChart
        modelBuilder.Entity<DataViewChart>(e =>
        {
            e.ToTable("DataViewChart");
            e.HasKey(x => x.ChartID);
            e.Property(x => x.ChartID).HasColumnName("ChartID").ValueGeneratedOnAdd();
            e.Property(x => x.ViewID).HasColumnName("ViewID").IsRequired();
            e.Property(x => x.ChartType).HasColumnName("ChartType").IsRequired();
            e.Property(x => x.ChartOrder).HasColumnName("ChartOrder");
            e.Property(x => x.ChartGridWidth).HasColumnName("ChartGridWidth").IsRequired();
            e.Property(x => x.ChartProperties).HasColumnName("ChartProperties").HasMaxLength(4000);
            e.Property(x => x.XField).HasColumnName("XField").HasMaxLength(300);
            e.Property(x => x.YField).HasColumnName("YField").HasMaxLength(300);
            e.Property(x => x.ZField).HasColumnName("ZField").HasMaxLength(300);

            e.HasOne(x => x.View)
                .WithMany()
                .HasForeignKey(x => x.ViewID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // DataViewActionTypes
        modelBuilder.Entity<DataViewActionTypes>(e =>
        {
            e.ToTable("DataViewActionTypes");
            e.HasKey(x => x.TypeValue);
            e.Property(x => x.TypeValue).HasColumnName("TypeValue").HasMaxLength(4000).ValueGeneratedNever();
            e.Property(x => x.TypeLabel).HasColumnName("TypeLabel").HasMaxLength(4000);
            e.Property(x => x.TypeDefault).HasColumnName("TypeDefault").HasMaxLength(4000);
        });

        // DataViewChartTypes
        modelBuilder.Entity<DataViewChartTypes>(e =>
        {
            e.ToTable("DataViewChartTypes");
            e.HasKey(x => x.TypeValue);
            e.Property(x => x.TypeValue).HasColumnName("TypeValue").HasMaxLength(4000).ValueGeneratedNever();
            e.Property(x => x.TypeLabel).HasColumnName("TypeLabel").HasMaxLength(4000);
            e.Property(x => x.TypeCode).HasColumnName("TypeCode").HasMaxLength(4000);
        });

        // Apply any IEntityTypeConfiguration implementations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ASPClassicVBScriptDbContext).Assembly);
    }
}
