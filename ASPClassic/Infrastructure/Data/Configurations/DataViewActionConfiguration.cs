using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ASPClassic.Infrastructure.Data.Configurations;

/// <summary>Deterministic EF relationship configuration for <see cref="ASPClassic.Domain.Entities.Data.DataViewAction"/>.</summary>
public class DataViewActionConfiguration : IEntityTypeConfiguration<ASPClassic.Domain.Entities.Data.DataViewAction>
{
    public void Configure(EntityTypeBuilder<ASPClassic.Domain.Entities.Data.DataViewAction> builder)
    {
        builder.HasOne(x => x.View).WithMany(y => y.DataViewActions).HasForeignKey(x => x.ViewID).OnDelete(DeleteBehavior.Restrict);
    }
}
