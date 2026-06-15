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
using System.Net;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using PPWCode.AspNetCore.Host.I.Transactional;
using PPWCode.AspNetCore.Server.I.Transactional;
using PPWCode.Vernacular.Exceptions.V;

namespace PPWCode.AspNetCore.Host.I.EntityFrameworkCore;

/// <summary>
///     The <see cref="TransactionMiddleware" /> handles transactions.
/// </summary>
/// <remarks>
///     <p>
///         The transaction is created, taking into account the <see cref="TransactionalAttribute" /> that might be applied
///         to the controller or the action method.
///     </p>
///     <p>
///         The transaction is created when the request passes through the middleware, before the next middleware in the
///         pipeline is called.
///     </p>
///     <p>
///         The transaction is closed either when ASP.NET Core attempts to start writing the response to the client or
///         when the response comes back through this middleware, whichever happens first.
///     </p>
///     <p>
///         This middleware instance keeps state. It needs to know whether a transaction has already been started. So, it
///         should be registered as scoped or transient in your DI container.
///     </p>
/// </remarks>
public class TransactionMiddleware : IMiddleware
{
    private readonly DbContext _dbContext;
    private readonly ILogger<TransactionMiddleware> _logger;

    private volatile int _isTransactionClosed;

    public TransactionMiddleware(DbContext dbContext, ILogger<TransactionMiddleware> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext httpContext, RequestDelegate next)
    {
        Endpoint? endPoint = httpContext.GetEndpoint();
        if (endPoint == null)
        {
            // It is possible that no endpoint is found when the backend receives a path not supported
            // by any controller.  When no endpoint is found, the response will likely be NotFound or another 4xx status,
            // and in that case no transaction handling is done.
            await next(httpContext).ConfigureAwait(false);
            return;
        }

        ControllerActionDescriptor? controllerActionDescriptor = endPoint.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (controllerActionDescriptor == null)
        {
            // It is possible that an endpoint is found, but that no ControllerActionDescriptor is found.  This could be
            // the case for a path that supports a number of HTTP verbs but is called with a different HTTP verb.
            // This is likely an internal endpoint added by ASP.NET Core.  When this is the case, the response will
            // likely be a 4xx status, and in that case no transaction handling is done.
            await next(httpContext).ConfigureAwait(false);
            return;
        }

        TransactionalAttribute? transactionalAttribute = endPoint.Metadata.GetMetadata<TransactionalAttribute>();
        IDbContextTransaction? transaction = InitiateTransaction(controllerActionDescriptor, transactionalAttribute);
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

    protected virtual IDbContextTransaction? InitiateTransaction(
        ControllerActionDescriptor controllerActionDescriptor,
        TransactionalAttribute? transactionalAttribute)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation($"Determine whether transactions should be used based on attribute {nameof(TransactionalAttribute)}");
        }

        string? displayName = controllerActionDescriptor.DisplayName;
        IsolationLevel isolationLevel = transactionalAttribute?.IsolationLevel ?? IsolationLevel.Unspecified;

        if (transactionalAttribute is { Transactional: true })
        {
            IDbContextTransaction? transaction = _dbContext.Database.CurrentTransaction;
            if (transaction != null)
            {
                throw new ProgrammingError($"{displayName} Expected no transaction on the dbContext.");
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "{ActionContext} Starting request transaction with isolation level {IsolationLevel}",
                    displayName,
                    isolationLevel);
            }

            transaction = _dbContext.Database.BeginTransaction(transactionalAttribute.IsolationLevel);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Created transaction {TransactionHashCode}", transaction.GetHashCode());
            }

            return transaction;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("{DisplayName} No transaction is requested", displayName);
        }

        return null;
    }

    protected virtual async Task CloseTransactionAsync(HttpContext httpContext, IDbContextTransaction transaction)
    {
        if (_isTransactionClosed > 0)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Transaction {TransactionHashCode} is already closed; no further action performed", transaction.GetHashCode());
            }

            return;
        }

        // Mark the transaction as handled before processing it. This prevents the execution of recursive calls.
        Interlocked.Add(ref _isTransactionClosed, 1);

        IDbContextTransaction? currentTransaction = _dbContext.Database.CurrentTransaction;
        if (currentTransaction != null)
        {
            CancellationToken cancellationToken = httpContext.RequestAborted;
            bool shouldRollback =
                cancellationToken.IsCancellationRequested
                || !IsSuccessStatusCode(httpContext)
                || (currentTransaction != transaction)
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
                    else if (currentTransaction != transaction)
                    {
                        _logger.LogInformation("Mismatch transactions, the current transaction on the db-context doesn't match the initial started transaction");
                    }
                }

                // A rollback should not be canceled.
                await HandleRollbackAsync().ConfigureAwait(false);
            }
            else
            {
                // Commit was chosen; do not cancel once it has started.
                await HandleCommitAsync().ConfigureAwait(false);
            }
        }
        else
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("No current transaction on the db-context");
            }
        }
    }

    /// <summary>
    ///     This method makes a best-effort attempt to roll back the given transaction.
    /// </summary>
    /// <remarks>
    ///     The method performs the rollback on a best-effort basis: when something goes wrong, the exception is properly
    ///     logged, but the exception itself is swallowed. The code determines that a rollback must be initiated, and
    ///     the further flow and handling act as if the rollback was successfully executed. Whenever a
    ///     rollback is initiated, there is a guarantee that the commit was not executed.
    /// </remarks>
    /// <returns>
    ///     A <see cref="Task" /> representing the asynchronous action.
    /// </returns>
    protected virtual async Task HandleRollbackAsync()
    {
        try
        {
            // Execute rollback.
            await _dbContext.Database.RollbackTransactionAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            // Log, but swallow the exception.
            _logger.LogError(e, "Actual rollback failed with exception: exception is logged but swallowed");
        }
    }

    /// <summary>
    ///     This method handles the commit of the given transaction. Note that if the commit call fails, the transaction
    ///     is rolled back on a best-effort basis. The exception thrown by the commit failure is rethrown further up
    ///     the stack.
    /// </summary>
    /// <returns>
    ///     A <see cref="Task" /> representing the asynchronous action.
    /// </returns>
    protected virtual async Task HandleCommitAsync()
    {
        try
        {
            await _dbContext.Database.CommitTransactionAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            // Log the exception first.
            _logger.LogError(e, "HandleCommit failed with exception");

            // Next, do a best-effort rollback.
            await HandleRollbackAsync().ConfigureAwait(false);

            // Rethrow the original exception for correct exception handling.
            throw;
        }
    }

    protected virtual bool IsSuccessStatusCode(HttpContext httpContext)
        => httpContext.Response.StatusCode is >= (int)HttpStatusCode.OK and <= 299;
}
