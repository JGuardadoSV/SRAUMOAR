using Microsoft.Data.SqlClient;
using System.IO.Compression;

namespace SRAUMOAR.Servicios
{
    public interface IBackupService
    {
        string GetDatabaseName();
        string? GetStorageConfigurationError();
        Task<string> CreateBackupAsync();
        Task<byte[]> CreateBackupFileAsync();
        Task<BackupDownloadResult> CreateRawBackupFileAsync();
        Task<List<BackupInfo>> GetBackupHistoryAsync();
        Task<bool> DeleteBackupAsync(string fileName);
    }

    public class BackupService : IBackupService
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly string _connectionString;
        private readonly SqlConnectionStringBuilder _connectionBuilder;
        private readonly string _databaseName;
        private readonly string? _configuredSqlBackupPath;
        private readonly string? _configuredAppAccessPath;

        public BackupService(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
            _connectionString = _configuration.GetConnectionString("Conexion")
                ?? throw new InvalidOperationException("Cadena de conexión 'Conexion' no configurada");
            _connectionBuilder = new SqlConnectionStringBuilder(_connectionString);
            _databaseName = _connectionBuilder.InitialCatalog;

            if (string.IsNullOrWhiteSpace(_databaseName))
            {
                throw new InvalidOperationException("La cadena de conexión no contiene el nombre de la base de datos");
            }

            _configuredSqlBackupPath = _configuration["BackupSettings:SqlBackupPath"];
            _configuredAppAccessPath = _configuration["BackupSettings:AppAccessPath"];
        }

        public string GetDatabaseName() => _databaseName;

        public string? GetStorageConfigurationError()
        {
            if (IsLocalSqlServer(_connectionBuilder.DataSource))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(_configuredSqlBackupPath) && !string.IsNullOrWhiteSpace(_configuredAppAccessPath))
            {
                return null;
            }

            return $"El servidor SQL '{_connectionBuilder.DataSource}' es remoto. Configure 'BackupSettings:SqlBackupPath' para la ruta que usará SQL Server y 'BackupSettings:AppAccessPath' para la ruta accesible desde la aplicación o el contenedor Docker.";
        }

        public async Task<string> CreateBackupAsync()
        {
            BackupDownloadResult? rawBackup = null;
            string? compressedPath = null;

            try
            {
                rawBackup = await CreateRawBackupInternalAsync();
                var compressedFileName = Path.ChangeExtension(rawBackup.FileName, ".zip")!;
                compressedPath = Path.Combine(rawBackup.DirectoryPath, compressedFileName);

                using (var archive = ZipFile.Open(compressedPath, ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry(rawBackup.FileName, CompressionLevel.Optimal);
                    await using var entryStream = entry.Open();
                    await using var fileStream = new FileStream(rawBackup.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    await fileStream.CopyToAsync(entryStream);
                }

                var compressedFileInfo = new FileInfo(compressedPath);
                if (!compressedFileInfo.Exists || compressedFileInfo.Length == 0)
                {
                    throw new Exception("El archivo comprimido no se creó correctamente");
                }

                TryDeleteFile(rawBackup.FilePath);
                await SaveBackupInfoAsync(compressedFileName, compressedPath);

                return compressedFileName;
            }
            catch (Exception ex)
            {
                if (rawBackup is not null)
                {
                    TryDeleteFile(rawBackup.FilePath);
                }

                if (!string.IsNullOrWhiteSpace(compressedPath))
                {
                    TryDeleteFile(compressedPath);
                }

                throw new Exception($"Error al crear el respaldo: {ex.Message}", ex);
            }
        }

        public async Task<byte[]> CreateBackupFileAsync()
        {
            var storagePath = ResolveAppStoragePath();
            var fileName = await CreateBackupAsync();
            var filePath = Path.Combine(storagePath, fileName);
            return await File.ReadAllBytesAsync(filePath);
        }

        public async Task<BackupDownloadResult> CreateRawBackupFileAsync()
        {
            BackupDownloadResult? rawBackup = null;

            try
            {
                rawBackup = await CreateRawBackupInternalAsync();
                var fileBytes = await File.ReadAllBytesAsync(rawBackup.FilePath);

                return new BackupDownloadResult
                {
                    FileName = rawBackup.FileName,
                    FilePath = rawBackup.FilePath,
                    DirectoryPath = rawBackup.DirectoryPath,
                    ContentType = "application/octet-stream",
                    FileBytes = fileBytes
                };
            }
            catch (Exception ex)
            {
                if (rawBackup is not null)
                {
                    TryDeleteFile(rawBackup.FilePath);
                }

                throw new Exception($"Error al crear el respaldo .bak: {ex.Message}", ex);
            }
            finally
            {
                if (rawBackup is not null)
                {
                    TryDeleteFile(rawBackup.FilePath);
                }
            }
        }

        public async Task<List<BackupInfo>> GetBackupHistoryAsync()
        {
            var storagePath = ResolveAppStoragePath(throwIfMissing: false);
            if (string.IsNullOrWhiteSpace(storagePath) || !Directory.Exists(storagePath))
            {
                return new List<BackupInfo>();
            }

            var backupFiles = Directory.GetFiles(storagePath, "*.zip")
                .Select(file => new FileInfo(file))
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            var backupHistory = backupFiles.Select(file => new BackupInfo
            {
                FileName = file.Name,
                FilePath = file.FullName,
                Size = file.Length,
                CreatedDate = file.CreationTime,
                SizeFormatted = FormatFileSize(file.Length)
            }).ToList();

            return await Task.FromResult(backupHistory);
        }

        public async Task<bool> DeleteBackupAsync(string fileName)
        {
            try
            {
                var safeFileName = Path.GetFileName(fileName);
                var storagePath = ResolveAppStoragePath(throwIfMissing: false);
                if (string.IsNullOrWhiteSpace(storagePath))
                {
                    return false;
                }

                var filePath = Path.Combine(storagePath, safeFileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return await Task.FromResult(true);
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private async Task<BackupDownloadResult> CreateRawBackupInternalAsync()
        {
            var sqlBackupDirectory = ResolveSqlBackupDirectory();
            var appStoragePath = ResolveAppStoragePath();
            EnsureStorageDirectoryExists(appStoragePath);

            var backupFileName = $"{SanitizeFileName(_databaseName)}_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
            var sqlBackupPath = CombineSqlPath(sqlBackupDirectory, backupFileName);
            var appBackupPath = Path.Combine(appStoragePath, backupFileName);

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                await ValidateDatabaseExistsAsync(connection);

                var escapedDatabaseName = EscapeSqlIdentifier(_databaseName);
                var escapedBackupPath = sqlBackupPath.Replace("'", "''");
                var commandText = $@"
BACKUP DATABASE [{escapedDatabaseName}]
TO DISK = N'{escapedBackupPath}'
WITH FORMAT, INIT, NAME = N'{escapedDatabaseName} Full Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10;";

                using var backupCommand = new SqlCommand(commandText, connection)
                {
                    CommandTimeout = 0
                };

                await backupCommand.ExecuteNonQueryAsync();
                await WaitForFileAsync(appBackupPath);

                return new BackupDownloadResult
                {
                    FileName = backupFileName,
                    FilePath = appBackupPath,
                    DirectoryPath = appStoragePath,
                    ContentType = "application/octet-stream"
                };
            }
            catch (Exception ex)
            {
                TryDeleteFile(appBackupPath);
                throw new Exception($"Error al generar el archivo de respaldo: {ex.Message}", ex);
            }
        }

        private async Task ValidateDatabaseExistsAsync(SqlConnection connection)
        {
            using var checkDbCommand = new SqlCommand("SELECT COUNT(*) FROM sys.databases WHERE name = @databaseName", connection);
            checkDbCommand.Parameters.AddWithValue("@databaseName", _databaseName);
            var dbExists = Convert.ToInt32(await checkDbCommand.ExecuteScalarAsync());

            if (dbExists == 0)
            {
                throw new Exception($"La base de datos '{_databaseName}' no existe");
            }
        }

        private string? ResolveSqlBackupDirectory(bool throwIfMissing = true)
        {
            if (!string.IsNullOrWhiteSpace(_configuredSqlBackupPath))
            {
                return _configuredSqlBackupPath;
            }

            if (IsLocalSqlServer(_connectionBuilder.DataSource))
            {
                return Path.Combine(_environment.WebRootPath, "backups");
            }

            if (throwIfMissing)
            {
                throw new InvalidOperationException(GetStorageConfigurationError());
            }

            return null;
        }

        private string? ResolveAppStoragePath(bool throwIfMissing = true)
        {
            if (!string.IsNullOrWhiteSpace(_configuredAppAccessPath))
            {
                return Path.GetFullPath(_configuredAppAccessPath);
            }

            if (IsLocalSqlServer(_connectionBuilder.DataSource))
            {
                return Path.Combine(_environment.WebRootPath, "backups");
            }

            if (throwIfMissing)
            {
                throw new InvalidOperationException(GetStorageConfigurationError());
            }

            return null;
        }

        private static string CombineSqlPath(string directory, string fileName)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return fileName;
            }

            var separator = directory.Contains('\\') ? "\\" : "/";
            return $"{directory.TrimEnd('\\', '/')}" + separator + fileName;
        }

        private static void EnsureStorageDirectoryExists(string storagePath)
        {
            if (!Directory.Exists(storagePath))
            {
                Directory.CreateDirectory(storagePath);
            }
        }

        private static bool IsLocalSqlServer(string? dataSource)
        {
            if (string.IsNullOrWhiteSpace(dataSource))
            {
                return false;
            }

            var normalizedSource = dataSource.Trim().ToLowerInvariant();
            var machineName = Environment.MachineName.ToLowerInvariant();

            return normalizedSource is "." or "(local)" or "localhost" or "127.0.0.1" or "::1"
                || normalizedSource.StartsWith("(localdb)")
                || normalizedSource.StartsWith($@"{machineName}\")
                || normalizedSource == machineName;
        }

        private static string EscapeSqlIdentifier(string name) => name.Replace("]", "]]",
            StringComparison.Ordinal);

        private static string SanitizeFileName(string value)
        {
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalidChar, '_');
            }

            return value;
        }

        private static async Task WaitForFileAsync(string filePath)
        {
            const int maxAttempts = 20;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length > 0)
                    {
                        return;
                    }
                }

                await Task.Delay(500);
            }

            throw new Exception("El archivo de respaldo no se creó correctamente o no es accesible desde la aplicación");
        }

        private async Task SaveBackupInfoAsync(string fileName, string filePath)
        {
            var storagePath = ResolveAppStoragePath()!;
            var infoFile = Path.Combine(storagePath, "backup_info.txt");
            var info = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}|{fileName}|{new FileInfo(filePath).Length}{Environment.NewLine}";
            await File.AppendAllTextAsync(infoFile, info);
        }

        private static void TryDeleteFile(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            var order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    public class BackupInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long Size { get; set; }
        public string SizeFormatted { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }

    public class BackupDownloadResult
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string DirectoryPath { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public byte[]? FileBytes { get; set; }
    }
}
