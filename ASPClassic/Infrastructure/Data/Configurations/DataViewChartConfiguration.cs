using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ASPClassic.Infrastructure.Data.Configurations;

/// <summary>Deterministic EF relationship configuration for <see cref="ASPClassic.Domain.Entities.Data.DataViewChart"/>.</summary>
public class DataViewChartConfiguration : IEntityTypeConfiguration<ASPClassic.Domain.Entities.Data.DataViewChart>
{
    public void Configure(EntityTypeBuilder<ASPClassic.Domain.Entities.Data.DataViewChart> builder)
    {
        builder.HasOne(x => x.View).WithMany(y => y.DataViewCharts).HasForeignKey(x => x.ViewID).OnDelete(DeleteBehavior.Restrict);
    }
}
