namespace UnityApiAnalyzer;

public sealed class Workspace : IDisposable
{
    private readonly DirectoryInfo _root;

    public Workspace()
    {
        _root = new DirectoryInfo(Path.GetTempPath());
        if (!_root.Exists)
        {
            _root.Create();
        }
    }

    public DirectoryInfo GetOrCreateDirectory(string name)
    {
        var path = Path.Combine(_root.FullName, name);
        if (!Directory.Exists(path))
        {
            return _root.CreateSubdirectory(name);
        }

        return new DirectoryInfo(path);
    }

    public void Dispose()
    {
        _root.Delete(true);
    }
}