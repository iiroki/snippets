using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Snippets.Database;

public static class EfCoreExtensions
{
    /// <summary>
    /// Replaces <c>@@table</c> with the table name extracted from the <c>DbSet.EntityType</c> and
    /// then calls <c>DbSet.FromSql()</c>.
    /// </summary>
    public static IQueryable<TEntity> FromSqlWithTable<TEntity>(this DbSet<TEntity> set, FormattableString sql)
        where TEntity : class
    {
        const string key = "@@table";
        if (!sql.Format.Contains(key))
        {
            return set.FromSql(sql);
        }

        var table = set.EntityType.GetSchemaQualifiedTableName();
        if (string.IsNullOrWhiteSpace(table))
        {
            throw new InvalidOperationException($"Could not extract table name from the DbSet: {set.EntityType.Name}");
        }

        var rawSqlWithTable = sql.Format.Replace(key, table);
        var sqlWithTable = FormattableStringFactory.Create(rawSqlWithTable, sql.GetArguments());
        return set.FromSql(sqlWithTable);
    }

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
