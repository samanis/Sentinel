using Microsoft.AspNetCore.Diagnostics;
using Sentinel.Application.AI;
using Sentinel.Infrastructure.AI;

namespace Sentinel.RagApi;

internal sealed partial class RagExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<RagExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
            EmbeddingException => (StatusCodes.Status502BadGateway, "Embedding model unavailable"),
            StructuredModelException => (StatusCodes.Status502BadGateway, "Answer model unavailable"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        LogRequestFailure(logger, exception, status);
        context.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = exception.Message
            }
        });
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "RAG request failed with status {StatusCode}")]
    private static partial void LogRequestFailure(
        ILogger logger,
        Exception exception,
        int statusCode);
}
