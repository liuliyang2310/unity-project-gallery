using System.IO;
using System.Text.Json;
using UnityProjectGallery.Core.Scanning;
using UnityProjectGallery.Infrastructure.Scanning;

namespace UnityProjectGallery.Tests.Scanning;

public sealed class UnityProjectScannerTests : IDisposable
{
    private readonly string _tempRoot;

    public UnityProjectScannerTests()
    {
        _tempRoot = Path.Combine(
            Path.GetTempPath(),
            "UnityProjectGalleryTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task ScanAsync_DetectsProjectsAndReadsVersionAndPackages()
    {
        var projectsRoot = Path.Combine(_tempRoot, "UnityProjects");
        CreateProject(projectsRoot, "FlockingDemo", "2022.3.62f1", new Dictionary<string, string>
        {
            ["com.unity.collab-proxy"] = "2.4.3",
            ["com.unity.ide.rider"] = "3.0.31"
        });
        CreateProject(projectsRoot, "TerrainDemo", "6000.0.34f1", new Dictionary<string, string>
        {
            ["com.unity.render-pipelines.universal"] = "17.0.3"
        });
        CreateProject(Path.Combine(projectsRoot, "Vendor"), "VRDemo", "2021.3.45f1",
            new Dictionary<string, string>());

        var scanner = new UnityProjectScanner();

        var projects = await scanner.ScanAsync(new[] { _tempRoot });

        Assert.Equal(3, projects.Count);
        var byName = projects.ToDictionary(project => project.Name);
        Assert.Equal("2022.3.62f1", byName["FlockingDemo"].UnityVersion);
        Assert.Equal("6000.0.34f1", byName["TerrainDemo"].UnityVersion);
        Assert.Equal("2021.3.45f1", byName["VRDemo"].UnityVersion);
        Assert.Equal(2, byName["FlockingDemo"].Packages.Length);
        Assert.Equal("2.4.3", byName["FlockingDemo"].Packages
            .Single(package => package.Name == "com.unity.collab-proxy")
            .Version);
        Assert.Empty(byName["VRDemo"].Packages);
        Assert.All(projects, project =>
        {
            Assert.True(Path.IsPathRooted(project.ProjectPath));
            Assert.NotNull(project.LastScanTime);
            Assert.Equal(0, project.ScreenshotCount);
            Assert.Empty(project.Tags);
        });
    }

    [Fact]
    public async Task ScanAsync_DeduplicatesRepeatedRootsAndProjectPaths()
    {
        var projectsRoot = Path.Combine(_tempRoot, "UnityProjects");
        var firstProject = CreateProject(projectsRoot, "FirstProject", "2021.3.45f1",
            new Dictionary<string, string>());
        CreateProject(projectsRoot, "SecondProject", "2022.3.62f1",
            new Dictionary<string, string>());

        var scanner = new UnityProjectScanner();

        var projects = await scanner.ScanAsync(new[]
        {
            _tempRoot,
            _tempRoot,
            firstProject,
            firstProject + Path.DirectorySeparatorChar
        });

        Assert.Equal(2, projects.Count);
        Assert.Single(projects, project => project.Name == "FirstProject");
    }

    [Fact]
    public async Task ScanAsync_SkipsCacheDirectories()
    {
        var projectsRoot = Path.Combine(_tempRoot, "UnityProjects");
        CreateProject(projectsRoot, "VisibleProject", "2020.3.48f1",
            new Dictionary<string, string>());

        foreach (var cacheName in new[] { "Library", "Temp", "Logs", "Obj" })
        {
            CreateProject(
                Path.Combine(projectsRoot, cacheName),
                "HiddenProject",
                "2021.3.45f1",
                new Dictionary<string, string>());
        }

        var scanner = new UnityProjectScanner();

        var projects = await scanner.ScanAsync(new[] { _tempRoot });

        Assert.Single(projects);
        Assert.Equal("VisibleProject", projects[0].Name);
    }

    [Fact]
    public async Task ScanAsync_IgnoresInvalidRootsWithoutFailing()
    {
        var scanner = new UnityProjectScanner();
        var missingRoot = Path.Combine(_tempRoot, "does-not-exist");

        var projects = await scanner.ScanAsync(new[] { missingRoot });

        Assert.Empty(projects);
    }

    [Fact]
    public async Task ScanAsync_UsesFallbacksWhenMetadataIsMissingOrInvalid()
    {
        var projectsRoot = Path.Combine(_tempRoot, "UnityProjects");
        var projectPath = CreateProject(projectsRoot, "BrokenManifest", "2021.3.45f1",
            new Dictionary<string, string>());
        File.WriteAllText(Path.Combine(projectPath, "Packages", "manifest.json"), "{ invalid json");

        var versionFilePath = Path.Combine(projectPath, "ProjectSettings", "ProjectVersion.txt");
        File.Delete(versionFilePath);

        var scanner = new UnityProjectScanner();

        var projects = await scanner.ScanAsync(new[] { _tempRoot });

        Assert.Single(projects);
        Assert.Equal("未识别", projects[0].UnityVersion);
        Assert.Empty(projects[0].Packages);
    }

    [Fact]
    public async Task ScanAsync_ReportsProgressAndCompletion()
    {
        var projectsRoot = Path.Combine(_tempRoot, "UnityProjects");
        CreateProject(projectsRoot, "ProgressProject", "2023.2.20f1",
            new Dictionary<string, string>());
        var progress = new CollectingProgress();
        var scanner = new UnityProjectScanner();

        await scanner.ScanAsync(new[] { _tempRoot }, progress);

        Assert.NotEmpty(progress.Reports);
        Assert.Equal(ScanPhase.Starting, progress.Reports[0].Phase);
        Assert.Equal(ScanPhase.Completed, progress.Reports[^1].Phase);
        Assert.Equal(1, progress.Reports[^1].ProjectsFound);
    }

    [Fact]
    public async Task ScanAsync_ThrowsWhenCancellationIsRequested()
    {
        var projectsRoot = Path.Combine(_tempRoot, "UnityProjects");
        CreateProject(projectsRoot, "CanceledProject", "2021.3.45f1",
            new Dictionary<string, string>());
        var scanner = new UnityProjectScanner();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => scanner.ScanAsync(new[] { _tempRoot }, null, cancellation.Token));
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

    private static string CreateProject(
        string parentDirectory,
        string projectName,
        string unityVersion,
        IReadOnlyDictionary<string, string> packages)
    {
        var projectPath = Path.Combine(parentDirectory, projectName);
        Directory.CreateDirectory(Path.Combine(projectPath, "Assets"));
        Directory.CreateDirectory(Path.Combine(projectPath, "Packages"));
        Directory.CreateDirectory(Path.Combine(projectPath, "ProjectSettings"));

        File.WriteAllText(
            Path.Combine(projectPath, "ProjectSettings", "ProjectVersion.txt"),
            $"m_EditorVersion: {unityVersion}{Environment.NewLine}" +
            $"m_EditorVersionWithRevision: {unityVersion} (test){Environment.NewLine}");

        var manifest = new
        {
            dependencies = packages
        };
        File.WriteAllText(
            Path.Combine(projectPath, "Packages", "manifest.json"),
            JsonSerializer.Serialize(manifest));

        return projectPath;
    }

    private sealed class CollectingProgress : IProgress<ScanProgress>
    {
        public List<ScanProgress> Reports { get; } = new();

        public void Report(ScanProgress value) => Reports.Add(value);
    }
}
