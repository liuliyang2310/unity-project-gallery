using System.IO;
using UnityProjectGallery.App.ViewModels;
using UnityProjectGallery.Core.Media;
using UnityProjectGallery.Infrastructure.Persistence;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Tests.Persistence;

public sealed class ProjectMetadataJsonStoreTests
{
    [Fact]
    public async Task WriteAsync_CreatesPreviewFolderAndRoundTripsDocument()
    {
        var projectPath = CreateProjectDirectory();
        var savedAt = new DateTimeOffset(2026, 10, 31, 10, 0, 0, TimeSpan.FromHours(8));

        try
        {
            var store = new ProjectMetadataJsonStore();
            var document = new ProjectMetadataDocument
            {
                SavedAt = savedAt,
                Description = "基于 Boids 的鸟群演示",
                Tags = new[] { "3D", "学习" },
                Features = new[]
                {
                    new ProjectFeature
                    {
                        Id = "feature-1",
                        Name = "群体寻路",
                        Description = "演示避障。"
                    }
                },
                Media = new[]
                {
                    new ProjectMediaMetadata
                    {
                        RelativePath = "01.png",
                        Description = "封面截图",
                        IsIncluded = false,
                        FeatureId = "feature-1"
                    }
                },
                CoverRelativePath = "01.png",
                IsFavorite = true,
                LastOpenedTime = savedAt
            };

            await store.WriteAsync(projectPath, document);

            var metadataPath = ProjectMetadataJsonStore.GetMetadataPath(projectPath);
            Assert.True(File.Exists(metadataPath));
            Assert.True(Directory.Exists(Path.Combine(projectPath, "_ProjectPreview")));

            var restored = await store.ReadAsync(projectPath);
            Assert.NotNull(restored);
            Assert.Equal(savedAt, restored.SavedAt);
            Assert.Equal("基于 Boids 的鸟群演示", restored.Description);
            Assert.Equal(new[] { "3D", "学习" }, restored.Tags);
            Assert.True(restored.IsFavorite);
            Assert.Equal("01.png", restored.CoverRelativePath);
            Assert.Equal(savedAt, restored.LastOpenedTime);

            var feature = Assert.Single(restored.Features);
            Assert.Equal("feature-1", feature.Id);
            Assert.Equal("群体寻路", feature.Name);

            var media = Assert.Single(restored.Media);
            Assert.Equal("01.png", media.RelativePath);
            Assert.Equal("封面截图", media.Description);
            Assert.False(media.IsIncluded);
            Assert.Equal("feature-1", media.FeatureId);

            // 文件为可读的 UTF-8 JSON，中文不应被转义。
            var json = await File.ReadAllTextAsync(metadataPath);
            Assert.Contains("基于 Boids 的鸟群演示", json);
            Assert.False(File.Exists(metadataPath + ".tmp"));
        }
        finally
        {
            Directory.Delete(projectPath, true);
        }
    }

    [Fact]
    public async Task ReadAsync_ReturnsNullForMissingOrCorruptDocument()
    {
        var projectPath = CreateProjectDirectory();

        try
        {
            var store = new ProjectMetadataJsonStore();
            Assert.Null(await store.ReadAsync(projectPath));

            var previewDirectory = Path.Combine(projectPath, "_ProjectPreview");
            Directory.CreateDirectory(previewDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(previewDirectory, ProjectMetadataJsonStore.FileName),
                "{ this is not json");

            Assert.Null(await store.ReadAsync(projectPath));
            Assert.Null(await store.ReadAsync(Path.Combine(projectPath, "MissingProject")));
        }
        finally
        {
            Directory.Delete(projectPath, true);
        }
    }

    [Fact]
    public void ProjectItemViewModel_UsesMetadataWhenItIsNewer()
    {
        var projectPath = Path.Combine(
            Path.GetTempPath(),
            "upg-tests",
            Guid.NewGuid().ToString("N"),
            "TestProject");

        var savedAt = DateTimeOffset.UtcNow;
        var snapshot = new UnityProject
        {
            Id = projectPath,
            Name = "TestProject",
            ProjectPath = projectPath,
            UnityVersion = "2022.3.62f1",
            Description = "旧简介",
            Tags = new[] { "旧标签" },
            UserDataUpdatedAt = savedAt.AddMinutes(-5),
            Features = new[]
            {
                new ProjectFeature { Id = "old-feature", Name = "旧功能" }
            },
            Media = new[]
            {
                new ProjectMediaAsset
                {
                    FilePath = Path.Combine(projectPath, "_ProjectPreview", "01.png"),
                    FileName = "01.png",
                    Kind = ProjectMediaKind.Image,
                    RelativePath = "01.png",
                    ThumbnailPath = Path.Combine(projectPath, "_ProjectPreview", "01-thumb.png")
                },
                new ProjectMediaAsset
                {
                    FilePath = Path.Combine(projectPath, "_ProjectPreview", "02.png"),
                    FileName = "02.png",
                    Kind = ProjectMediaKind.Image,
                    RelativePath = "02.png",
                    ThumbnailPath = Path.Combine(projectPath, "_ProjectPreview", "02-thumb.png")
                }
            },
            CoverMediaPath = Path.Combine(projectPath, "_ProjectPreview", "01.png")
        };

        var metadata = new ProjectMetadataDocument
        {
            SavedAt = savedAt,
            Description = "新的简介",
            Tags = new[] { "3D", "演示" },
            IsFavorite = true,
            LastOpenedTime = savedAt,
            Features = new[]
            {
                new ProjectFeature
                {
                    Id = "feature-json",
                    Name = "群体寻路",
                    Description = "来自工程目录的 JSON。"
                }
            },
            Media = new[]
            {
                new ProjectMediaMetadata
                {
                    RelativePath = "01.png",
                    Description = "隐藏的封面截图",
                    IsIncluded = false,
                    FeatureId = "feature-json"
                }
            },
            CoverRelativePath = "02.png"
        };

        var projectItem = new ProjectItemViewModel(
            snapshot,
            _ => { },
            _ => { },
            metadata: metadata);

        Assert.Equal("新的简介", projectItem.Description);
        Assert.Equal(new[] { "3D", "演示" }, projectItem.Tags);
        Assert.True(projectItem.IsFavorite);
        Assert.Equal(savedAt, projectItem.LastOpenedTime);

        var feature = Assert.Single(projectItem.Features);
        Assert.Equal("feature-json", feature.Id);
        Assert.Equal("群体寻路", feature.Name);

        Assert.Single(projectItem.DisplayedMedia);
        Assert.Equal(1, projectItem.HiddenMediaCount);
        var hiddenMedia = Assert.Single(projectItem.AllMedia, media => !media.IsIncluded);
        Assert.Equal("隐藏的封面截图", hiddenMedia.Description);
        Assert.Equal("群体寻路", hiddenMedia.FeatureName);

        var cover = Assert.Single(projectItem.AllMedia, media => media.IsCover);
        Assert.Equal("02.png", cover.FileName);
        Assert.Equal(
            Path.Combine(projectPath, "_ProjectPreview", "02.png"),
            projectItem.CoverImagePath);

        var exported = projectItem.ToMetadataDocument();
        Assert.Equal(savedAt, exported.SavedAt);
        Assert.Equal("02.png", exported.CoverRelativePath);
        Assert.Equal("新的简介", exported.Description);
        Assert.Equal(metadata.Tags, exported.Tags);
    }

    [Fact]
    public void ProjectItemViewModel_IgnoresMetadataOlderThanSnapshot()
    {
        var savedAt = DateTimeOffset.UtcNow;
        var snapshot = new UnityProject
        {
            Id = @"C:\UnityProjects\TestProject",
            Name = "TestProject",
            ProjectPath = @"C:\UnityProjects\TestProject",
            UnityVersion = "2022.3.62f1",
            Description = "数据库中的较新简介",
            Tags = new[] { "数据库" },
            IsFavorite = true,
            UserDataUpdatedAt = savedAt
        };

        var metadata = new ProjectMetadataDocument
        {
            SavedAt = savedAt.AddMinutes(-10),
            Description = "JSON 中的旧简介",
            Tags = new[] { "旧" },
            IsFavorite = false
        };

        var projectItem = new ProjectItemViewModel(
            snapshot,
            _ => { },
            _ => { },
            metadata: metadata);

        Assert.Equal("数据库中的较新简介", projectItem.Description);
        Assert.Equal(new[] { "数据库" }, projectItem.Tags);
        Assert.True(projectItem.IsFavorite);
    }

    [Fact]
    public void ProjectItemViewModel_FlagsUserEditsSeparatelyFromScanUpdates()
    {
        var project = new UnityProject
        {
            Id = @"C:\UnityProjects\TestProject",
            Name = "TestProject",
            ProjectPath = @"C:\UnityProjects\TestProject",
            UnityVersion = "2022.3.62f1"
        };

        var flags = new List<bool>();
        var projectItem = new ProjectItemViewModel(
            project,
            _ => { },
            _ => { },
            (_, isUserEdit) => flags.Add(isUserEdit));

        Assert.Empty(flags);

        projectItem.SetDescription("新的简介");
        projectItem.SetTags(new[] { "3D" });
        projectItem.ToggleFavoriteCommand.Execute(null);

        Assert.Equal(new[] { true, true, true }, flags);

        projectItem.ApplyMediaResult(new ProjectMediaScanResult(
            project.ProjectPath,
            Array.Empty<ProjectMediaAsset>(),
            Array.Empty<string>(),
            true));

        Assert.False(flags[^1]);
    }

    private static string CreateProjectDirectory()
    {
        var projectPath = Path.Combine(
            Path.GetTempPath(),
            "upg-tests",
            Guid.NewGuid().ToString("N"),
            "TestProject");

        Directory.CreateDirectory(Path.Combine(projectPath, "Assets"));
        Directory.CreateDirectory(Path.Combine(projectPath, "Packages"));
        Directory.CreateDirectory(Path.Combine(projectPath, "ProjectSettings"));
        return projectPath;
    }
}
