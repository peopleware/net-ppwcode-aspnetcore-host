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

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

using Asp.Versioning;

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

using PPWCode.AspNetCore.Host.I.Bootstrap;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace PPWCode.AspNetCore.Host.I.Swagger
{
    [ExcludeFromCodeCoverage]
    public class AddDefaultRequiredFields : PpwOperationFilter
    {
        private static readonly IList<DefaultRequiredField> _requiredFieldsWithDefaultValues =
            new List<DefaultRequiredField>();

        static AddDefaultRequiredFields()
        {
            DefaultRequiredField defaultRequiredField =
                new(
                    typeof(ApiVersion),
                    [
                        (pd => $"{pd.ParameterDescriptor.Name}", new OpenApiString($"{Startup.DefaultApiVersion.ToString(Startup.ApiVersionFormat)}"))
                    ]);
            _requiredFieldsWithDefaultValues.Add(defaultRequiredField);
        }

        /// <inheritdoc />
        public override void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation.Parameters == null)
            {
                return;
            }

            ReadOnlyCollection<ApiParameterDescription> parameters =
                GetParameters(context.ApiDescription)
                    .ToList()
                    .AsReadOnly();
            foreach (DefaultRequiredField defaultRequiredField in _requiredFieldsWithDefaultValues)
            {
                ApiParameterDescription? parameter =
                    parameters
                        .SingleOrDefault(p => defaultRequiredField.Type.IsAssignableFrom(p.ParameterDescriptor.ParameterType));
                if (parameter == null)
                {
                    continue;
                }

                foreach ((Func<ApiParameterDescription, string>, IOpenApiAny) tuple in defaultRequiredField.ParamLambdas)
                {
                    SetParamValue(operation, tuple.Item1.Invoke(parameter), tuple.Item2);
                }
            }
        }

        protected void SetParamValue(OpenApiOperation operation, string name, IOpenApiAny defaultValue)
        {
            OpenApiParameter? param =
                operation
                    .Parameters
                    .SingleOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (param is { Required: true })
            {
                param.Example = defaultValue;
            }
        }

        public class DefaultRequiredField
        {
            public DefaultRequiredField(Type type, (Func<ApiParameterDescription, string>, IOpenApiAny)[] paramLambdas)
            {
                Type = type;
                ParamLambdas = paramLambdas;
            }

            public Type Type { get; }

            public (Func<ApiParameterDescription, string>, IOpenApiAny)[] ParamLambdas { get; }
        }
    }
}
