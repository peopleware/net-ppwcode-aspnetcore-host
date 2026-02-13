// Copyright 2025 by PeopleWare n.v..
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Diagnostics.CodeAnalysis;

using Microsoft.OpenApi.Models;

using PPWCode.AspNetCore.Host.I.Transactional;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace PPWCode.AspNetCore.Host.I.Swagger
{
    /// <inheritdoc />
    /// <summary>
    ///     Operation filter to add the custom header: <see cref="TransactionMiddleware.RequestSimulation" />.
    ///     If this header is added, the request executed completely, but the persistent store will not be updated.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class AddRequestSimulationHeader : PpwOperationFilter
    {
        /// <inheritdoc />
        public override void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (IsPost(context) || IsPut(context) || IsDelete(context))
            {
                operation.Parameters ??= new List<OpenApiParameter>();
                operation.Parameters.Add(
                    new OpenApiParameter
                    {
                        Name = TransactionMiddleware.RequestSimulation,
                        Description = "Simulate Request?",
                        In = ParameterLocation.Header,
                        Schema =
                            new OpenApiSchema
                            {
                                Type = "string"
                            },
                        Required = false,
                    });
            }
        }
    }
}
