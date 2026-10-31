using System.IO;
using System.Threading;
using UnityProjectGallery.App.Services;
using UnityProjectGallery.App.ViewModels;
using UnityProjectGallery.Core.Models;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Tests.ModelPreview;

public sealed class AssetPreviewViewModelTests : IDisposable
{
    private readonly List<string> _tempDirectories = new();

    [Fact]
    public void RevealAssetFolderCommand_OpensSelectedAssetFolder()
    {
        var root = CreateTempDirectory();
        var filePath = Path.Combine(root, "test.wav");
        File.WriteAllText(filePath, string.Empty);

        Exception? failure = null;
        string? revealedFolder = null;
        string? status = null;
        RunOnSta(() =>
        {
            var viewModel = new AssetPreviewViewModel(
                new StubScanner(),
                new StubLoader(),
                new StubMediaViewer(),
                new FakeRevealService(path => revealedFolder = path),
                "TestProject",
                root);
            viewModel.SelectedAsset = CreateAudioAsset(filePath);
            viewModel.RevealAssetFolderCommand.Execute(null);
            status = viewModel.StatusMessage;
            viewModel.Dispose();
        }, exception => failure = exception);

        Assert.Null(failure);
        Assert.Equal(root, revealedFolder);
        Assert.NotNull(status);
        Assert.Contains("资源管理器", status);
    }

    [Fact]
    public void RevealAssetFolderCommand_ReportsMissingFolder()
    {
        var root = CreateTempDirectory();
        var filePath = Path.Combine(root, "missing", "test.wav");

        Exception? failure = null;
        string? revealedFolder = null;
        string? status = null;
        RunOnSta(() =>
        {
            var viewModel = new AssetPreviewViewModel(
                new StubScanner(),
                new StubLoader(),
                new StubMediaViewer(),
                new FakeRevealService(path => revealedFolder = path),
                "TestProject",
                root);
            viewModel.SelectedAsset = CreateAudioAsset(filePath);
            viewModel.RevealAssetFolderCommand.Execute(null);
            status = viewModel.StatusMessage;
            viewModel.Dispose();
        }, exception => failure = exception);

        Assert.Null(failure);
        Assert.Null(revealedFolder);
        Assert.NotNull(status);
        Assert.Contains("不存在", status);
    }

    public void Dispose()
    {
        foreach (var directory in _tempDirectories)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static AssetPreviewItemViewModel CreateAudioAsset(string filePath)
    {
        return new AssetPreviewItemViewModel(new AssetPreviewInfo(
            filePath,
            Path.Combine("Audio", Path.GetFileName(filePath)),
            Path.GetFileName(filePath),
            AssetPreviewKind.Audio,
            0,
            DateTimeOffset.Now));
    }

    private string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "UnityProjectGalleryAssetPreviewTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }

    private static void RunOnSta(Action action, Action<Exception> onError)
    {
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                onError(exception);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    private sealed class StubScanner : IAssetsPreviewScanner
    {
        public Task<IReadOnlyList<AssetPreviewInfo>> ScanAsync(
            string projectPath,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AssetPreviewInfo>>(
                Array.Empty<AssetPreviewInfo>());
        }
    }

    private sealed class StubLoader : IModelPreviewLoader
    {
        public Task<ModelPreviewResult> LoadAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ModelPreviewResult(null, "未使用模型加载器。"));
        }
    }

    private sealed class StubMediaViewer : IMediaViewerService
    {
        public void ShowImage(string filePath, string title)
        {
        }

        public void ShowVideo(string filePath, string title)
        {
        }
    }

    private sealed class FakeRevealService : IProjectFolderRevealService
    {
        private readonly Action<string> _onReveal;

        public FakeRevealService(Action<string> onReveal)
        {
            _onReveal = onReveal;
        }

        public bool Reveal(string folderPath)
        {
            _onReveal(folderPath);
            return true;
        }
    }
}
