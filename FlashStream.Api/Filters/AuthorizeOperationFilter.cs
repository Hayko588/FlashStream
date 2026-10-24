using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FlashStream.Api.Filters;

public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var allowsAnonymous = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<IAllowAnonymous>()
            .Any() ||
            context.MethodInfo.DeclaringType?
                .GetCustomAttributes(true)
                .OfType<IAllowAnonymous>()
                .Any() == true;
        if (allowsAnonymous)
        {
            return;
        }

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                }] = []
            }
        ];
    }
}
