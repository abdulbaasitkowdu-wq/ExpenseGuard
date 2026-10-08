using ExpenseGuard.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseGuard.Api.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (ex is InsufficientBudgetException or DuplicateResourceException or
                                   InvalidBudgetOperationException or DbUpdateException or
                                   KeyNotFoundException)
        {
            var status = ex switch
            {
                KeyNotFoundException => StatusCodes.Status404NotFound,
                InvalidBudgetOperationException => StatusCodes.Status422UnprocessableEntity,
                _ => StatusCodes.Status409Conflict
            };
            logger.LogWarning(ex, "Request rejected: {Message}", ex.Message);
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = "Budget operation failed",
                Detail = ex.Message
            }, context.RequestAborted);
        }
    }
}
