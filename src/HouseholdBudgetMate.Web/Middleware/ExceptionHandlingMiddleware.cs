using HouseholdBudgetMate.Application.Kernel.Exceptions;

namespace HouseholdBudgetMate.Web.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var statusCode = MapStatusCode(ex);
            logger.LogError(ex, "Unhandled exception mapped to HTTP {StatusCode} for {Path}", statusCode, context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = statusCode;

            if (HttpMethods.IsGet(context.Request.Method)
                && !context.Request.Path.StartsWithSegments("/Error"))
            {
                context.Response.Redirect("/Error", permanent: false);
                return;
            }

            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync("Wystapil blad podczas przetwarzania zadania.");
        }
    }

    private static int MapStatusCode(Exception exception)
    {
        return exception switch
        {
            BadRequestException => StatusCodes.Status400BadRequest,
            ImportException => StatusCodes.Status422UnprocessableEntity,
            NotFoundException => StatusCodes.Status404NotFound,
            ArgumentNullException => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status409Conflict,
            ApplicationException => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError
        };
    }
}

