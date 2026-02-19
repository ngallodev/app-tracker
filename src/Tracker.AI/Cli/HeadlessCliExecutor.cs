using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Tracker.AI.Cli;

public sealed class HeadlessCliExecutor : IHeadlessCliExecutor
{
    private readonly ILogger<HeadlessCliExecutor> _logger;

    public HeadlessCliExecutor(ILogger<HeadlessCliExecutor> logger)
    {
        _logger = logger;
    }

    public ProviderAvailability CheckAvailability(string provider, CliProviderOptions options)
    {
        if (!options.Enabled)
        {
            return new ProviderAvailability(provider, false, false, options.Command, null, "Provider disabled in config.");
        }

        if (string.IsNullOrWhiteSpace(options.Command))
        {
            return new ProviderAvailability(provider, true, false, null, null, "No command configured.");
        }

        var resolved = ResolveCommandPath(options.Command);
        return resolved is null
            ? new ProviderAvailability(provider, true, false, options.Command, null, "Command not found in PATH.")
            : new ProviderAvailability(provider, true, true, options.Command, resolved, "Ready.");
    }

    public async Task<CliExecutionResult> ExecuteAsync(
        string provider,
        CliProviderOptions options,
        string input,
        CancellationToken cancellationToken = default)
    {
        var availability = CheckAvailability(provider, options);
        if (!availability.Enabled)
        {
            throw new LlmException($"Provider '{provider}' is disabled.", 503, "provider_unavailable");
        }

        if (!availability.Available || string.IsNullOrWhiteSpace(availability.ResolvedCommandPath))
        {
            throw new LlmException(
                $"Provider '{provider}' command '{options.Command}' is unavailable ({availability.Message}).",
                503,
                "provider_unavailable");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = availability.ResolvedCommandPath,
            Arguments = options.Arguments ?? string.Empty,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var sw = Stopwatch.StartNew();

        process.Start();

        await process.StandardInput.WriteAsync(input.AsMemory(), cancellationToken);
        await process.StandardInput.FlushAsync();
        process.StandardInput.Close();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds)));

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
        var waitTask = process.WaitForExitAsync(timeoutCts.Token);

        try
        {
            await Task.WhenAll(stdoutTask, stderrTask, waitTask);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            _logger.LogWarning("Provider {Provider} command timed out after {Timeout}s", provider, options.TimeoutSeconds);
            throw new LlmException($"Provider '{provider}' timed out.", 504, "llm_timeout");
        }

        sw.Stop();
        return new CliExecutionResult(
            process.ExitCode,
            stdoutTask.Result,
            stderrTask.Result,
            (int)sw.ElapsedMilliseconds,
            availability.ResolvedCommandPath);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // No-op: process may have already exited.
        }
    }

    private static string? ResolveCommandPath(string command)
    {
        if (Path.IsPathRooted(command) || command.Contains(Path.DirectorySeparatorChar) || command.Contains(Path.AltDirectorySeparatorChar))
        {
            return File.Exists(command) ? command : null;
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var pathDir in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(pathDir, command);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
