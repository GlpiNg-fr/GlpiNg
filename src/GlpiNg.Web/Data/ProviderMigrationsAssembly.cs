using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GlpiNg.Web.Data;

/// <summary>
/// Jeu de migrations propre à un fournisseur, rangé dans un sous-espace de noms de
/// <c>GlpiNg.Web.Migrations</c>. Les migrations de SQL Server (types <c>nvarchar</c>, identités...)
/// ne se rejouent pas sur MySQL/PostgreSQL, mais EF ne sait pas filtrer par fournisseur : toutes
/// portent le même <c>[DbContext]</c> et seraient toutes appliquées. Ce service remplace donc la
/// découverte d'EF par une découverte limitée à l'espace de noms (voir
/// <see cref="GlpiNgDbContext.ConfigureProvider"/>). SQL Server aussi : sa découverte standard verrait les migrations
/// des deux autres jeux, rangées dans le même assemblage.
///
/// Ajouter une migration : <c>dotnet ef migrations add X --output-dir Migrations/MySql
/// --namespace GlpiNg.Web.Migrations.MySql</c> avec <c>Setup__Provider=MySql</c> (idem Postgres).
/// </summary>
public abstract class ProviderMigrationsAssembly(string migrationsNamespace) : IMigrationsAssembly
{
    private IReadOnlyDictionary<string, TypeInfo>? _migrations;

    public Assembly Assembly => typeof(ProviderMigrationsAssembly).Assembly;

    public IReadOnlyDictionary<string, TypeInfo> Migrations => _migrations ??= Assembly.GetTypes()
        .Where(t => t.Namespace == migrationsNamespace && !t.IsAbstract && typeof(Migration).IsAssignableFrom(t))
        .Select(t => (Id: t.GetCustomAttribute<MigrationAttribute>()?.Id, Type: t.GetTypeInfo()))
        .Where(m => m.Id is not null)
        .OrderBy(m => m.Id, StringComparer.Ordinal)
        .ToDictionary(m => m.Id!, m => m.Type);

    public ModelSnapshot? ModelSnapshot => Assembly.GetTypes()
        .Where(t => t.Namespace == migrationsNamespace && typeof(ModelSnapshot).IsAssignableFrom(t))
        .Select(t => (ModelSnapshot)Activator.CreateInstance(t)!)
        .FirstOrDefault();

    public string? FindMigrationId(string nameOrId) => Migrations.Keys.FirstOrDefault(id =>
        string.Equals(id, nameOrId, StringComparison.OrdinalIgnoreCase)
        || id.Length > 15 && string.Equals(id[15..], nameOrId, StringComparison.OrdinalIgnoreCase));

    public Migration CreateMigration(TypeInfo migrationClass, string activeProvider)
    {
        Migration migration = (Migration)Activator.CreateInstance(migrationClass.AsType())!;
        migration.ActiveProvider = activeProvider;
        return migration;
    }
}

public sealed class SqlServerMigrationsAssembly() : ProviderMigrationsAssembly("GlpiNg.Web.Migrations");

public sealed class MySqlMigrationsAssembly() : ProviderMigrationsAssembly("GlpiNg.Web.Migrations.MySql");

public sealed class PostgresMigrationsAssembly() : ProviderMigrationsAssembly("GlpiNg.Web.Migrations.Postgres");
