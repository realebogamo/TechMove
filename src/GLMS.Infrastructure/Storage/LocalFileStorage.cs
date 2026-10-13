using System.Text.RegularExpressions;
using GLMS.Core.Repositories;
using Microsoft.Extensions.Options;

namespace GLMS.Infrastructure.Storage;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Folder for uploaded agreements. Relative paths are resolved against the app's content root.</summary>
    public string RootPath { get; set; } = "App_Data/agreements";
}

/// <summary>
/// Saves files to a server folder outside wwwroot so they can only be reached through the download action.
/// Files are named with a new GUID, so user-supplied names never reach the file system (no overwrites,
/// no path traversal).
/// </summary>
public partial class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<FileStorageOptions> options, string contentRoot)
    {
        var configured = options.Value.RootPath;
        _root = Path.IsPathRooted(configured) ? configured : Path.Combine(contentRoot, configured);
        Directory.CreateDirectory(_root);
    }

    public string RootPath => _root;

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default)
    {
        var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        if (!IsSafeName(storedName))
            throw new ArgumentException("Unsupported file extension.", nameof(extension));

        if (content.CanSeek)
            content.Position = 0;

        await using var file = new FileStream(Path.Combine(_root, storedName), FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, ct);
        return storedName;
    }

    public Task<Stream?> OpenReadAsync(string storedName, CancellationToken ct = default)
    {
        var path = ResolvePath(storedName);
        Stream? stream = path is not null && File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storedName, CancellationToken ct = default)
    {
        var path = ResolvePath(storedName);
        if (path is not null && File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private string? ResolvePath(string storedName) =>
        IsSafeName(storedName) ? Path.Combine(_root, storedName) : null;

    private static bool IsSafeName(string name) => StoredNamePattern().IsMatch(name);

    [GeneratedRegex("^[0-9a-f]{32}\\.pdf$")]
    private static partial Regex StoredNamePattern();
}
