using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Organization.Domain.ValueObjects;

namespace Organization.Infrastructure.Persistence.Converters;

/// <summary>
/// Conversor EF Core de <see cref="BusinessUnitName"/> para <see cref="string"/> e vice-versa.
/// </summary>
internal sealed class BusinessUnitNameConverter : ValueConverter<BusinessUnitName, string>
{
    /// <summary>Inicializa o conversor.</summary>
    public BusinessUnitNameConverter()
        : base(
            vo => vo.Value,
            s => BusinessUnitName.Create(s))
    {
    }
}
