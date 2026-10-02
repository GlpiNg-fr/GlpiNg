using Spectre.Console.Cli;

namespace GlpiNg.Web.Cli;

/// <summary>Commandes de <c>glping</c> ; sans argument, Spectre affiche l'aide.</summary>
public static class GlpiNgCli
{
    public static int Run(string[] args)
    {
        // Accents des descriptions : la console Windows n'est pas en UTF-8 par défaut.
        System.Console.OutputEncoding = System.Text.Encoding.UTF8;

        var app = new CommandApp();

        app.Configure(config =>
        {
            config.SetApplicationName("glping");
            config.SetApplicationVersion(typeof(GlpiNgCli).Assembly.GetName().Version?.ToString(3) ?? "?");

            config.AddCommand<ServeCommand>("serve")
                .WithDescription("Lance le serveur web (options ASP.NET Core acceptées, ex. --urls)")
                .WithExample("serve")
                .WithExample("serve", "--urls", "http://0.0.0.0:5000");

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

        return app.Run(args);
    }
}

/// <summary>
/// Présente seulement pour l'aide : <c>glping serve ...</c> est lancé par <see cref="Program.Main"/>
/// sans passer par Spectre, qui rangerait <c>--urls</c>, <c>--environment</c>... dans
/// <c>Remaining.Parsed</c> au lieu de les laisser intacts pour ASP.NET Core.
/// </summary>
public sealed class ServeCommand : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        Program.RunServer([.. context.Remaining.Raw]);
        return 0;
    }
}
