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
using System.Net;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Logging;

using NHibernate;

using PPWCode.AspNetCore.Host.I.Transactional;
using PPWCode.AspNetCore.Server.I.Transactional;
using PPWCode.Vernacular.Exceptions.V;
using PPWCode.Vernacular.NHibernate.IV;

using ISession = NHibernate.ISession;

namespace PPWCode.AspNetCore.Host.I.NHibernate;

/// <summary>
///     The <see cref="TransactionMiddleware" /> handles transactions.
/// </summary>
/// <remarks>
///     <p>
///         The transaction is created, taking into account the <see cref="TransactionalAttribute" /> that might be placed
///         on the controller or on the action method.
///     </p>
///     <p>
///         The transaction is created when the request passes through the middleware, and before the next middleware in
///         the pipeline is called.
///     </p>
///     <p>
///         The transaction is closed either when ASP.NET Core attempts to start writing the response to the client,
///         or when the response comes back through this middleware; whichever happens earlier.
///     </p>
/// </remarks>
public class TransactionMiddleware : IMiddleware
{
    private readonly ISessionProviderAsync _sessionProvider;
    private readonly ILogger<TransactionMiddleware> _logger;

    public TransactionMiddleware(
        ISessionProviderAsync sessionProvider,
        ILogger<TransactionMiddleware> logger)
    {
        _sessionProvider = sessionProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext httpContext, RequestDelegate next)
    {
        Endpoint? endPoint = httpContext.GetEndpoint();
        if (endPoint == null)
        {
            // It is possible that no endpoint is found when the backend is presented with a path that is not supported
            // by any controller.  When no endpoint is found, the response will likely be NotFound or another 4xx status
            // and in that case no transaction handling is done.
            await next(httpContext).ConfigureAwait(false);
            return;
        }

        ControllerActionDescriptor? controllerActionDescriptor = endPoint.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (controllerActionDescriptor == null)
        {
            // It is possible that an endpoint is found, but that no ControllerActionDescriptor is found.  This could be
            // the case for a path that is supported for a number of HTTP verbs, but is called with another HTTP verb.
            // This is likely an internal endpoint added by ASP.NET Core.  When this is the case, the response will
            // likely be a 4xx status and in that case no transaction handling is done.
            await next(httpContext).ConfigureAwait(false);
            return;
        }

        TransactionalAttribute? transactionalAttribute = endPoint.Metadata.GetMetadata<TransactionalAttribute>();
        ITransaction? transaction = InitiateTransaction(controllerActionDescriptor, transactionalAttribute);
        if (transaction == null)
        {
            await next(httpContext).ConfigureAwait(false);
            return;
        }

        httpContext.Response.OnStarting(() => CloseTransactionAsync(httpContext, transaction));
        try
        {
            await next(httpContext).ConfigureAwait(false);
        }
        finally
        {
            await CloseTransactionAsync(httpContext, transaction).ConfigureAwait(false);
        }
    }

    protected virtual ITransaction? InitiateTransaction(
        ControllerActionDescriptor controllerActionDescriptor,
        TransactionalAttribute? transactionalAttribute)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation($"Determine if we should use transactions using attribute {nameof(TransactionalAttribute)}");
        }

        string? displayName = controllerActionDescriptor.DisplayName;
        IsolationLevel isolationLevel = transactionalAttribute?.IsolationLevel ?? IsolationLevel.Unspecified;

        if (transactionalAttribute is { Transactional: true })
        {
            ISession session = _sessionProvider.Session;
            if (!session.IsOpen)
            {
                throw new ProgrammingError($"{displayName} Current session is not opened.");
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("{DisplayName} Start our request transaction, with isolation level {IsolationLevel}", displayName, isolationLevel);
            }

            return session.BeginTransaction(transactionalAttribute.IsolationLevel);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("{DisplayName} No transaction is requested", displayName);
        }

        return null;
    }

    protected virtual async Task CloseTransactionAsync(
        HttpContext httpContext,
        ITransaction transaction)
    {
        if (transaction.IsActive)
        {
            CancellationToken cancellationToken = httpContext.RequestAborted;
            bool shouldRollback =
                cancellationToken.IsCancellationRequested
                || !IsSuccessStatusCode(httpContext)
                || httpContext.Request.Headers.ContainsKey(Constants.RequestSimulation);
            if (shouldRollback)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Rollback due to cancellation request");
                    }
                    else if (!IsSuccessStatusCode(httpContext))
                    {
                        _logger.LogInformation("Rollback due to Http status code {HttpStatusCode}", httpContext.Response.StatusCode);
                    }
                    else if (httpContext.Request.Headers.ContainsKey(Constants.RequestSimulation))
                    {
                        _logger.LogInformation("Rollback due to Header {HttpHeader}", Constants.RequestSimulation);
                    }
                }

                // A rollback shouldn't be canceled!
                cancellationToken = CancellationToken.None;
                await OnRollbackAsync(httpContext, cancellationToken).ConfigureAwait(false);
                await _sessionProvider
                    .SafeEnvironmentProviderAsync
                    .RunAsync(
                        nameof(ITransaction.RollbackAsync),
                        transaction.RollbackAsync,
                        cancellationToken)
                    .ConfigureAwait(false);
                await OnAfterRollbackAsync(httpContext, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await OnCommitAsync(httpContext, cancellationToken).ConfigureAwait(false);
                await _sessionProvider
                    .SafeEnvironmentProviderAsync
                    .RunAsync(
                        nameof(ITransaction.CommitAsync),
                        transaction.CommitAsync,
                        cancellationToken)
                    .ConfigureAwait(false);
                await OnAfterCommitAsync(httpContext, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    protected virtual bool IsSuccessStatusCode(HttpContext httpContext)
    {
        int statusCode = httpContext.Response.StatusCode;
        return statusCode is >= (int)HttpStatusCode.OK and <= 299;
    }

    protected virtual Task OnCommitAsync([NotNull] HttpContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;

    protected virtual Task OnAfterCommitAsync([NotNull] HttpContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;

    protected virtual Task OnRollbackAsync([NotNull] HttpContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;

    protected virtual Task OnAfterRollbackAsync([NotNull] HttpContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
