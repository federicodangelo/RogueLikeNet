namespace RogueLikeNet.Core.Data;

/// <summary>
/// Locates the data/ directory by searching upward from a starting path.
/// Works for dev (repo root) and deployed scenarios.
/// </summary>
public static class DataDirectory
{
    /// <summary>
    /// Finds the data directory by searching upward from the given start path (or current directory).
    /// Looks for a directory named "data" that contains an "items" subdirectory.
    /// </summary>
    public static string? Find(string? startPath = null)
    {
        var dir = startPath ?? Directory.GetCurrentDirectory();
        for (int i = 0; i < 8; i++) // limit depth to avoid infinite loop
        {
            var candidate = Path.Combine(dir, "data");
            if (Directory.Exists(candidate) && Directory.Exists(Path.Combine(candidate, "items")))
                return candidate;

            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return null;
    }

    /// <summary>
    /// Finds the data directory or throws if not found.
    /// </summary>
    public static string FindOrThrow(string? startPath = null) =>
        Find(startPath) ?? throw new DirectoryNotFoundException(
            $"Could not find 'data/items/' directory searching upward from '{startPath ?? Directory.GetCurrentDirectory()}'");
}
