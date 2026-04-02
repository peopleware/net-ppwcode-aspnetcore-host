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

using System.Diagnostics.CodeAnalysis;
using System.Net;

using Asp.Versioning;

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace PPWCode.AspNetCore.Host.I.Swagger
{
    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public class AddNotFoundResponseCodes : PpwOperationFilter
    {
        /// <inheritdoc />
        public override void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            ApiDescription apiDescription = context.ApiDescription;

            bool ParameterSelector(ApiDescription description, ApiParameterDescription parameterDescription)
                => (parameterDescription.Source == BindingSource.Path)
                   && (parameterDescription.ParameterDescriptor.ParameterType != typeof(ApiVersion));

            if (GetParameters(apiDescription, ParameterSelector).Any())
            {
                string identifications = GetIdentifications(apiDescription, ParameterSelector);
                ApiResponseType? responseType =
                    apiDescription
                        .SupportedResponseTypes
                        .FirstOrDefault(srt => srt.Type != typeof(void));
                string? responseTypeName = responseType?.Type?.Name ?? GetControllerName(context);
                OpenApiResponse response = new() { Description = $"A(n) {responseTypeName} identified by ({identifications}), is not found." };
                ConditionalAddResponse(operation, $"{HttpStatusCode.NotFound:D}", response);
            }
        }
    }
}
