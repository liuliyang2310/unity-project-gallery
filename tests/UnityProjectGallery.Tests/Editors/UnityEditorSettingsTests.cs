using System.IO;
using UnityProjectGallery.App.Services;
using UnityProjectGallery.App.ViewModels;
using UnityProjectGallery.Core.Editors;
using UnityProjectGallery.Infrastructure.Editors;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Tests.Editors;

public sealed class UnityEditorSettingsTests : IDisposable
{
    private readonly string _tempRoot;

    public UnityEditorSettingsTests()
    {
        _tempRoot = Path.Combine(
            Path.GetTempPath(),
            "UnityProjectGalleryEditorSettingsTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task JsonStore_RoundTripsMultiplePathsAndDefaultVersion()
    {
        var settingsPath = Path.Combine(_tempRoot, "unity-editors.json");
        var store = new JsonUnityEditorSettingsStore(settingsPath);
        var firstPath = CreateFakeUnityExecutable("2022.3.62f1", "First");
        var secondPath = CreateFakeUnityExecutable("2022.3.62f1", "Second");

        await store.SaveAsync(new UnityEditorSettingsDocument
        {
            DefaultVersion = "2022.3.62f1",
            Editors = new[]
            {
                new UnityEditorRecord
                {
                    Version = "2022.3.62f1",
                    ExecutablePath = firstPath,
                    AddedAt = DateTimeOffset.UtcNow
                },
                new UnityEditorRecord
                {
                    Version = "2022.3.62f1",
                    ExecutablePath = secondPath,
                    AddedAt = DateTimeOffset.UtcNow
                }
            }
        });

        var loaded = await store.LoadAsync();

        Assert.Equal("2022.3.62f1", loaded.DefaultVersion);
        Assert.Equal(2, loaded.Editors.Length);
        Assert.All(loaded.Editors, record => Assert.Equal("2022.3.62f1", record.Version));
        Assert.Contains(loaded.Editors, record => record.ExecutablePath == firstPath);
        Assert.Contains(loaded.Editors, record => record.ExecutablePath == secondPath);
        Assert.False(File.Exists(settingsPath + ".tmp"));
    }

    [Fact]
    public async Task JsonStore_ReturnsEmptySettingsForMissingOrCorruptFile()
    {
        var missingPath = Path.Combine(_tempRoot, "missing.json");
        var store = new JsonUnityEditorSettingsStore(missingPath);
        Assert.Empty((await store.LoadAsync()).Editors);

        var corruptPath = Path.Combine(_tempRoot, "corrupt.json");
        await File.WriteAllTextAsync(corruptPath, "{ not valid json");
        var corruptStore = new JsonUnityEditorSettingsStore(corruptPath);
        Assert.Empty((await corruptStore.LoadAsync()).Editors);
    }

    [Fact]
    public void VersionReader_FallsBackToInstallationDirectoryName()
    {
        var executablePath = CreateFakeUnityExecutable("2021.3.45f1", "Editor");
        var reader = new UnityEditorVersionReader();

        var version = reader.ReadVersion(executablePath);

        Assert.Equal("2021.3.45f1", version);
    }

    [Fact]
    public async Task SettingsViewModel_MergesManualAndDiscoveredEditorsAndSetsDefault()
    {
        var manualPath = CreateFakeUnityExecutable("2022.3.62f1", "Manual");
        var discoveredPath = CreateFakeUnityExecutable("6000.0.32f1", "Discovered");
        var store = new FakeSettingsStore(new UnityEditorSettingsDocument
        {
            Editors = new[]
            {
                new UnityEditorRecord
                {
                    Version = "2022.3.62f1",
                    ExecutablePath = manualPath
                }
            }
        });
        var viewModel = CreateViewModel(
            store,
            new FakeEditorLocator(new UnityEditorInstallation
            {
                Version = "6000.0.32f1",
                ExecutablePath = discoveredPath
            }),
            new FakeVersionReader(path =>
                path == manualPath ? "2022.3.62f1" : "6000.0.32f1"));
        await viewModel.InitializeAsync();

        Assert.Equal(2, viewModel.Editors.Count);
        Assert.Contains(viewModel.Editors, editor => editor.IsManual);
        Assert.Contains(viewModel.Editors, editor => !editor.IsManual);

        var manualEditor = Assert.Single(viewModel.Editors, editor => editor.IsManual);
        await viewModel.ToggleDefaultVersionAsync(manualEditor);

        Assert.True(manualEditor.IsDefaultVersion);
        Assert.Equal("2022.3.62f1", store.Saved!.DefaultVersion);
        Assert.Contains("2022.3.62f1", viewModel.DefaultVersionText);
    }

    [Fact]
    public async Task SettingsViewModel_AddsEditorAndRejectsDuplicatePath()
    {
        var existingPath = CreateFakeUnityExecutable("2021.3.45f1", "Existing");
        var newPath = CreateFakeUnityExecutable("2022.3.62f1", "New");
        var store = new FakeSettingsStore(new UnityEditorSettingsDocument
        {
            Editors = new[]
            {
                new UnityEditorRecord
                {
                    Version = "2021.3.45f1",
                    ExecutablePath = existingPath
                }
            }
        });
        var picker = new FakeFilePicker(newPath);
        var viewModel = CreateViewModel(
            store,
            new FakeEditorLocator(),
            new FakeVersionReader(_ => "2022.3.62f1"),
            picker);
        await viewModel.InitializeAsync();

        await viewModel.AddEditorAsync();

        var added = Assert.Single(
            viewModel.Editors,
            editor => editor.ExecutablePath == newPath);
        Assert.True(added.IsManual);
        Assert.Contains(store.Saved!.Editors, record => record.ExecutablePath == newPath);

        picker.NextPath = existingPath;
        await viewModel.AddEditorAsync();

        Assert.Contains("已存在", viewModel.StatusMessage);
    }

    [Fact]
    public async Task SettingsViewModel_RemovesMissingRecordAndClearsDefault()
    {
        var missingPath = Path.Combine(_tempRoot, "Missing", "Editor", "Unity.exe");
        var store = new FakeSettingsStore(new UnityEditorSettingsDocument
        {
            DefaultVersion = "2022.3.62f1",
            Editors = new[]
            {
                new UnityEditorRecord
                {
                    Version = "2022.3.62f1",
                    ExecutablePath = missingPath
                }
            }
        });
        var viewModel = CreateViewModel(
            store,
            new FakeEditorLocator(),
            new FakeVersionReader(_ => "2022.3.62f1"));
        await viewModel.InitializeAsync();

        var editor = Assert.Single(viewModel.Editors);
        Assert.True(editor.IsMissing);
        await viewModel.RemoveEditorAsync(editor);

        Assert.Empty(viewModel.Editors);
        Assert.Null(store.Saved!.DefaultVersion);
        Assert.Empty(store.Saved.Editors);
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

    private string CreateFakeUnityExecutable(string versionDirectory, string leafDirectory)
    {
        var directory = Path.Combine(_tempRoot, versionDirectory, leafDirectory);
        Directory.CreateDirectory(directory);
        var executablePath = Path.Combine(directory, "Unity.exe");
        File.WriteAllText(executablePath, "fake");
        return executablePath;
    }

    private static SettingsViewModel CreateViewModel(
        IUnityEditorSettingsStore store,
        IUnityEditorLocator locator,
        IUnityEditorVersionReader versionReader,
        IUnityEditorFilePicker? picker = null)
    {
        return new SettingsViewModel(
            store,
            locator,
            versionReader,
            picker ?? new FakeFilePicker(null),
            () => { });
    }

    private sealed class FakeSettingsStore : IUnityEditorSettingsStore
    {
        private readonly UnityEditorSettingsDocument _initial;

        public FakeSettingsStore(UnityEditorSettingsDocument initial)
        {
            _initial = initial;
        }

        public UnityEditorSettingsDocument? Saved { get; private set; }

        public Task<UnityEditorSettingsDocument> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Saved ?? _initial);
        }

        public Task SaveAsync(
            UnityEditorSettingsDocument settings,
            CancellationToken cancellationToken = default)
        {
            Saved = settings;
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

    private sealed class FakeVersionReader : IUnityEditorVersionReader
    {
        private readonly Func<string, string?> _read;

        public FakeVersionReader(Func<string, string?> read)
        {
            _read = read;
        }

        public string? ReadVersion(string executablePath) => _read(executablePath);
    }

    private sealed class FakeFilePicker : IUnityEditorFilePicker
    {
        public FakeFilePicker(string? nextPath)
        {
            NextPath = nextPath;
        }

        public string? NextPath { get; set; }

        public string? PickExecutable() => NextPath;
    }
}
