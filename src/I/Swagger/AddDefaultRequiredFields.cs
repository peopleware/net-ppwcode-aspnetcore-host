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

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

using Asp.Versioning;

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;

using PPWCode.AspNetCore.Host.I.Bootstrap;
using PPWCode.Vernacular.Contracts.I;
using PPWCode.Vernacular.Exceptions.V;

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
                        (pd => $"{pd.ParameterDescriptor.Name}", JsonValue.Create($"{Startup.DefaultApiVersion.ToString(Startup.ApiVersionFormat)}"))
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

                foreach ((Func<ApiParameterDescription, string>, JsonNode) tuple in defaultRequiredField.ParamLambdas)
                {
                    SetParamValue(operation, tuple.Item1.Invoke(parameter), tuple.Item2);
                }
            }
        }

        protected void SetParamValue(OpenApiOperation operation, string name, JsonNode defaultValue)
        {
            Contract.Requires(operation.Parameters is not null);
            IOpenApiParameter? parameter =
                operation
                    .Parameters
                    .SingleOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

            if (parameter is not null)
            {
                OpenApiParameter? openApiParameter =
                    parameter switch
                    {
                        OpenApiParameter openApiParameterTemp => openApiParameterTemp,
                        OpenApiParameterReference openApiParameterReference => openApiParameterReference.RecursiveTarget,
                        _ => throw new ProgrammingError("Unsupported configuration")
                    };

                if (openApiParameter is not null && openApiParameter.Required)
                {
                    openApiParameter.Example = defaultValue;
                }
            }
        }

        public class DefaultRequiredField
        {
            public DefaultRequiredField(Type type, (Func<ApiParameterDescription, string>, JsonNode)[] paramLambdas)
            {
                Type = type;
                ParamLambdas = paramLambdas;
            }

            public Type Type { get; }

            public (Func<ApiParameterDescription, string>, JsonNode)[] ParamLambdas { get; }
        }
    }
}
