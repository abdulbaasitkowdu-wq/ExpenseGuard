using Microsoft.EntityFrameworkCore;

namespace ExpenseGuard.Api.Services;

public sealed class ApiExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var status = ex switch
            {
                KeyNotFoundException => StatusCodes.Status404NotFound,
                UnauthorizedAccessException => StatusCodes.Status403Forbidden,
                ArgumentException => StatusCodes.Status400BadRequest,
                InvalidOperationException => StatusCodes.Status409Conflict,
                DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError
            };
            context.Response.StatusCode = status;
            await Results.Problem(statusCode: status, title: ex.Message).ExecuteAsync(context);
        }
    }
}
