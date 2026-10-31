using System.IO;
using UnityProjectGallery.App.ViewModels;
using UnityProjectGallery.Core.Media;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Tests.ViewModels;

public sealed class ProjectPersistenceTests
{
    [Fact]
    public void ProjectItemViewModel_RestoresPersistedContent()
    {
        var project = new UnityProject
        {
            Id = @"C:\UnityProjects\TestProject",
            Name = "TestProject",
            ProjectPath = @"C:\UnityProjects\TestProject",
            UnityVersion = "2022.3.62f1",
            Description = "持久化后的简介",
            Tags = new[] { "3D", "演示" },
            IsFavorite = true,
            LastOpenedTime = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(8)),
            Features = new[]
            {
                new ProjectFeature
                {
                    Id = "feature-1",
                    Name = "群体寻路",
                    Description = "演示 Boids 行为。"
                }
            },
            Media = new[]
            {
                new ProjectMediaAsset
                {
                    FilePath = @"C:\UnityProjects\TestProject\_ProjectPreview\01.png",
                    FileName = "01.png",
                    Kind = ProjectMediaKind.Image,
                    RelativePath = "01.png",
                    PixelWidth = 800,
                    PixelHeight = 450,
                    ThumbnailPath = @"C:\Cache\01.png",
                    Description = "封面截图",
                    IsIncluded = false,
                    FeatureId = "feature-1"
                },
                new ProjectMediaAsset
                {
                    FilePath = @"C:\UnityProjects\TestProject\_ProjectPreview\02.png",
                    FileName = "02.png",
                    Kind = ProjectMediaKind.Image,
                    RelativePath = "02.png",
                    PixelWidth = 640,
                    PixelHeight = 360,
                    ThumbnailPath = @"C:\Cache\02.png"
                }
            },
            CoverMediaPath = @"C:\UnityProjects\TestProject\_ProjectPreview\01.png",
            ScreenshotCount = 2
        };

        var projectItem = new ProjectItemViewModel(project, _ => { }, _ => { });

        Assert.Equal("持久化后的简介", projectItem.Description);
        Assert.Equal(new[] { "3D", "演示" }, projectItem.Tags);
        Assert.True(projectItem.IsFavorite);
        Assert.Equal(project.LastOpenedTime, projectItem.LastOpenedTime);

        var feature = Assert.Single(projectItem.Features);
        Assert.Equal("feature-1", feature.Id);
        Assert.Equal("群体寻路", feature.Name);

        Assert.Equal(2, projectItem.MediaCount);
        Assert.Single(projectItem.DisplayedMedia);
        Assert.Equal(1, projectItem.HiddenMediaCount);
        Assert.Equal(2, projectItem.ScreenshotCount);

        var restoredCover = Assert.Single(projectItem.AllMedia, media => media.IsCover);
        Assert.Equal("01.png", restoredCover.FileName);
        Assert.Equal("封面截图", restoredCover.Description);
        Assert.Equal("群体寻路", restoredCover.FeatureName);
        Assert.Equal(projectItem.CoverThumbnailPath, restoredCover.ThumbnailPath);
    }

    [Fact]
    public void RelinkProject_UpdatesProjectAndMediaPaths()
    {
        var tempRoot = CreateTempDirectory();
        var newProjectPath = Path.Combine(tempRoot, "MovedProject");

        try
        {
            CreateUnityProjectDirectories(newProjectPath);

            var mediaPath = Path.Combine(tempRoot, "MovedProject", "_ProjectPreview", "01.png");
            var project = new UnityProject
            {
                Id = @"C:\UnityProjects\TestProject",
                Name = "TestProject",
                ProjectPath = Path.Combine(tempRoot, "MissingProject"),
                UnityVersion = "2022.3.62f1",
                Media = new[]
                {
                    new ProjectMediaAsset
                    {
                        FilePath = Path.Combine(tempRoot, "MissingProject", "_ProjectPreview", "01.png"),
                        FileName = "01.png",
                        Kind = ProjectMediaKind.Image,
                        RelativePath = "01.png"
                    }
                },
                CoverMediaPath = Path.Combine(tempRoot, "MissingProject", "_ProjectPreview", "01.png")
            };

            var saveCount = 0;
            var projectItem = new ProjectItemViewModel(
                project,
                _ => { },
                _ => { },
                (_, _) => saveCount++);

            Assert.True(projectItem.IsProjectPathMissing);

            Assert.True(projectItem.RelinkProject(newProjectPath));
            Assert.Equal(newProjectPath, projectItem.ProjectPath);
            Assert.False(projectItem.IsProjectPathMissing);
            Assert.Equal(mediaPath, Assert.Single(projectItem.AllMedia).FilePath);
            Assert.Equal(mediaPath, projectItem.CoverImagePath);
            Assert.Equal(1, saveCount);

            var snapshot = projectItem.ToModelSnapshot();
            Assert.Equal(newProjectPath, snapshot.ProjectPath);
            Assert.Equal(mediaPath, Assert.Single(snapshot.Media).FilePath);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void RelinkProject_RejectsDirectoryThatIsNotUnityProject()
    {
        var tempRoot = CreateTempDirectory();

        try
        {
            var invalidPath = Path.Combine(tempRoot, "NotAUnityProject");
            Directory.CreateDirectory(invalidPath);

            var project = new UnityProject
            {
                Id = @"C:\UnityProjects\TestProject",
                Name = "TestProject",
                ProjectPath = Path.Combine(tempRoot, "MissingProject"),
                UnityVersion = "2022.3.62f1"
            };

            var saveCount = 0;
            var projectItem = new ProjectItemViewModel(
                project,
                _ => { },
                _ => { },
                (_, _) => saveCount++);

            Assert.False(projectItem.RelinkProject(invalidPath));
            Assert.Equal(project.ProjectPath, projectItem.ProjectPath);
            Assert.True(projectItem.IsProjectPathMissing);
            Assert.Equal(0, saveCount);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "upg-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreateUnityProjectDirectories(string path)
    {
        Directory.CreateDirectory(Path.Combine(path, "Assets"));
        Directory.CreateDirectory(Path.Combine(path, "Packages"));
        Directory.CreateDirectory(Path.Combine(path, "ProjectSettings"));
        Directory.CreateDirectory(Path.Combine(path, "_ProjectPreview"));
    }
}
