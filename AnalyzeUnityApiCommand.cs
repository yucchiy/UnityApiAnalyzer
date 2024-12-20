using System.Text;
using ConsoleAppFramework;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace UnityApiAnalyzer;

public sealed class AnalyzeUnityApiCommand(
    ILogger<AnalyzeUnityApiCommand> logger,
    ApiAnalyzer analyzer)
{
    private const int SuccessExitCode = 0;
    private const int FailureExitCode = 1;

    /// <summary>
    /// Analyze Unity API.
    /// </summary>
    /// <param name="a">Target unity version.</param>
    /// <param name="b">Target unity version.</param>
    /// <param name="outputDir">Output path.</param>
    /// <param name="token">cancellation token</param>
    [Command("")]
    public async Task<int> Analyze(
        string a,
        string b,
        string output,
        CancellationToken token)
    {
        if (!UnityVersion.TryParse(a, out var parsedUnityVersionA))
        {
            logger.ZLogCritical($"Failed to parse unity version \"{a}\".");
            return FailureExitCode;
        }

        if (!UnityVersion.TryParse(b, out var parsedUnityVersionB))
        {
            logger.ZLogCritical($"Failed to parse unity version \"{b}\".");
            return FailureExitCode;
        }

        if (parsedUnityVersionA.Major <= 2021 ||
            parsedUnityVersionB.Major <= 2021)
        {
            logger.ZLogCritical($"Unity version 2021 and earlier are not supported.");
            return FailureExitCode;
        }

        var outputDirectory = Directory.Exists(output) ? new DirectoryInfo(output) : Directory.CreateDirectory(output);

        try
        {
            var resultA = await analyzer.RunAsync(parsedUnityVersionA, token);
            var resultB = await analyzer.RunAsync(parsedUnityVersionB, token);

            await OutputDiff(outputDirectory, "UnityEngine", resultA, resultB);
            await OutputDiff(outputDirectory, "UnityEditor", resultA, resultB);
        }
        catch (Exception e)
        {
            logger.LogCritical($"{e.Message}");
            return FailureExitCode;
        }

        return SuccessExitCode;
    }

    private async Task OutputDiff(
        DirectoryInfo outputDirectory,
        string projectName,
        ApiAnalyzer.Result resultA,
        ApiAnalyzer.Result resultB)
    {
        var projectA = resultA.Projects.FirstOrDefault(x => x.Name == projectName);
        var projectB = resultB.Projects.FirstOrDefault(x => x.Name == projectName);
        if (projectA == null || projectB == null)
        {
            logger.ZLogCritical($"Failed to find project {projectName}.");
            return;
        }

        var membersA = projectA.Types
            .SelectMany(x => x.Members)
            .Select(x => x.GetDocumentationCommentId())
            .Distinct()
            .ToArray();

        var membersB = projectB.Types
            .SelectMany(x => x.Members)
            .Select(x => x.GetDocumentationCommentId())
            .Distinct()
            .ToArray();

        var sb = new StringBuilder();
        foreach (var member in membersA.Except(membersB))
        {
            sb.AppendLine(member);
        }

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory.FullName, $"{projectName}_added.txt"),
            sb.ToString());

        sb.Clear();

        foreach (var member in membersB.Except(membersA))
        {
            sb.AppendLine(member);
        }

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory.FullName, $"{projectName}_removed.txt"),
            sb.ToString());
    }
}