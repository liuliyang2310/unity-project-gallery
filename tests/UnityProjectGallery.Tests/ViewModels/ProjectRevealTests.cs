using System.IO;
using UnityProjectGallery.App.Services;
using UnityProjectGallery.App.ViewModels;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Tests.ViewModels;

public sealed class ProjectRevealTests : IDisposable
{
    private readonly string _tempRoot;

    public ProjectRevealTests()
    {
        _tempRoot = Path.Combine(
            Path.GetTempPath(),
            "UnityProjectGalleryRevealTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void RevealProjectCommand_OpensProjectAssetsFolder()
    {
        var projectPath = CreateUnityProject("TestProject");
        var revealService = new FakeRevealService();
        var statusMessage = string.Empty;
        var viewModel = CreateDetailViewModel(
            projectPath,
            revealService,
            message => statusMessage = message);

        viewModel.RevealProjectCommand.Execute(null);

        Assert.Equal(Path.Combine(projectPath, "Assets"), revealService.RevealedPath);
        Assert.Contains("Assets", statusMessage);
    }

    [Fact]
    public void RevealProjectCommand_ReportsMissingAssetsFolder()
    {
        var projectPath = Path.Combine(_tempRoot, "MissingAssets");
        Directory.CreateDirectory(Path.Combine(projectPath, "Packages"));
        Directory.CreateDirectory(Path.Combine(projectPath, "ProjectSettings"));

        var revealService = new FakeRevealService();
        var statusMessage = string.Empty;
        var viewModel = CreateDetailViewModel(
            projectPath,
            revealService,
            message => statusMessage = message);

        viewModel.RevealProjectCommand.Execute(null);

        Assert.Null(revealService.RevealedPath);
        Assert.Contains("Assets", statusMessage);
    }

    [Fact]
    public void BrowseAssetsCommand_OpensAssetBrowserForProject()
    {
        var projectPath = CreateUnityProject("AssetProject");
        var previewService = new FakeAssetPreviewService();
        var statusMessage = string.Empty;
        var viewModel = CreateDetailViewModel(
            projectPath,
            new FakeRevealService(),
            message => statusMessage = message,
            previewService);

        viewModel.BrowseAssetsCommand.Execute(null);

        Assert.Equal(projectPath, previewService.ProjectPath);
        Assert.Equal("AssetProject", previewService.ProjectName);
        Assert.Contains("Assets 预览", statusMessage);
    }

    [Fact]
    public void BrowseAssetsCommand_ReportsMissingAssetsFolder()
    {
        var projectPath = Path.Combine(_tempRoot, "MissingAssetPreview");
        Directory.CreateDirectory(Path.Combine(projectPath, "Packages"));
        Directory.CreateDirectory(Path.Combine(projectPath, "ProjectSettings"));

        var previewService = new FakeAssetPreviewService();
        var statusMessage = string.Empty;
        var viewModel = CreateDetailViewModel(
            projectPath,
            new FakeRevealService(),
            message => statusMessage = message,
            previewService);

        viewModel.BrowseAssetsCommand.Execute(null);

        Assert.Null(previewService.ProjectPath);
        Assert.Contains("Assets", statusMessage);
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

    private string CreateUnityProject(string name)
    {
        var projectPath = Path.Combine(_tempRoot, name);
        Directory.CreateDirectory(Path.Combine(projectPath, "Assets"));
        Directory.CreateDirectory(Path.Combine(projectPath, "Packages"));
        Directory.CreateDirectory(Path.Combine(projectPath, "ProjectSettings"));
        return projectPath;
    }

    private static ProjectDetailViewModel CreateDetailViewModel(
        string projectPath,
        IProjectFolderRevealService revealService,
        Action<string> showStatus,
        IAssetPreviewService? assetPreviewService = null)
    {
        var project = new UnityProject
        {
            Id = projectPath,
            Name = Path.GetFileName(projectPath),
            ProjectPath = projectPath,
            UnityVersion = "2022.3.62f1"
        };
        var projectItem = new ProjectItemViewModel(project, _ => { }, _ => { });

        return new ProjectDetailViewModel(
            projectItem,
            () => { },
            showStatus,
            new StubLaunchService(),
            new StubPathPicker(),
            revealService,
            assetPreviewService ?? new StubAssetPreviewService());
    }

    private sealed class FakeRevealService : IProjectFolderRevealService
    {
        public string? RevealedPath { get; private set; }

        public bool Reveal(string folderPath)
        {
            RevealedPath = folderPath;
            return true;
        }
    }

    private sealed class StubPathPicker : IProjectPathPicker
    {
        public string? PickProjectDirectory() => null;
    }

    private sealed class StubAssetPreviewService : IAssetPreviewService
    {
        public void ShowAssetBrowser(string projectName, string projectPath)
        {
        }
    }

    private sealed class FakeAssetPreviewService : IAssetPreviewService
    {
        public string? ProjectName { get; private set; }

        public string? ProjectPath { get; private set; }

        public void ShowAssetBrowser(string projectName, string projectPath)
        {
            ProjectName = projectName;
            ProjectPath = projectPath;
        }
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
