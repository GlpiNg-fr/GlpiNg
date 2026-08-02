using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace GlpiNg.Web.Options;

/// <summary>
/// Ajoute l'exigence de sécurité "OAuth2" (voir Program.cs, AddSecurityDefinition) uniquement
/// aux actions marquées <c>[Authorize(AuthenticationSchemes = "Bearer")]</c> (aujourd'hui
/// GlpiImportController) — pour que le document Swagger reflète fidèlement que seuls certains
/// endpoints exigent un jeton OAuth, plutôt que de l'exiger partout.
/// </summary>
public class OAuthSecurityRequirementFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        IEnumerable<AuthorizeAttribute> authorizeAttributes = context.MethodInfo
            .GetCustomAttributes(true)
            .Concat(context.MethodInfo.DeclaringType?.GetCustomAttributes(true) ?? [])
            .OfType<AuthorizeAttribute>();

        if (!authorizeAttributes.Any(a => a.AuthenticationSchemes?.Contains("Bearer") == true))
        {
            return;
        }

        OpenApiSecurityScheme scheme = new() { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "OAuth2" } };
        operation.Security = [new OpenApiSecurityRequirement { [scheme] = ["api", "inventory"] }];
    }
}
