using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using UnityProjectGallery.Core.Abstractions;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Infrastructure.Persistence;

/// <summary>
/// 使用 SQLite 保存工程快照与扫描源。工程完整状态以 JSON 快照存放，
/// 同时保留独立列用于路径、版本与收藏等基础信息，数据库操作全部在线程池执行。
/// </summary>
public sealed class SqliteProjectRepository : IProjectRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _databasePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public SqliteProjectRepository(string? databasePath = null)
    {
        _databasePath = string.IsNullOrWhiteSpace(databasePath)
            ? GetDefaultDatabasePath()
            : databasePath;
    }

    public string DatabasePath => _databasePath;

    public async Task<IReadOnlyList<UnityProject>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(ReadProjects, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveProjectAsync(
        UnityProject project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await Task.Run(() => WriteProject(project), cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<string>> GetScanRootsAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(ReadScanRoots, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveScanRootsAsync(
        IReadOnlyList<string> scanRoots,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scanRoots);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await Task.Run(() => WriteScanRoots(scanRoots), cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private IReadOnlyList<UnityProject> ReadProjects()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT StateJson FROM Projects ORDER BY ProjectPath;";

        var projects = new List<UnityProject>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var json = reader.GetString(0);
            try
            {
                var project = JsonSerializer.Deserialize<UnityProject>(json, JsonOptions);
                if (project is not null)
                {
                    projects.Add(project);
                }
            }
            catch (JsonException)
            {
                // 单条快照损坏时跳过，其余工程仍可正常加载。
            }
        }

        return projects;
    }

    private void WriteProject(UnityProject project)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Projects
                (Id, ProjectPath, Name, UnityVersion, IsFavorite, LastOpenedTime, UpdatedAt, StateJson)
            VALUES
                ($id, $path, $name, $version, $favorite, $lastOpened, $updatedAt, $state)
            ON CONFLICT(Id) DO UPDATE SET
                ProjectPath = excluded.ProjectPath,
                Name = excluded.Name,
                UnityVersion = excluded.UnityVersion,
                IsFavorite = excluded.IsFavorite,
                LastOpenedTime = excluded.LastOpenedTime,
                UpdatedAt = excluded.UpdatedAt,
                StateJson = excluded.StateJson;
            """;

        command.Parameters.AddWithValue("$id", project.Id);
        command.Parameters.AddWithValue("$path", project.ProjectPath);
        command.Parameters.AddWithValue("$name", project.Name);
        command.Parameters.AddWithValue("$version", project.UnityVersion);
        command.Parameters.AddWithValue("$favorite", project.IsFavorite ? 1 : 0);
        command.Parameters.AddWithValue(
            "$lastOpened",
            project.LastOpenedTime?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$state", JsonSerializer.Serialize(project));

        command.ExecuteNonQuery();
    }

    private IReadOnlyList<string> ReadScanRoots()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Path FROM ScanRoots ORDER BY AddedAt, Path;";

        var roots = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            roots.Add(reader.GetString(0));
        }

        return roots;
    }

    private void WriteScanRoots(IReadOnlyList<string> scanRoots)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var deleteCommand = connection.CreateCommand();
        deleteCommand.Transaction = transaction;
        deleteCommand.CommandText = "DELETE FROM ScanRoots;";
        deleteCommand.ExecuteNonQuery();

        foreach (var root in scanRoots
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
                "INSERT INTO ScanRoots (Path, AddedAt) VALUES ($path, $addedAt);";
            insertCommand.Parameters.AddWithValue("$path", root);
            insertCommand.Parameters.AddWithValue("$addedAt", DateTimeOffset.UtcNow.ToString("O"));
            insertCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private SqliteConnection OpenConnection()
    {
        EnsureInitialized();

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString());
        connection.Open();
        return connection;
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString());
        connection.Open();

        using (var pragmaCommand = connection.CreateCommand())
        {
            pragmaCommand.CommandText = "PRAGMA journal_mode=WAL;";
            pragmaCommand.ExecuteNonQuery();
        }

        using (var schemaCommand = connection.CreateCommand())
        {
            schemaCommand.CommandText = """
                CREATE TABLE IF NOT EXISTS Projects (
                    Id TEXT PRIMARY KEY NOT NULL,
                    ProjectPath TEXT NOT NULL COLLATE NOCASE,
                    Name TEXT NOT NULL,
                    UnityVersion TEXT NOT NULL,
                    IsFavorite INTEGER NOT NULL DEFAULT 0,
                    LastOpenedTime TEXT NULL,
                    UpdatedAt TEXT NOT NULL,
                    StateJson TEXT NOT NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS IX_Projects_ProjectPath
                    ON Projects (ProjectPath COLLATE NOCASE);

                CREATE TABLE IF NOT EXISTS ScanRoots (
                    Path TEXT PRIMARY KEY NOT NULL COLLATE NOCASE,
                    AddedAt TEXT NOT NULL
                );
                """;
            schemaCommand.ExecuteNonQuery();
        }

        _initialized = true;
    }

    private static string GetDefaultDatabasePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UnityProjectGallery",
            "unity-project-gallery.db");
    }
}
