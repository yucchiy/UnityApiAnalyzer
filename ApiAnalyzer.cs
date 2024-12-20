using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace UnityApiAnalyzer;

public sealed class ApiAnalyzer(
    ILogger<ApiAnalyzer> logger,
    UnityCsReferenceRepository repository)
{
    public async Task<Result> RunAsync(UnityVersion version, CancellationToken token)
    {
        repository.Fetch();

        if (repository.GetVersions().All(x => x != version))
        {
            logger.ZLogCritical($"Unity version {version} is not found.");
            throw new ArgumentException($"Unity version {version} is not found.");
        }

        repository.CheckoutVersion(version);

        using var workspace = MSBuildWorkspace.Create();

        var solutionPath = Path.Combine(repository.Path.ToString(), "Projects", "CSharp", "UnityReferenceSource.sln");

        logger.ZLogInformation($"Opening solution: {solutionPath}");
        var solution = await workspace.OpenSolutionAsync(solutionPath, null, null, token);
        logger.ZLogInformation($"Solution loaded: {solution.Projects.Count()} projects");

        var projectResults = new List<Result.Project>(solution.Projects.Count());
        foreach (var project in solution.Projects)
        {
            logger.ZLogInformation($"Trying to analyze project {project.Name}");
            var projectResult = await AnalyzeProjectAsync(project, token);
            if (projectResult != null)
            {
                projectResults.Add(projectResult);
            }
            else
            {
                logger.ZLogCritical($"Failed to analyze project {project.Name}");
            }
        }

        return new Result(
            projectResults.ToArray()
        );
    }

    private async Task<Result.Project?> AnalyzeProjectAsync(Project project, CancellationToken token)
    {
        var compilation = await project.GetCompilationAsync(token);
        if (compilation == null)
        {
            logger.ZLogCritical($"Failed to get compilation for project {project.Name}");
            return null;
        }

        var errorCount = compilation.GetDiagnostics().Count(x => x.Severity == DiagnosticSeverity.Error);
        if (errorCount > 0)
        {
            logger.ZLogCritical($"Project {project.Name} has {errorCount} errors.");
        }

        var typeResults = new List<Result.Type>();
        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var typeSymbols = tree.GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Select(x => semanticModel.GetDeclaredSymbol(x))
                .OfType<INamedTypeSymbol>()
                .Where(x => x.DeclaredAccessibility == Accessibility.Public)
                .ToArray();

            foreach (var typeSymbol in typeSymbols)
            {
                var publicMembers = typeSymbol.GetMembers()
                    .Where(x => x.DeclaredAccessibility == Accessibility.Public);
                typeResults.Add(new Result.Type(typeSymbol, publicMembers.ToArray()));
            }
        }

        return new Result.Project(
            project.Name,
            project.FilePath!,
            typeResults.ToArray());
    }

    public sealed class Result(Result.Project[] projects)
    {
        public sealed class Project(string name, string path, Type[] types)
        {
            public string Name { get; } = name;
            public string Path { get; } = path;
            public Type[] Types { get; } = types;
        }

        public sealed class Type(INamedTypeSymbol symbol, ISymbol[] members)
        {
            public INamedTypeSymbol Symbol { get; } = symbol;
            public ISymbol[] Members { get; } = members;
        }

        public Project[] Projects { get; } = projects;
    }
}