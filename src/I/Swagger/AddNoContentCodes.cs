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
using System.Net;

using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace PPWCode.AspNetCore.Host.I.Swagger
{
    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public class AddNoContentCodes : PpwOperationFilter
    {
        /// <inheritdoc />
        public override void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (IsDelete(context))
            {
                OpenApiResponse response =
                    new()
                    {
                        Description = $"A {GetControllerName(context)} identified by ({GetIdentifications(context.ApiDescription)}), is found and deleted."
                    };
                ForceAddResponse(operation, $"{HttpStatusCode.NoContent:D}", response);
                RemoveResponse(operation, $"{HttpStatusCode.OK:D}");
            }
        }
    }
}
