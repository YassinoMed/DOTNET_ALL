using Microsoft.AspNetCore.Mvc;

namespace ProductsAPI.Middleware
{
    /// <summary>
    /// Convertit les exceptions métier en réponses HTTP normalisées (ProblemDetails)
    /// au lieu de laisser fuiter une erreur 500.
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (KeyNotFoundException exception)
            {
                await WriteProblemAsync(context, StatusCodes.Status404NotFound, "Not Found", exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Bad Request", exception.Message);
            }
            catch (ArgumentException exception)
            {
                await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Bad Request", exception.Message);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unhandled exception while processing {Path}", context.Request.Path);

                await WriteProblemAsync(
                    context,
                    StatusCodes.Status500InternalServerError,
                    "Internal Server Error",
                    "An unexpected error occurred.");
            }
        }

        private static async Task WriteProblemAsync(
            HttpContext context,
            int statusCode,
            string title,
            string detail)
        {
            if (context.Response.HasStarted)
                return;

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = context.Request.Path
            };

            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
