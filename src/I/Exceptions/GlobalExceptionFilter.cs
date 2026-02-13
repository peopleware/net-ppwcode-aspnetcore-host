using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

using PPWCode.AspNetCore.Server.I.Exceptions;

namespace PPWCode.AspNetCore.Host.I.Exceptions;

[ExcludeFromCodeCoverage]
public sealed class GlobalExceptionFilter : IAsyncExceptionFilter
{
    private readonly IExceptionHandler[] _handlers;
    private readonly ILogger<GlobalExceptionFilter> _logger;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger, IEnumerable<IExceptionHandler> handlers)
    {
        _logger = logger;
        _handlers = handlers.ToArray();
    }

    /// <inheritdoc />
    public Task OnExceptionAsync(ExceptionContext context)
    {
        bool handled = false;
        foreach (IExceptionHandler handler in _handlers)
        {
            handled |= handler.Handle(context);
            if (handled)
            {
                break;
            }
        }

        if (!handled)
        {
            HttpContext httpContext = context.HttpContext;
            HttpRequest request = httpContext.Request;

            // Basic info
            string method = request.Method;
            PathString path = request.Path;
            string? queryString = request.QueryString.Value;

            // Headers
            Dictionary<string, string> headers = request.Headers
                .ToDictionary(h => h.Key, h => h.Value.ToString());

            _logger.LogError(
                context.Exception,
                "Unhandled exception: {Method} {Path}{Query}\nHeaders: {@Headers}",
                method,
                path,
                queryString,
                headers);

            context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }

        context.ExceptionHandled = true;

        return Task.CompletedTask;
    }
}
