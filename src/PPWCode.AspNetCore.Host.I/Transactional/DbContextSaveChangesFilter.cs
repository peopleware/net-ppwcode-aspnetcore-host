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
using System.Data.Common;

using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using PPWCode.AspNetCore.Server.I.Transactional;
using PPWCode.Vernacular.Exceptions.V;

namespace PPWCode.AspNetCore.Host.I.Transactional;

/// <summary>
///     The <see cref="DbContextSaveChangesFilter" /> is an <see cref="IAsyncActionFilter" /> that executes a
///     <c>SaveChangesAsync</c>
///     on theEntity Framework context <see cref="DbContext" /> right after the action method is executed.
/// </summary>
/// <remarks>
///     <p>
///         Note that this action filter is best placed as the first action filter: no database actions should be performed
///         after the execution (in the 'after' part) of this action filter.
///     </p>
///     <p>
///         Note that the <c>SaveChangesAsync</c> is only executed if the request is not canceled and did not generate an
///         exception.
///     </p>
/// </remarks>
public class DbContextSaveChangesFilter
    : IAsyncActionFilter
{
    private readonly DbContext _dbContext;
    private readonly ILogger<DbContextSaveChangesFilter> _logger;

    public DbContextSaveChangesFilter(DbContext dbContext, ILogger<DbContextSaveChangesFilter> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        TransactionalAttribute? transactional =
            context
                .ActionDescriptor
                .EndpointMetadata
                .OfType<TransactionalAttribute>()
                .LastOrDefault();
        if (transactional?.Transactional == false)
        {
            await next().ConfigureAwait(false);
        }
        else
        {
            DbConnection connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                throw new ProgrammingError($"{ActionContextDisplayName(context)} No open connection found on the dbContext, state is {connection.State}.");
            }

            IDbContextTransaction? transaction = _dbContext.Database.CurrentTransaction;
            if (transaction == null)
            {
                throw new ProgrammingError($"{ActionContextDisplayName(context)} Expected an active transaction on the dbContext.");
            }

            ActionExecutedContext executedContext = await next().ConfigureAwait(false);

            // We will only flush our pending action if and only if:
            // * No cancellation was requested
            // * no exception was thrown by the endpoint
            CancellationToken cancellationToken = executedContext.HttpContext.RequestAborted;
            if (!cancellationToken.IsCancellationRequested
                && (executedContext.Exception == null))
            {
                _logger.LogInformation("Saving changes to the database");
                await _dbContext
                    .SaveChangesAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Saving changes to the database was requested, but either the request is cancelled {IsCancellationRequested} or an exception, {ExceptionMessage}, occured",
                    cancellationToken.IsCancellationRequested,
                    executedContext.Exception?.Message);
            }
        }
    }

    protected virtual string ActionContextDisplayName(FilterContext context)
        => context.ActionDescriptor.DisplayName ?? "Unknown action display name";
}
