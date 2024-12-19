using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Snippets.Database;

public static class EfCoreExtensions
{
    /// <summary>
    /// Applies the value converter globally for all entity properties of the specified type.
    /// </summary>
    public static void HasGlobalValueConverter<TClrType>(this ModelBuilder builder, ValueConverter converter)
    {
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            foreach (var prop in entity.GetProperties())
            {
                if (prop.ClrType == typeof(TClrType))
                {
                    prop.SetValueConverter(converter);
                }
            }
        }
    }

    /// <summary>
    /// Finds the value converter for the specified entity property.
    /// </summary>
    public static ValueConverter? FindValueConverter<TEntity>(this IModel model, string property) =>
        model.FindEntityType(typeof(TEntity))?.GetProperty(property).GetValueConverter();
}
