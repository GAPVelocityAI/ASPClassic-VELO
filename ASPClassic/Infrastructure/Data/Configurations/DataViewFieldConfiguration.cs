using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ASPClassic.Infrastructure.Data.Configurations;

/// <summary>Deterministic EF relationship configuration for <see cref="ASPClassic.Domain.Entities.Data.DataViewField"/>.</summary>
public class DataViewFieldConfiguration : IEntityTypeConfiguration<ASPClassic.Domain.Entities.Data.DataViewField>
{
    public void Configure(EntityTypeBuilder<ASPClassic.Domain.Entities.Data.DataViewField> builder)
    {
        builder.HasOne(x => x.View).WithMany(y => y.DataViewFields).HasForeignKey(x => x.ViewID).OnDelete(DeleteBehavior.Restrict);
    }
}
