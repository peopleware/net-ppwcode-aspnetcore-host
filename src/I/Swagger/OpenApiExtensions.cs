// Copyright 2026 by PeopleWare n.v..
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Microsoft.OpenApi;

using PPWCode.Vernacular.Exceptions.V;

namespace PPWCode.AspNetCore.Host.I.Swagger;

public static class OpenApiExtensions
{
    extension(IOpenApiSchema schema)
    {
        /// <summary>
        ///     Retrieve the concrete <see cref="OpenApiSchema"/> that this instance of <see cref="IOpenApiSchema"/>
        ///     refers to.
        /// </summary>
        /// <returns>A concrete instance of <see cref="OpenApiSchema"/> if one is found</returns>
        /// <exception cref="ProgrammingError">
        ///     Error is thrown when the given <paramref name="schema"/> is not an instance of the supported types
        ///     <see cref="OpenApiSchema"/> and <see cref="OpenApiSchemaReference"/>.
        /// </exception>
        public OpenApiSchema? GetOpenApiSchema()
            => schema switch
            {
                OpenApiSchema openApiSchemaTemp => openApiSchemaTemp,
                OpenApiSchemaReference openApiSchemaReference => openApiSchemaReference.RecursiveTarget,
                _ => throw new ProgrammingError($"Unsupported type of {nameof(IOpenApiSchema)}: {schema.GetType()}")
            };
    }

    extension(IOpenApiParameter parameter)
    {
        /// <summary>
        ///     Retrieve the concrete <see cref="OpenApiParameter"/> that this instance of <see cref="IOpenApiParameter"/>
        ///     refers to.
        /// </summary>
        /// <returns>A concrete instance of <see cref="OpenApiParameter"/> if one is found</returns>
        /// <exception cref="ProgrammingError">
        ///     Error is thrown when the given <paramref name="parameter"/> is not an instance of the supported types
        ///     <see cref="OpenApiParameter"/> and <see cref="OpenApiParameterReference"/>.
        /// </exception>
        public OpenApiParameter? GetOpenApiParameter()
            => parameter switch
            {
                OpenApiParameter openApiParameterTemp => openApiParameterTemp,
                OpenApiParameterReference openApiParameterReference => openApiParameterReference.RecursiveTarget,
                _ => throw new ProgrammingError($"Unsupported type of {nameof(IOpenApiParameter)}: {parameter.GetType()}")
            };
    }
}
