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

using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

using Microsoft.OpenApi;

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
            TransactionalAttribute? attribute = GetTransactionalAttribute(context.MethodInfo);
            TransactionTypeEnum transactionalType = attribute?.TransactionalType ?? TransactionTypeEnum.NONE;
            IsolationLevel isolationLevel = attribute?.IsolationLevel ?? IsolationLevel.Unspecified;
            StringBuilder sb = new();
            if (transactionalType is TransactionTypeEnum.YES or TransactionTypeEnum.MANUAL)
            {
                sb
                    .AppendFormat("<b>Transactional</b>: Yes{0}<br>", transactionalType == TransactionTypeEnum.MANUAL ? " (manual)" : string.Empty)
                    .AppendFormat("<b>Isolation level</b>: {0}<br>", isolationLevel);
                if (transactionalType is TransactionTypeEnum.MANUAL
                    && !string.IsNullOrWhiteSpace(attribute?.ManualReason))
                {
                    sb.AppendFormat("<b>Manual reason</b>: {0}<br>", attribute.ManualReason);
                }
            }
            else if (transactionalType is TransactionTypeEnum.NO)
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

        private static TransactionalAttribute? GetTransactionalAttribute(MethodInfo methodInfo)
            => methodInfo.GetCustomAttribute<TransactionalAttribute>(true)
               ?? methodInfo.DeclaringType?.GetCustomAttribute<TransactionalAttribute>(true);
    }
}
