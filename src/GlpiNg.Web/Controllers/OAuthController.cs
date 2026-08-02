using System.Net;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Controllers;

/// <summary>
/// Endpoint de jetons OAuth2 (RFC 6749) pour les <see cref="OAuthClient"/> gérés depuis
/// /oauth-clients. Seuls les grants "client_credentials" et "password" sont supportés :
/// "authorization_code" resterait sélectionnable dans le formulaire de gestion pour la fidélité
/// avec GLPI, mais nécessiterait un écran de consentement, un stockage de code d'autorisation et
/// PKCE — hors périmètre ici (voir <see cref="OAuthGrantTypes"/>).
///
/// Utilisé pour protéger /admin/import/glpi (policy "OAuthApiAccess", voir Program.cs) : un
/// client avec le grant "client_credentials" et le scope "api" ou "inventory" peut y obtenir un
/// jeton d'accès.
/// </summary>
[AllowAnonymous]
[Route("oauth2")]
public class OAuthController(GlpiNgDbContext db, UserCredentialAuthenticator credentialAuthenticator, OAuthTokenIssuer tokenIssuer) : ControllerBase
{
    private static readonly PasswordHasher<OAuthClient> SecretHasher = new();

    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Token(
        [FromForm(Name = "grant_type")] string? grantType,
        [FromForm(Name = "client_id")] string? clientId,
        [FromForm(Name = "client_secret")] string? clientSecret,
        [FromForm] string? username,
        [FromForm] string? password,
        [FromForm] string? scope,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            return BadRequest(OAuthError("invalid_client", "client_id et client_secret sont requis."));
        }

        OAuthClient? client = await db.OAuthClients.FirstOrDefaultAsync(c => c.ClientId == clientId, ct);
        if (client is null || !client.IsActive
            || SecretHasher.VerifyHashedPassword(client, client.ClientSecretHash, clientSecret) == PasswordVerificationResult.Failed)
        {
            return Unauthorized(OAuthError("invalid_client", "Client inconnu, inactif, ou secret invalide."));
        }

        if (!IsRemoteIpAllowed(client))
        {
            return Unauthorized(OAuthError("access_denied", "Adresse IP non autorisée pour ce client."));
        }

        GlpiUser? subjectUser = null;

        switch (grantType)
        {
            case "client_credentials":
                if (!client.Grants.HasFlag(OAuthGrantTypes.ClientCredentials))
                {
                    return BadRequest(OAuthError("unauthorized_client", "Le grant client_credentials n'est pas autorisé pour ce client."));
                }
                break;

            case "password":
                if (!client.Grants.HasFlag(OAuthGrantTypes.Password))
                {
                    return BadRequest(OAuthError("unauthorized_client", "Le grant password n'est pas autorisé pour ce client."));
                }
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    return BadRequest(OAuthError("invalid_request", "username et password sont requis pour ce grant."));
                }

                subjectUser = await credentialAuthenticator.AuthenticateAsync(username, password, forcedSource: null, ct);
                if (subjectUser is null)
                {
                    return Unauthorized(OAuthError("invalid_grant", "Identifiants utilisateur invalides."));
                }
                break;

            case null or "":
                return BadRequest(OAuthError("invalid_request", "grant_type est requis."));

            default:
                return BadRequest(OAuthError("unsupported_grant_type",
                    $"Grant '{grantType}' non supporté par GlpiNg (seuls client_credentials et password le sont — voir OAuthController)."));
        }

        OAuthScopes requestedScopes = ParseScopes(scope) ?? client.Scopes;
        OAuthScopes grantedScopes = requestedScopes & client.Scopes;
        if (grantedScopes == OAuthScopes.None)
        {
            return BadRequest(OAuthError("invalid_scope", "Aucun des scopes demandés n'est autorisé pour ce client."));
        }

        string accessToken = tokenIssuer.IssueAccessToken(client, subjectUser, grantedScopes);

        return Ok(new
        {
            token_type = "Bearer",
            access_token = accessToken,
            expires_in = tokenIssuer.AccessTokenLifetimeSeconds,
            scope = OAuthTokenIssuer.ScopesToString(grantedScopes),
        });
    }

    private bool IsRemoteIpAllowed(OAuthClient client)
    {
        string[] restrictions = SplitLines(client.IpRestrictionsRaw);
        if (restrictions.Length == 0)
        {
            return true;
        }

        IPAddress? remoteIp = HttpContext.Connection.RemoteIpAddress;
        if (remoteIp is null)
        {
            return false;
        }

        foreach (string restriction in restrictions)
        {
            if (IPAddress.TryParse(restriction, out IPAddress? exact) && exact.Equals(remoteIp))
            {
                return true;
            }

            if (IPNetwork.TryParse(restriction, out IPNetwork network) && network.Contains(remoteIp))
            {
                return true;
            }
        }

        return false;
    }

    private static OAuthScopes? ParseScopes(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        OAuthScopes result = OAuthScopes.None;
        foreach (string token in scope.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Enum.TryParse(token, ignoreCase: true, out OAuthScopes parsed))
            {
                result |= parsed;
            }
        }

        return result;
    }

    private static string[] SplitLines(string? raw) =>
        raw?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

    private static object OAuthError(string error, string description) => new { error, error_description = description };
}
