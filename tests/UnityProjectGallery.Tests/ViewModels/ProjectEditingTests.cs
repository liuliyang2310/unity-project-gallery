using UnityProjectGallery.App.Services;
using UnityProjectGallery.App.ViewModels;
using UnityProjectGallery.Core.Media;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Tests.ViewModels;

public sealed class ProjectEditingTests
{
    [Fact]
    public void DetailViewModel_EditsDescriptionAndTags()
    {
        var project = CreateProject();
        var viewModel = CreateDetailViewModel(project);

        viewModel.EditDescriptionCommand.Execute(null);
        viewModel.DescriptionDraft = "基于 Boids 的鸟群演示";
        viewModel.SaveDescriptionCommand.Execute(null);

        Assert.Equal("基于 Boids 的鸟群演示", project.Description);
        Assert.Equal("基于 Boids 的鸟群演示", project.DescriptionDisplay);

        viewModel.EditTagsCommand.Execute(null);
        viewModel.TagsDraft = "3D, 学习，3D";
        viewModel.SaveTagsCommand.Execute(null);

        Assert.Equal(new[] { "3D", "学习" }, project.Tags);
    }

    [Fact]
    public void DetailViewModel_AddsFeatureAndAssociatesSelectedMedia()
    {
        var project = CreateProject();
        var media = CreateMedia(project);
        var viewModel = CreateDetailViewModel(project);

        viewModel.AddFeatureCommand.Execute(null);
        viewModel.FeatureNameDraft = "鸟群群体寻路";
        viewModel.FeatureDescriptionDraft = "演示 Boids 群体行为。";
        Assert.Equal(2, viewModel.FeatureMediaSelections.Count);
        viewModel.FeatureMediaSelections[0].IsSelected = true;
        viewModel.SaveFeatureCommand.Execute(null);

        var feature = Assert.Single(project.Features);
        Assert.Equal("鸟群群体寻路", feature.Name);
        Assert.Single(feature.Media);
        Assert.True(media[0].HasFeature);
        Assert.Equal("鸟群群体寻路", media[0].FeatureName);
        Assert.Equal(1, project.FeatureCount);
        Assert.True(project.HasFeatures);
    }

    [Fact]
    public void MediaDescription_IsEditableThroughDetailViewModel()
    {
        var project = CreateProject();
        var media = CreateMedia(project);
        var viewModel = CreateDetailViewModel(project);

        viewModel.BeginEditMedia(media[0]);
        viewModel.MediaDescriptionDraft = "群体转向与避障效果";
        viewModel.SaveMediaDescriptionCommand.Execute(null);

        Assert.Equal("群体转向与避障效果", media[0].Description);
        Assert.True(media[0].HasDescription);
        Assert.False(viewModel.IsEditingMedia);
    }

    [Fact]
    public void RemovingFeature_ClearsMediaAssociations()
    {
        var project = CreateProject();
        var media = CreateMedia(project);
        var feature = project.CreateFeature("示例功能", string.Empty);
        project.AssignMediaToFeature(feature, media);

        Assert.All(media, item => Assert.True(item.HasFeature));

        project.RemoveFeature(feature);

        Assert.Empty(project.Features);
        Assert.All(media, item => Assert.False(item.HasFeature));
    }

    private static ProjectItemViewModel CreateProject()
    {
        return new ProjectItemViewModel(
            new UnityProject
            {
                Id = "test-project",
                Name = "TestProject",
                ProjectPath = @"C:\UnityProjects\TestProject",
                UnityVersion = "2022.3.62f1"
            },
            _ => { },
            _ => { });
    }

    private static IReadOnlyList<ProjectMediaItemViewModel> CreateMedia(ProjectItemViewModel project)
    {
        var result = new ProjectMediaScanResult(
            project.ProjectPath,
            new[]
            {
                new ProjectMediaAsset
                {
                    FilePath = @"C:\UnityProjects\TestProject\_ProjectPreview\01.png",
                    FileName = "01.png",
                    Kind = ProjectMediaKind.Image,
                    RelativePath = "01.png",
                    PixelWidth = 800,
                    PixelHeight = 450,
                    ThumbnailPath = @"C:\Cache\01.png"
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
            Array.Empty<string>(),
            true);

        project.ApplyMediaResult(result);
        return project.AllMedia;
    }

    private static ProjectDetailViewModel CreateDetailViewModel(ProjectItemViewModel project)
    {
        return new ProjectDetailViewModel(
            project,
            () => { },
            _ => { },
            new StubLaunchService(),
            new StubPathPicker(),
            new StubRevealService());
    }

    private sealed class StubPathPicker : IProjectPathPicker
    {
        public string? PickProjectDirectory() => null;
    }

    private sealed class StubRevealService : IProjectFolderRevealService
    {
        public bool Reveal(string folderPath) => true;
    }

    private sealed class StubLaunchService : IProjectLaunchService
    {
        public Task<ProjectLaunchResult> LaunchAsync(
            string projectName,
            string projectPath,
            string unityVersion,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProjectLaunchResult(false, "未配置启动服务。"));
        }
    }
}
