using System.IO;
using UnityProjectGallery.Core.Models;
using UnityProjectGallery.Infrastructure.Models;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Tests.ModelPreview;

public sealed class AssetsPreviewScannerTests : IDisposable
{
    private readonly string _tempRoot;

    public AssetsPreviewScannerTests()
    {
        _tempRoot = Path.Combine(
            Path.GetTempPath(),
            "UnityProjectGalleryAssetScannerTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task ScanAsync_ReturnsModelsImagesAndAudioInsideAssetsOnly()
    {
        var projectPath = Path.Combine(_tempRoot, "Project");
        var assets = Path.Combine(projectPath, "Assets");
        var models = Path.Combine(assets, "Models");
        var images = Path.Combine(assets, "Textures");
        var audio = Path.Combine(assets, "Audio");
        Directory.CreateDirectory(models);
        Directory.CreateDirectory(images);
        Directory.CreateDirectory(audio);

        CreateFile(models, "character.obj");
        CreateFile(models, "robot.fbx");
        CreateFile(models, "README.txt");
        CreateFile(images, "preview.png");
        CreateFile(images, "texture.jpg");
        CreateFile(images, "background.webp");
        CreateFile(audio, "click.wav");
        CreateFile(audio, "music.ogg");
        CreateFile(assets, "Root.glb");
        CreateFile(assets, "Level.gltf");
        CreateFile(projectPath, "Outside.obj");

        var scanner = new AssetsPreviewScanner();
        var results = await scanner.ScanAsync(projectPath);

        Assert.Equal(9, results.Count);
        Assert.All(results, result =>
            Assert.StartsWith(assets, result.FilePath, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(4, results.Count(result => result.Kind == AssetPreviewKind.Model));
        Assert.Equal(3, results.Count(result => result.Kind == AssetPreviewKind.Image));
        Assert.Equal(2, results.Count(result => result.Kind == AssetPreviewKind.Audio));
        Assert.Contains(results, result => result.RelativePath == Path.Combine("Models", "character.obj"));
        Assert.Contains(results, result => result.RelativePath == Path.Combine("Audio", "music.ogg"));
        Assert.DoesNotContain(results, result => result.RelativePath.EndsWith("README.txt"));
        Assert.DoesNotContain(results, result => result.RelativePath.EndsWith("Outside.obj"));
    }

    [Fact]
    public async Task ScanAsync_GroupsByKindThenRelativePath()
    {
        var projectPath = Path.Combine(_tempRoot, "Sorted");
        var assets = Path.Combine(projectPath, "Assets");
        Directory.CreateDirectory(assets);
        CreateFile(assets, "z.obj");
        CreateFile(assets, "a.gltf");
        CreateFile(assets, "b.png");
        CreateFile(assets, "c.mp3");

        var scanner = new AssetsPreviewScanner();
        var results = await scanner.ScanAsync(projectPath);

        Assert.Equal(
            new[] { "a.gltf", "z.obj", "b.png", "c.mp3" },
            results.Select(result => result.RelativePath));
    }

    [Fact]
    public async Task ScanAsync_ReturnsEmptyWhenProjectHasNoAssetsFolder()
    {
        var projectPath = Path.Combine(_tempRoot, "MissingAssets");
        Directory.CreateDirectory(projectPath);

        var scanner = new AssetsPreviewScanner();
        var results = await scanner.ScanAsync(projectPath);

        Assert.Empty(results);
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

    private static void CreateFile(string directory, string fileName)
    {
        File.WriteAllText(Path.Combine(directory, fileName), string.Empty);
    }
}
