using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace YoloTest.Util;

public class XUnitLoggerProvider(ITestOutputHelper outputHelper) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new XUnitLogger(outputHelper, categoryName);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private class XUnitLogger(ITestOutputHelper outputHelper, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            try
            {
                var message = formatter(state, exception);
                outputHelper.WriteLine($"[{logLevel}] [{categoryName}] {message}");
                
                if (exception != null)
                {
                    outputHelper.WriteLine(exception.ToString());
                }
            }
            catch (InvalidOperationException)
            {
                // Test output is no longer available
            }
        }
    }
}
