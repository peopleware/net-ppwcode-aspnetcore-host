using Microsoft.OpenApi;

using PPWCode.Vernacular.Contracts.I;
using PPWCode.Vernacular.Exceptions.V;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace PPWCode.AspNetCore.Host.I.Swagger;

public class ForceTypeToStringSchemaFilter<T> : ISchemaFilter
    where T : class
{
    /// <inheritdoc />
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        OpenApiSchema? openApiSchema =
            schema switch
            {
                OpenApiSchema openApiSchemaTemp => openApiSchemaTemp,
                OpenApiSchemaReference openApiSchemaReference => openApiSchemaReference.RecursiveTarget,
                _ => throw new ProgrammingError("Unsupported configuration")
            };

        if (openApiSchema is not null && (context.Type == typeof(T)))
        {
            // Override schema to be a string
            openApiSchema.Type = JsonSchemaType.String;

            // Remove properties (e.g., "value")
            Contract.Assert(openApiSchema.Properties != null);
            openApiSchema.Properties.Clear();
        }
    }
}
