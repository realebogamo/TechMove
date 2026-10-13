using GLMS.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace GLMS.UnitTests;

public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "glms-tests", Guid.NewGuid().ToString("N"));
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        _storage = new LocalFileStorage(Options.Create(new FileStorageOptions { RootPath = _root }), contentRoot: "unused");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Save_UsesGuidFileName_OnDisk_NotTheUploadedName()
    {
        var storedName = await _storage.SaveAsync(new MemoryStream(TestData.PdfBytes), ".pdf");

        Assert.Matches("^[0-9a-f]{32}\\.pdf$", storedName);
        Assert.True(File.Exists(Path.Combine(_root, storedName)));
        Assert.Equal(TestData.PdfBytes, await File.ReadAllBytesAsync(Path.Combine(_root, storedName)));
    }

    [Fact]
    public async Task Save_TwiceWithSameContent_ProducesDifferentNames()
    {
        var first = await _storage.SaveAsync(new MemoryStream(TestData.PdfBytes), ".pdf");
        var second = await _storage.SaveAsync(new MemoryStream(TestData.PdfBytes), ".pdf");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task OpenRead_ReturnsSavedContent()
    {
        var storedName = await _storage.SaveAsync(new MemoryStream(TestData.PdfBytes), ".pdf");

        await using var stream = await _storage.OpenReadAsync(storedName);
        using var copy = new MemoryStream();
        await stream!.CopyToAsync(copy);

        Assert.Equal(TestData.PdfBytes, copy.ToArray());
    }

    [Theory]
    [InlineData("../../appsettings.json")]
    [InlineData("..\\secrets.pdf")]
    [InlineData("not-a-guid.pdf")]
    public async Task OpenRead_RejectsNamesThatAreNotGeneratedGuids(string name)
    {
        Assert.Null(await _storage.OpenReadAsync(name));
    }

    [Fact]
    public async Task Delete_RemovesFile()
    {
        var storedName = await _storage.SaveAsync(new MemoryStream(TestData.PdfBytes), ".pdf");

        await _storage.DeleteAsync(storedName);

        Assert.False(File.Exists(Path.Combine(_root, storedName)));
    }
}
