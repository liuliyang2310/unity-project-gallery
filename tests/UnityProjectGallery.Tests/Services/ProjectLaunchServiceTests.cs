using System.IO;
using UnityProjectGallery.App.Services;
using UnityProjectGallery.Core.Editors;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Tests.Services;

public sealed class ProjectLaunchServiceTests : IDisposable
{
    private readonly string _projectPath;
    private readonly string _executablePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "notepad.exe");

    public ProjectLaunchServiceTests()
    {
        _projectPath = Path.Combine(
            Path.GetTempPath(),
            "UnityProjectGalleryLaunchTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectPath);
    }

    [Fact]
    public async Task LaunchAsync_UsesMatchingEditorWithoutPrompting()
    {
        var process = new FakeProcessLauncher();
        var prompt = new FakePromptService();
        var service = CreateService(
            new FakeEditorLocator(new UnityEditorInstallation
            {
                Version = "2022.3.62f1",
                ExecutablePath = _executablePath
            }),
            prompt,
            process);

        var result = await service.LaunchAsync("TestProject", _projectPath, "2022.3.62f1");

        Assert.True(result.Launched);
        Assert.Equal(0, prompt.PromptCount);
        var invocation = Assert.Single(process.Invocations);
        Assert.Equal(_executablePath, invocation.ExecutablePath);
        Assert.Contains("-projectPath", invocation.Arguments);
        Assert.Contains(_projectPath, invocation.Arguments);
    }

    [Fact]
    public async Task LaunchAsync_PromptsForEditorAndRemembersCustomPath()
    {
        var process = new FakeProcessLauncher();
        var prompt = new FakePromptService
        {
            NextChoice = UnityEditorMissingChoice.ChooseEditor,
            PickedEditor = _executablePath
        };
        var service = CreateService(new FakeEditorLocator(), prompt, process);

        var first = await service.LaunchAsync("TestProject", _projectPath, "2021.3.45f1");
        var second = await service.LaunchAsync("TestProject", _projectPath, "2021.3.45f1");

        Assert.True(first.Launched);
        Assert.True(second.Launched);
        Assert.Equal(1, prompt.PromptCount);
        Assert.Equal(2, process.Invocations.Count);
        Assert.All(process.Invocations, invocation => Assert.Equal(_executablePath, invocation.ExecutablePath));
    }

    [Fact]
    public async Task LaunchAsync_OpensProjectWithUnityHubWhenRequested()
    {
        var process = new FakeProcessLauncher();
        var prompt = new FakePromptService
        {
            NextChoice = UnityEditorMissingChoice.UseUnityHub
        };
        var service = new ProjectLaunchService(
            new FakeEditorLocator(),
            prompt,
            process,
            new FakeHubLocator(_executablePath),
            new FakeEditorSettingsStore());

        var result = await service.LaunchAsync("TestProject", _projectPath, "2020.3.48f1");

        Assert.True(result.Launched);
        var invocation = Assert.Single(process.Invocations);
        Assert.Equal(_executablePath, invocation.ExecutablePath);
        Assert.Contains("--projectPath", invocation.Arguments);
        Assert.Contains(_projectPath, invocation.Arguments);
    }

    [Fact]
    public async Task LaunchAsync_RejectsMissingProjectPath()
    {
        var process = new FakeProcessLauncher();
        var prompt = new FakePromptService();
        var service = CreateService(new FakeEditorLocator(), prompt, process);

        var result = await service.LaunchAsync(
            "TestProject",
            Path.Combine(_projectPath, "missing"),
            "2022.3.62f1");

        Assert.False(result.Launched);
        Assert.Equal(0, prompt.PromptCount);
        Assert.Empty(process.Invocations);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_projectPath, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public async Task LaunchAsync_UsesManagedEditorPathForMatchingVersion()
    {
        var process = new FakeProcessLauncher();
        var prompt = new FakePromptService();
        var settingsStore = new FakeEditorSettingsStore(new UnityEditorSettingsDocument
        {
            Editors = new[]
            {
                new UnityEditorRecord
                {
                    Version = "2022.3.62f1",
                    ExecutablePath = Path.Combine(_projectPath, "missing-Unity.exe")
                },
                new UnityEditorRecord
                {
                    Version = "2022.3.62f1",
                    ExecutablePath = _executablePath
                }
            }
        });
        var service = ProjectLaunchServiceTestFactory.Create(
            new FakeEditorLocator(),
            settingsStore,
            prompt,
            process);

        var result = await service.LaunchAsync("TestProject", _projectPath, "2022.3.62f1");

        Assert.True(result.Launched);
        Assert.Equal(0, prompt.PromptCount);
        var invocation = Assert.Single(process.Invocations);
        Assert.Equal(_executablePath, invocation.ExecutablePath);
    }

    [Fact]
    public async Task LaunchAsync_UsesDefaultVersionWhenExactVersionIsMissing()
    {
        var process = new FakeProcessLauncher();
        var prompt = new FakePromptService();
        var settingsStore = new FakeEditorSettingsStore(new UnityEditorSettingsDocument
        {
            DefaultVersion = "2022.3.62f1",
            Editors = new[]
            {
                new UnityEditorRecord
                {
                    Version = "2022.3.62f1",
                    ExecutablePath = _executablePath
                }
            }
        });
        var service = ProjectLaunchServiceTestFactory.Create(
            new FakeEditorLocator(),
            settingsStore,
            prompt,
            process);

        var result = await service.LaunchAsync("TestProject", _projectPath, "6000.0.32f1");

        Assert.True(result.Launched);
        Assert.Contains("默认版本", result.Message);
        Assert.Equal(0, prompt.PromptCount);
        var invocation = Assert.Single(process.Invocations);
        Assert.Equal(_executablePath, invocation.ExecutablePath);
    }

    private static ProjectLaunchService CreateService(
        IUnityEditorLocator locator,
        FakePromptService prompt,
        FakeProcessLauncher process)
    {
        return ProjectLaunchServiceTestFactory.Create(
            locator,
            new FakeEditorSettingsStore(),
            prompt,
            process);
    }

    private static class ProjectLaunchServiceTestFactory
    {
        public static ProjectLaunchService Create(
            IUnityEditorLocator locator,
            IUnityEditorSettingsStore settingsStore,
            FakePromptService prompt,
            FakeProcessLauncher process)
        {
            return new ProjectLaunchService(
                locator,
                prompt,
                process,
                new FakeHubLocator(@"C:\Program Files\Unity Hub\Unity Hub.exe"),
                settingsStore);
        }
    }

    private sealed class FakeEditorSettingsStore : IUnityEditorSettingsStore
    {
        private readonly UnityEditorSettingsDocument _settings;

        public FakeEditorSettingsStore(UnityEditorSettingsDocument? settings = null)
        {
            _settings = settings ?? new UnityEditorSettingsDocument();
        }

        public Task<UnityEditorSettingsDocument> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_settings);
        }

        public Task SaveAsync(
            UnityEditorSettingsDocument settings,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEditorLocator : IUnityEditorLocator
    {
        private readonly IReadOnlyList<UnityEditorInstallation> _editors;

        public FakeEditorLocator(params UnityEditorInstallation[] editors)
        {
            _editors = editors;
        }

        public Task<IReadOnlyList<UnityEditorInstallation>> DiscoverAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_editors);
        }
    }

    private sealed class FakePromptService : IUnityLaunchPromptService
    {
        public int PromptCount { get; private set; }

        public UnityEditorMissingChoice NextChoice { get; set; } = UnityEditorMissingChoice.Cancel;

        public string? PickedEditor { get; set; }

        public UnityEditorMissingChoice AskForMissingEditor(
            string projectName,
            string projectPath,
            string unityVersion)
        {
            PromptCount++;
            return NextChoice;
        }

        public string? PickEditorExecutable() => PickedEditor;
    }

    private sealed class FakeHubLocator : IUnityHubLocator
    {
        private readonly string? _path;

        public FakeHubLocator(string? path)
        {
            _path = path;
        }

        public string? FindExecutable() => _path;
    }

    private sealed class FakeProcessLauncher : IExternalProcessLauncher
    {
        public List<ProcessInvocation> Invocations { get; } = new();

        public bool Start(string executablePath, IReadOnlyList<string> arguments, string? workingDirectory)
        {
            Invocations.Add(new ProcessInvocation(executablePath, arguments.ToArray()));
            return true;
        }
    }

    private sealed record ProcessInvocation(string ExecutablePath, IReadOnlyList<string> Arguments);
}
