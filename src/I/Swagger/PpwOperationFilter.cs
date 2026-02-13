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

using Asp.Versioning;

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;

using PPWCode.Vernacular.Persistence.V;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace PPWCode.AspNetCore.Host.I.Swagger
{
    /// <inheritdoc />
    public abstract class PpwOperationFilter : IOperationFilter
    {
        public abstract void Apply(OpenApiOperation operation, OperationFilterContext context);

        protected virtual bool IsGet(OperationFilterContext context)
            => string.Equals(context.ApiDescription.HttpMethod, HttpMethod.Get.ToString(), StringComparison.OrdinalIgnoreCase);

        protected virtual bool IsPost(OperationFilterContext context)
            => string.Equals(context.ApiDescription.HttpMethod, HttpMethod.Post.ToString(), StringComparison.OrdinalIgnoreCase);

        protected virtual bool IsPut(OperationFilterContext context)
            => string.Equals(context.ApiDescription.HttpMethod, HttpMethod.Put.ToString(), StringComparison.OrdinalIgnoreCase);

        protected virtual bool IsDelete(OperationFilterContext context)
            => string.Equals(context.ApiDescription.HttpMethod, HttpMethod.Delete.ToString(), StringComparison.OrdinalIgnoreCase);

        protected virtual ControllerActionDescriptor? ControllerActionDescriptor(OperationFilterContext context)
            => context.ApiDescription.ActionDescriptor as ControllerActionDescriptor;

        protected virtual string? GetControllerName(OperationFilterContext context)
            => ControllerActionDescriptor(context)?.ControllerName;

        protected virtual IEnumerable<ApiParameterDescription> GetParameters(ApiDescription apiDescription, Func<ApiDescription, ApiParameterDescription, bool>? func = null)
            => apiDescription
                .ParameterDescriptions
                .Where(pd => (func == null) || func(apiDescription, pd));

        protected virtual string GetIdentification(ApiParameterDescription description)
        {
            string paramName = description.Name;
            Type paramType = description.ParameterDescriptor.ParameterType;
            if (typeof(IPersistentObject<>).IsAssignableFrom(paramType))
            {
                paramName = string.Concat(paramName, ".Id");
            }

            return paramName;
        }

        protected virtual string GetIdentifications(ApiDescription apiDescription, Func<ApiDescription, ApiParameterDescription, bool>? func = null)
            => string.Join(
                ", ",
                GetParameters(apiDescription, func)
                    .Where(p => p.ParameterDescriptor.ParameterType != typeof(ApiVersion))
                    .Select(GetIdentification));

        protected virtual bool ResponseExists(OpenApiOperation operation, string key)
            => operation.Responses.ContainsKey(key);

        protected virtual void AddResponse(OpenApiOperation operation, string key, OpenApiResponse response)
        {
            operation.Responses.Add(key, response);
        }

        protected virtual void RemoveResponse(OpenApiOperation operation, string key)
        {
            if (ResponseExists(operation, key))
            {
                operation.Responses.Remove(key);
            }
        }

        protected virtual void AddResponseIfNotExists(OpenApiOperation operation, string key, OpenApiResponse response)
        {
            if (!ResponseExists(operation, key))
            {
                AddResponse(operation, key, response);
            }
        }

        protected virtual void ForceAddResponse(OpenApiOperation operation, string key, OpenApiResponse response)
        {
            if (ResponseExists(operation, key))
            {
                RemoveResponse(operation, key);
            }

            AddResponse(operation, key, response);
        }

        protected virtual void ConditionalAddResponse(OpenApiOperation operation, string key, OpenApiResponse response)
        {
            if (!ResponseExists(operation, key))
            {
                AddResponse(operation, key, response);
            }
        }
    }
}
