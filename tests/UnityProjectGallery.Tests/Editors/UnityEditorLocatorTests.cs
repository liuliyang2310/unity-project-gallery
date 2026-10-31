using System.IO;
using UnityProjectGallery.Infrastructure.Editors;

namespace UnityProjectGallery.Tests.Editors;

public sealed class UnityEditorLocatorTests : IDisposable
{
    private readonly string _tempRoot;

    public UnityEditorLocatorTests()
    {
        _tempRoot = Path.Combine(
            Path.GetTempPath(),
            "UnityProjectGalleryEditorTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task DiscoverAsync_FindsEditorInVersionDirectory()
    {
        var editorDirectory = Path.Combine(_tempRoot, "2022.3.62f1", "Editor");
        Directory.CreateDirectory(editorDirectory);
        var unityExecutable = Path.Combine(editorDirectory, "Unity.exe");
        File.WriteAllText(unityExecutable, "fake editor");
        Directory.CreateDirectory(Path.Combine(_tempRoot, "2021.3.45f1"));

        var locator = new UnityEditorLocator(new[] { _tempRoot });

        var editors = await locator.DiscoverAsync();

        var editor = Assert.Single(editors);
        Assert.Equal("2022.3.62f1", editor.Version);
        Assert.Equal(unityExecutable, editor.ExecutablePath);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
