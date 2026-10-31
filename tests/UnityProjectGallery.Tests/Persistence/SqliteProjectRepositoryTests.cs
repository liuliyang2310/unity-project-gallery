using System.IO;
using UnityProjectGallery.Infrastructure.Persistence;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Tests.Persistence;

public sealed class SqliteProjectRepositoryTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _databasePath;

    public SqliteProjectRepositoryTests()
    {
        _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "UnityProjectGalleryDbTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _databasePath = Path.Combine(_tempDirectory, "gallery.db");
    }

    [Fact]
    public async Task SaveProjectAsync_PersistsCompleteSnapshotAcrossRepositoryInstances()
    {
        var repository = new SqliteProjectRepository(_databasePath);
        var project = BuildProject(@"C:\UnityProjects\FlockingDemo");
        project = new UnityProject
        {
            Id = project.Id,
            Name = project.Name,
            ProjectPath = project.ProjectPath,
            UnityVersion = project.UnityVersion,
            Description = "Boids 群体行为演示",
            Tags = new[] { "3D", "学习" },
            IsFavorite = true,
            LastOpenedTime = DateTimeOffset.Now.AddHours(-2),
            LastModifiedTime = project.LastModifiedTime,
            LastScanTime = project.LastScanTime,
            ScreenshotCount = 1,
            VideoCount = 0,
            FeatureCount = 1,
            Packages = project.Packages,
            Features = new[]
            {
                new ProjectFeature
                {
                    Id = "feature-1",
                    Name = "鸟群群体寻路",
                    Description = "展示 Boids 群体移动。"
                }
            },
            Media = new[]
            {
                new ProjectMediaAsset
                {
                    FilePath = @"C:\UnityProjects\FlockingDemo\_ProjectPreview\Flocking_01.png",
                    FileName = "Flocking_01.png",
                    Kind = ProjectMediaKind.Image,
                    RelativePath = "Flocking_01.png",
                    PixelWidth = 800,
                    PixelHeight = 450,
                    ThumbnailPath = @"C:\Cache\Flocking_01.png",
                    Description = "群体转向截图",
                    IsIncluded = false,
                    FeatureId = "feature-1"
                }
            },
            CoverMediaPath = @"C:\UnityProjects\FlockingDemo\_ProjectPreview\Flocking_01.png"
        };

        await repository.SaveProjectAsync(project);

        var reloadedRepository = new SqliteProjectRepository(_databasePath);
        var reloaded = Assert.Single(await reloadedRepository.GetAllAsync());

        Assert.Equal("Boids 群体行为演示", reloaded.Description);
        Assert.Equal(new[] { "3D", "学习" }, reloaded.Tags);
        Assert.True(reloaded.IsFavorite);
        Assert.NotNull(reloaded.LastOpenedTime);
        Assert.Single(reloaded.Features);
        Assert.Equal("鸟群群体寻路", reloaded.Features[0].Name);
        var media = Assert.Single(reloaded.Media);
        Assert.False(media.IsIncluded);
        Assert.Equal("群体转向截图", media.Description);
        Assert.Equal("feature-1", media.FeatureId);
        Assert.Equal(project.CoverMediaPath, reloaded.CoverMediaPath);
    }

    [Fact]
    public async Task SaveProjectAsync_UpdatesPathByProjectId()
    {
        var repository = new SqliteProjectRepository(_databasePath);
        var original = BuildProject(@"C:\UnityProjects\OldLocation");
        await repository.SaveProjectAsync(original);

        var moved = BuildProject(@"D:\MovedProjects\NewLocation");
        moved = new UnityProject
        {
            Id = original.Id,
            Name = moved.Name,
            ProjectPath = moved.ProjectPath,
            UnityVersion = moved.UnityVersion,
            LastModifiedTime = moved.LastModifiedTime,
            LastScanTime = moved.LastScanTime,
            Packages = moved.Packages
        };
        await repository.SaveProjectAsync(moved);

        var projects = await repository.GetAllAsync();

        var project = Assert.Single(projects);
        Assert.Equal(moved.ProjectPath, project.ProjectPath);
    }

    [Fact]
    public async Task ScanRoots_RoundTrip()
    {
        var repository = new SqliteProjectRepository(_databasePath);
        var roots = new[] { @"C:\UnityProjects", @"D:\UnityProjects" };

        await repository.SaveScanRootsAsync(roots);
        var reloadedRepository = new SqliteProjectRepository(_databasePath);
        var reloadedRoots = await reloadedRepository.GetScanRootsAsync();

        Assert.Equal(roots, reloadedRoots);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static UnityProject BuildProject(string projectPath)
    {
        return new UnityProject
        {
            Id = "project-1",
            Name = "FlockingDemo",
            ProjectPath = projectPath,
            UnityVersion = "2022.3.62f1",
            LastModifiedTime = DateTimeOffset.Now.AddDays(-1),
            LastScanTime = DateTimeOffset.Now,
            Packages = new[]
            {
                new UnityPackageReference
                {
                    Name = "com.unity.ide.rider",
                    Version = "3.0.31"
                }
            }
        };
    }
}
