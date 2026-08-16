using GlpiNg.Console.Db;
using GlpiNg.Console.User;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("GlpiNg.Console");
    config.SetApplicationVersion("1.0.0");

    config.AddCommand<InstallCommand>("db:install")
        .WithDescription("Initialise la base de données (schéma + configuration)")
        .WithExample("db:install")
        .WithExample("db:install", "--provider", "SqlServer", "--connection-string", "Server=.;Database=GlpiNg;Trusted_Connection=True");

    config.AddCommand<CheckCommand>("db:check")
        .WithDescription("Vérifie l'intégrité de la structure de la base de données")
        .WithExample("db:check")
        .WithExample("db:check", "--fix");

    config.AddCommand<CreateUserCommand>("user:create")
        .WithDescription("Crée un nouvel utilisateur")
        .WithExample("user:create", "jdupont")
        .WithExample("user:create", "jdupont", "--password", "MonMdp!", "--admin", "--email", "j@example.com");

    config.AddCommand<ResetPasswordCommand>("user:resetpassword")
        .WithDescription("Réinitialise le mot de passe d'un utilisateur")
        .WithExample("user:resetpassword", "admin")
        .WithExample("user:resetpassword", "admin", "--password", "MonNouveauMdp!");

    config.AddCommand<EnableUserCommand>("user:enable")
        .WithDescription("Active un utilisateur désactivé")
        .WithExample("user:enable", "jdupont");

    config.AddCommand<DisableUserCommand>("user:disable")
        .WithDescription("Désactive un utilisateur")
        .WithExample("user:disable", "jdupont");
});

return await app.RunAsync(args);
