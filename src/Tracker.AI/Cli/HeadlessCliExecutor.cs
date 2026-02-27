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
        var normalizedProvider = LlmProviderCatalog.NormalizeOrThrow(provider);
        if (!options.Enabled)
        {
            return new ProviderAvailability(normalizedProvider, false, false, options.Command, null, "Provider disabled in config.");
        }

        var resolution = ResolveProviderCommand(normalizedProvider, options.Command);
        if (!resolution.Found)
        {
            return new ProviderAvailability(
                normalizedProvider,
                true,
                false,
                resolution.SelectedCommand,
                null,
                resolution.Message);
        }

        return new ProviderAvailability(
            normalizedProvider,
            true,
            true,
            resolution.SelectedCommand,
            resolution.ResolvedPath,
            "Ready.");
    }

    public async Task<CliExecutionResult> ExecuteAsync(
        string provider,
        CliProviderOptions options,
        string input,
        CancellationToken cancellationToken = default)
    {
        var normalizedProvider = LlmProviderCatalog.NormalizeOrThrow(provider);
        var availability = CheckAvailability(normalizedProvider, options);
        if (!availability.Enabled)
        {
            throw new LlmException($"Provider '{normalizedProvider}' is disabled.", 503, "provider_unavailable");
        }

        if (!availability.Available || string.IsNullOrWhiteSpace(availability.ResolvedCommandPath))
        {
            throw new LlmException(
                $"Provider '{normalizedProvider}' command '{availability.Command ?? options.Command ?? "auto"}' is unavailable ({availability.Message}).",
                503,
                "provider_unavailable");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = availability.ResolvedCommandPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var argumentString = BuildArguments(options);
        if (!string.IsNullOrWhiteSpace(argumentString))
        {
            startInfo.Arguments = argumentString;
        }

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
            _logger.LogWarning("Provider {Provider} command timed out after {Timeout}s", normalizedProvider, options.TimeoutSeconds);
            throw new LlmException($"Provider '{normalizedProvider}' timed out.", 504, "llm_timeout");
        }

        sw.Stop();
        return new CliExecutionResult(
            process.ExitCode,
            stdoutTask.Result,
            stderrTask.Result,
            (int)sw.ElapsedMilliseconds,
            availability.ResolvedCommandPath);
    }

    private static string BuildArguments(CliProviderOptions options)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(options.Arguments))
        {
            parts.Add(options.Arguments.Trim());
        }

        foreach (var flag in options.ExtraFlags)
        {
            if (!string.IsNullOrWhiteSpace(flag))
            {
                parts.Add(EscapeArgument(flag.Trim()));
            }
        }

        return string.Join(" ", parts);
    }

    private static string EscapeArgument(string value)
    {
        if (value.Length == 0)
        {
            return "\"\"";
        }

        return value.Any(char.IsWhiteSpace) || value.Contains('"')
            ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : value;
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

    private static (bool Found, string? SelectedCommand, string? ResolvedPath, string Message) ResolveProviderCommand(
        string provider,
        string? configuredCommand)
    {
        if (!string.IsNullOrWhiteSpace(configuredCommand))
        {
            var resolvedConfigured = ResolveCommandPath(configuredCommand);
            return resolvedConfigured is null
                ? (false, configuredCommand, null, "Configured command not found in PATH.")
                : (true, configuredCommand, resolvedConfigured, "Ready.");
        }

        var candidates = LlmProviderCatalog.GetDefaultCommandCandidates(provider);
        foreach (var candidate in candidates)
        {
            var resolved = ResolveCommandPath(candidate);
            if (resolved is not null)
            {
                return (true, candidate, resolved, "Ready.");
            }
        }

        return (false, null, null, $"No configured command. Auto-detect failed for PATH candidates: {string.Join(", ", candidates)}.");
    }
}
