using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using UnityProjectGallery.App.Services;
using UnityProjectGallery.App.ViewModels;
using UnityProjectGallery.Infrastructure.Editors;
using UnityProjectGallery.Infrastructure.Media;
using UnityProjectGallery.Infrastructure.Persistence;
using UnityProjectGallery.Infrastructure.Scanning;

namespace UnityProjectGallery.App;

public partial class App : Application
{
    public App()
    {
        AppLog.Initialize();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var repository = new SqliteProjectRepository();
            var metadataStore = new ProjectMetadataJsonStore();
            var editorSettingsStore = new JsonUnityEditorSettingsStore();
            var editorLocator = new UnityEditorLocator();
            var editorVersionReader = new UnityEditorVersionReader();
            var editorFilePicker = new WindowsUnityEditorFilePicker();
            var scanner = new UnityProjectScanner();
            var mediaScanner = new ProjectMediaScanner();
            var folderPicker = new WindowsScanFolderPicker();
            var mediaViewer = new WpfMediaViewerService();
            var launchService = new ProjectLaunchService(
                editorLocator,
                new WpfUnityLaunchPromptService(),
                new WindowsProcessLauncher(),
                new WindowsUnityHubLocator(),
                editorSettingsStore);
            var mainViewModel = new MainViewModel(
                repository,
                scanner,
                mediaScanner,
                folderPicker,
                mediaViewer,
                launchService,
                new WindowsProjectPathPicker(),
                metadataStore,
                editorSettingsStore,
                editorLocator,
                editorVersionReader,
                editorFilePicker,
                new WindowsExplorerRevealService());
            await mainViewModel.InitializeAsync();

            var mainWindow = new MainWindow { DataContext = mainViewModel };
            MainWindow = mainWindow;
            mainWindow.Show();
            AppLog.Info("主窗口已显示");
        }
        catch (Exception exception)
        {
            AppLog.Error("应用启动失败", exception);
            ShowError(
                "Unity Project Gallery 启动失败",
                $"启动失败：{exception.Message}");
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLog.Info("应用退出");
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error("未处理的界面异常", e.Exception);
        e.Handled = true;
        ShowError(
            "Unity Project Gallery 错误",
            $"程序遇到未处理错误，已记录到日志。\n\n{e.Exception.Message}");
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        AppLog.Error("未处理的进程异常", e.ExceptionObject as Exception);
    }

    private static void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        AppLog.Error("未观察的后台任务异常", e.Exception);
        e.SetObserved();
    }

    private static void ShowError(string title, string message)
    {
        MessageBox.Show(
            $"{message}\n\n日志目录：{AppLog.LogDirectory}",
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
