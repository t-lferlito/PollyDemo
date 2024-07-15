using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using System;
using System.Linq.Expressions;

public class PollyDemo
{
    public static async Task Main()
    {
        //CircuitBreakerDemo().Wait();
        //PollyRetryDemo().Wait();
        BothPoliciesWrappedDemo().Wait();
    }

    public static async Task CircuitBreakerDemo()
    {
        int totalApiCall = 6;
        Console.WriteLine("Starting Polly Circuit Breaker Demo");

        var circuitBreakerPolicy = InitializeCircuitBreakerPolicy();

        for (int apiCallNum = 0; apiCallNum < totalApiCall; apiCallNum++)
        {
            try
            {
                await circuitBreakerPolicy.ExecuteAsync(() => SimulatedApiCall(apiCallNum));
                Log($"API call #{apiCallNum + 1} was successful. Circuit State: {circuitBreakerPolicy.CircuitState}");
            }
            catch (Exception ex) when (ex is BrokenCircuitException)
            {
                Log($"API call #{apiCallNum + 1} was not successful. Circuit State: {circuitBreakerPolicy.CircuitState}");
            }
            catch (Exception)
            {
                Log($"An unexpected error occurred. API call #{apiCallNum + 1} was not successful. Circuit State: {circuitBreakerPolicy.CircuitState}");
            }
            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        Log("Done with Circuit Breaker Demo");
    }

    public static async Task PollyRetryDemo()
    {
        int totalApiCall = 3;
        var retryPolicy = InitializeRetryPolicy();

        for (int apiCallNum = 0; apiCallNum < totalApiCall; apiCallNum++)
        {
            try
            {
                await retryPolicy.ExecuteAsync(() => SimulatedApiCall(apiCallNum));
                Log($"API call #{apiCallNum + 1} was successful");
            }
            catch (Exception ex)
            {
                Log($"API call #{apiCallNum + 1} failed: {ex.Message}");
            }
        }
        await Task.Delay(TimeSpan.FromSeconds(5));
    }

    public static async Task BothPoliciesWrappedDemo()
    {
        int totalApiCall = 6;
        var retryPolicy = InitializeRetryPolicy();
        var circuitBreakerPolicy = InitializeCircuitBreakerPolicy();

        for (int apiCallNum = 0; apiCallNum < totalApiCall; apiCallNum++)
        {
            try
            {
                await retryPolicy.ExecuteAsync(() => circuitBreakerPolicy.ExecuteAsync(() => SimulatedApiCall(apiCallNum)));
                Log($"API call #{apiCallNum + 1} was successful");
            }
            catch (Exception ex)
            {
                Log($"API call #{apiCallNum + 1} failed: {ex.Message}");
            }
        }
        await Task.Delay(TimeSpan.FromSeconds(5));
    }

        private static Task SimulatedApiCall(int attemptNum)
    {
        Log($"Making API call #{attemptNum + 1}...");
        if (attemptNum < 2)
        {
            throw new Exception("Simulated failure");
        }

        return Task.CompletedTask;
    }

    public static void Log(string message)
    {
        Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " - " + message);
    }

    public static AsyncCircuitBreakerPolicy InitializeCircuitBreakerPolicy()
    {
        var circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .AdvancedCircuitBreakerAsync(
                failureThreshold: 0.5,
                samplingDuration: TimeSpan.FromSeconds(60),
                minimumThroughput: 2,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (ex, @break) => Log($"BREAK. Duration of break for {@break:ss\\.fff} sec due to: {ex.GetType().Name}"),
                onHalfOpen: () => Log($"Circuit State: Half-Open"),
                onReset: () => Log("Resetting Circuit Breaker"));

        return circuitBreakerPolicy;
    }

    public static AsyncRetryPolicy InitializeRetryPolicy()
    {
        var retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
            retryCount: 4,
            sleepDurationProvider: retryAttempt =>
            {
                var sleepDuration = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                Log($"Sleeping for {sleepDuration.TotalSeconds} seconds");
                return sleepDuration;
            },
            onRetry: (exception, retryCount, context) => Log($"Retry {retryCount} due to: {exception.Message}"));

        return retryPolicy;
    }
}
