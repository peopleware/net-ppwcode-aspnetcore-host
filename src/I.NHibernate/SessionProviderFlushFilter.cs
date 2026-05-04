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

using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

using NHibernate;

using PPWCode.AspNetCore.Server.I.Transactional;
using PPWCode.Vernacular.Exceptions.V;
using PPWCode.Vernacular.NHibernate.IV;

namespace PPWCode.AspNetCore.Host.I.NHibernate;

/// <summary>
///     The <see cref="SessionProviderFlushFilter" /> is an <see cref="IAsyncActionFilter" /> that executes a <c>Flush</c>
///     on the NHibernate <see cref="ISession" /> right after the action method is executed.
/// </summary>
/// <remarks>
///     <p>
///         Note that this action filter is best placed as the first action filter: no database actions should be performed
///         after the execution (in the 'after' part) of this action filter.
///     </p>
///     <p>
///         Note that the <c>Flush</c> is only executed if the request is not cancelled and did not generate an exception.
///     </p>
/// </remarks>
public class SessionProviderFlushFilter
    : IAsyncActionFilter
{
    private readonly ISessionProviderAsync _sessionProvider;
    private readonly ILogger<SessionProviderFlushFilter> _logger;

    public SessionProviderFlushFilter(
        ISessionProviderAsync sessionProvider,
        ILogger<SessionProviderFlushFilter> logger)
    {
        _sessionProvider = sessionProvider;
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
            string displayName = ActionContextDisplayName(context);

            if (!_sessionProvider.Session.IsOpen)
            {
                throw new ProgrammingError($"{displayName} Current session is not opened.");
            }

            ITransaction? transaction = _sessionProvider.Session.GetCurrentTransaction();
            if (transaction is null or { IsActive: false })
            {
                throw new ProgrammingError($"{displayName} Expected an active transaction on the session.");
            }

            ActionExecutedContext executedContext = await next().ConfigureAwait(false);

            // We will only flush our pending action if and only if:
            // * No cancellation was requested
            // * no exception was thrown by the endpoint
            CancellationToken cancellationToken = executedContext.HttpContext.RequestAborted;
            if (!cancellationToken.IsCancellationRequested
                && (executedContext.Exception == null))
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "{DisplayName} Flush request to the database",
                        displayName);
                }

                await _sessionProvider
                    .FlushAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (_logger.IsEnabled(LogLevel.Information))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation(
                        "{DisplayName} Not flushing the request since cancellation is requested",
                        displayName);
                }
                else if (executedContext.Exception != null)
                {
                    _logger.LogInformation(
                        "{DisplayName} Not flushing the request since an exception was thrown, {ExecptionMessage}",
                        displayName,
                        executedContext.Exception.Message);
                }
            }
        }
    }

    protected virtual string ActionContextDisplayName(FilterContext context)
        => context.ActionDescriptor.DisplayName ?? "Unknown action display name";
}
