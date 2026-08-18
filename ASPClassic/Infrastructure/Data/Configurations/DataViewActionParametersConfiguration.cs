using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ASPClassic.Infrastructure.Data.Configurations;

/// <summary>Deterministic EF relationship configuration for <see cref="ASPClassic.Domain.Entities.Data.DataViewActionParameters"/>.</summary>
public class DataViewActionParametersConfiguration : IEntityTypeConfiguration<ASPClassic.Domain.Entities.Data.DataViewActionParameters>
{
    public void Configure(EntityTypeBuilder<ASPClassic.Domain.Entities.Data.DataViewActionParameters> builder)
    {
        builder.HasOne(x => x.Action).WithMany(y => y.DataViewActionParameters).HasForeignKey(x => x.ActionID).OnDelete(DeleteBehavior.Restrict);
    }
}
