using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace UnityApiAnalyzer;

public sealed class UnityCsReferenceRepository : IDisposable
{
    public UnityCsReferenceRepository(ILogger<UnityCsReferenceRepository> logger, Workspace rootWorkspace)
    {
        _logger = logger;
        var workspace = rootWorkspace.GetOrCreateDirectory("UnityCsReference");

        _logger.ZLogInformation($"Checkout UnityCsReference to {workspace.FullName}");
        _logger.LogInformation("Cloning...");
        if (!Repository.IsValid(workspace.FullName))
        {
            Repository.Clone(GitUrl, workspace.FullName, new CloneOptions
            {
                OnCheckoutProgress =
                    (s, steps, totalSteps) => { _logger.ZLogInformation($"{s} ({steps} / {totalSteps})"); },
                IsBare = false
            });
            _logger.LogInformation("Cloned");
        }

        _repository = new Repository(workspace.FullName);
    }

    private readonly ILogger<UnityCsReferenceRepository> _logger;
    private readonly Repository _repository;

    private const string GitUrl = "https://github.com/Unity-Technologies/UnityCsReference.git";

    public DirectoryInfo Path => new(_repository.Info.WorkingDirectory);

    public IEnumerable<UnityVersion> GetVersions()
    {
        foreach (var tag in _repository.Tags)
        {
            if (UnityVersion.TryParse(tag.FriendlyName, out var version))
            {
                yield return version;
            }
        }
    }

    public void Fetch()
    {
        var remote = _repository.Network.Remotes["origin"];

        _logger.LogInformation($"Fetching...");

        Commands.Fetch(
            _repository,
            remote.Name,
            remote.FetchRefSpecs.Select(x => x.Specification),
            new FetchOptions
            {
                TagFetchMode = TagFetchMode.Auto,
                OnProgress = message =>
                {
                    _logger.LogInformation(message);
                    return true;
                }
            },
            "Fetch");

        _logger.LogInformation("Fetched.");
    }

    public void CheckoutVersion(UnityVersion version)
    {
        _logger.ZLogInformation($"Checking out version {version}...");
        var canonicalName = $"refs/tags/{version}";
        Commands.Checkout(
            _repository,
            canonicalName,
            new CheckoutOptions
            {
                CheckoutModifiers = CheckoutModifiers.Force
            }
        );
    }

    public void Dispose()
    {
        _repository.Dispose();
    }
}