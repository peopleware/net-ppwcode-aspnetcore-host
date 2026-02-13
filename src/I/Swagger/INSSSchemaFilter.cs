using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace PPWCode.AspNetCore.Host.I.Swagger;

public class INSSSchemaFilter<T> : ISchemaFilter
    where T : class
{
    /// <inheritdoc />
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(T))
        {
            // Override schema to be a string
            schema.Type = "string";

            // Remove properties (e.g., "value")
            schema.Properties.Clear();
        }
    }
}
