using Polly;
using Polly.Retry;

namespace EMMA.Ingestion.Extensions;

public static class ResilienceExtensions
{
    public static AsyncRetryPolicy CreateDbRetryPolicy(ILogger logger)
    {
        return Policy
            .Handle<Npgsql.NpgsqlException>(ex => ex.IsTransient)
            .Or<System.TimeoutException>()
            .WaitAndRetryAsync(5,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    logger.LogWarning(exception, "Database operation failed. Retrying in {TimeSpan}. Attempt {RetryCount}.", timeSpan, retryCount);
                });
    }
}
