using Microsoft.AspNetCore.Diagnostics;

namespace InventraUI.BackEnd.Common.Middlewares;

public class GlobalApiExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;

    public GlobalApiExceptionHandlerMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext, IExceptionHandler customExceptionHandler)
    {
        try
        {
            await _next(httpContext);

            // Once the response has started it can no longer be changed; writing to it would corrupt it
            // (the browser then shows ERR_INVALID_RESPONSE), so leave it as it is.
            if (httpContext.Response.HasStarted) return;

            switch (httpContext.Response.StatusCode)
            {
                case StatusCodes.Status401Unauthorized:
                    await customExceptionHandler.TryHandleAsync(
                        httpContext,
                        new UnauthorizedAccessException("Unauthorized - Token missing or invalid"),
                        CancellationToken.None
                    );
                    break;

                case StatusCodes.Status403Forbidden:
                    if (!httpContext.Response.HasStarted
                        && HttpMethods.IsGet(httpContext.Request.Method)
                        && !httpContext.Request.Path.StartsWithSegments("/api")
                        && httpContext.Request.Headers.Accept.ToString().Contains("text/html"))
                    {
                        httpContext.Response.Redirect("/Dashboards/DefaultDashboard?denied=1");
                        break;
                    }
                    await customExceptionHandler.TryHandleAsync(
                        httpContext,
                        new Exception("Forbidden - Access denied"),
                        CancellationToken.None
                    );
                    break;

                default:
                    // Let other status codes pass through
                    break;
            }
        }
        catch (Exception ex)
        {
            if (httpContext.Response.HasStarted) return;
            httpContext.Response.Clear();
            await customExceptionHandler.TryHandleAsync(httpContext, ex, CancellationToken.None);
        }
    }
}
