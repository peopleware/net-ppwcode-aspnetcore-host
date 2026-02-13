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

using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;

using PPWCode.AspNetCore.Server.I.Transactional;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace PPWCode.AspNetCore.Host.I.Swagger
{
    /// <inheritdoc />
    /// <summary>
    ///     Operation filter to add the transaction information for the endpoint.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class AddTransactionalInformation : PpwOperationFilter
    {
        /// <inheritdoc />
        public override void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            ControllerActionDescriptor? controllerActionDescriptor = ControllerActionDescriptor(context);
            if (controllerActionDescriptor != null)
            {
                TransactionalAttribute? attribute =
                    controllerActionDescriptor
                        .MethodInfo
                        .GetCustomAttributes(typeof(TransactionalAttribute), true)
                        .OfType<TransactionalAttribute>()
                        .SingleOrDefault()
                    ?? controllerActionDescriptor
                        .ControllerTypeInfo
                        .GetCustomAttributes(typeof(TransactionalAttribute), true)
                        .OfType<TransactionalAttribute>()
                        .SingleOrDefault();
                bool transactional = attribute?.Transactional ?? true;
                IsolationLevel isolationLevel = attribute?.IsolationLevel ?? IsolationLevel.Unspecified;
                StringBuilder sb = new StringBuilder();
                if (transactional)
                {
                    sb
                        .Append("<b>Transactional</b>: Yes<br>")
                        .AppendFormat("<b>Isolation level</b>: {0}<br>", isolationLevel);
                }
                else
                {
                    sb
                        .Append("<b>Transactional</b>: No<br>");
                }

                if (operation.Description != null)
                {
                    sb
                        .Append("<br>")
                        .Append(operation.Description);
                }

                operation.Description = sb.ToString();
            }
        }
    }
}
