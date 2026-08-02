using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GlpiNg.Web.Models;
using Microsoft.IdentityModel.Tokens;

namespace GlpiNg.Web.Services;

/// <summary>Nom d'émetteur/audience des jetons d'accès émis par <see cref="OAuthTokenIssuer"/> — utilisé aussi bien à l'émission qu'à la validation (voir Program.cs, AddJwtBearer).</summary>
public static class OAuthTokenConstants
{
    public const string Issuer = "GlpiNg";
    public const string Audience = "GlpiNg";
    public const string ScopeClaimType = "scope";
    public const string ClientIdClaimType = "client_id";
}

/// <summary>
/// Émet les jetons d'accès JWT du endpoint /oauth2/token (voir Controllers.OAuthController).
/// La clé de signature est bootstrappée une fois au démarrage dans appsettings.local.json (voir
/// Program.EnsureOAuthSigningKey) — un jeton émis avant un redémarrage reste donc valide après,
/// contrairement à une clé régénérée en mémoire à chaque démarrage.
/// </summary>
public class OAuthTokenIssuer(IConfiguration configuration)
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);

    private readonly SymmetricSecurityKey _signingKey = new(
        Convert.FromBase64String(configuration["Oauth:SigningKey"]
            ?? throw new InvalidOperationException("Oauth:SigningKey manquant — voir Program.EnsureOAuthSigningKey.")));

    public int AccessTokenLifetimeSeconds => (int)AccessTokenLifetime.TotalSeconds;

    /// <summary>Émet un jeton d'accès pour <paramref name="client"/>, optionnellement au nom de <paramref name="subjectUser"/> (grant "password").</summary>
    public string IssueAccessToken(OAuthClient client, GlpiUser? subjectUser, OAuthScopes grantedScopes)
    {
        List<Claim> claims = [new Claim(OAuthTokenConstants.ClientIdClaimType, client.ClientId)];
        claims.AddRange(EnumerateScopeNames(grantedScopes).Select(scope => new Claim(OAuthTokenConstants.ScopeClaimType, scope)));

        if (subjectUser is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, subjectUser.Id.ToString()));
            claims.Add(new Claim(ClaimTypes.Name, subjectUser.DisplayName ?? subjectUser.UserName));
        }

        SigningCredentials credentials = new(_signingKey, SecurityAlgorithms.HmacSha256);
        JwtSecurityToken token = new(
            issuer: OAuthTokenConstants.Issuer,
            audience: OAuthTokenConstants.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(AccessTokenLifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static IEnumerable<string> EnumerateScopeNames(OAuthScopes scopes) => Enum.GetValues<OAuthScopes>()
        .Where(scope => scope != OAuthScopes.None && scopes.HasFlag(scope))
        .Select(scope => scope.ToString().ToLowerInvariant());

    public static string ScopesToString(OAuthScopes scopes) => string.Join(' ', EnumerateScopeNames(scopes));
}
