using Microsoft.Data.SqlClient;
using System.Data;
using System.IO.Compression;

namespace SRAUMOAR.Servicios
{
    public interface IBackupService
    {
        Task<string> CreateBackupAsync();
        Task<byte[]> CreateBackupFileAsync();
        Task<List<BackupInfo>> GetBackupHistoryAsync();
        Task<bool> DeleteBackupAsync(string fileName);
    }

    public class BackupService : IBackupService
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly string _backupPath;

        public BackupService(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
            _backupPath = Path.Combine(_environment.WebRootPath, "backups");
            
            // Crear directorio si no existe
            if (!Directory.Exists(_backupPath))
            {
                Directory.CreateDirectory(_backupPath);
            }
        }

        public async Task<string> CreateBackupAsync()
        {
            var connectionString = _configuration.GetConnectionString("Conexion");
            var backupFileName = $"SRAUMOAR_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
            var backupPath = Path.Combine(_backupPath, backupFileName);

            try
            {
                // Verificar que la cadena de conexión existe
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new Exception("Cadena de conexión no configurada");
                }

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Verificar que la base de datos existe
                var checkDbCommand = new SqlCommand("SELECT COUNT(*) FROM sys.databases WHERE name = 'raumoar'", connection);
                var dbExists = (int)await checkDbCommand.ExecuteScalarAsync();
                
                if (dbExists == 0)
                {
                    throw new Exception("La base de datos 'raumoar' no existe");
                }

                // Crear el respaldo
                var backupCommand = new SqlCommand(
                    $"BACKUP DATABASE [raumoar] TO DISK = '{backupPath}' WITH FORMAT, INIT, NAME = 'SRAUMOAR Full Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10",
                    connection);

                await backupCommand.ExecuteNonQueryAsync();

                // Esperar un momento para que el archivo se escriba completamente
                await Task.Delay(2000);

                // Verificar que el archivo .bak se creó correctamente
                if (!File.Exists(backupPath))
                {
                    throw new Exception("El archivo de respaldo no se creó correctamente");
                }

                var fileInfo = new FileInfo(backupPath);
                if (fileInfo.Length == 0)
                {
                    throw new Exception("El archivo de respaldo está vacío");
                }

                // Comprimir el archivo
                var compressedFileName = backupFileName.Replace(".bak", ".zip");
                var compressedPath = Path.Combine(_backupPath, compressedFileName);
                
                using (var archive = ZipFile.Open(compressedPath, ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry(Path.GetFileName(backupPath));
                    using (var entryStream = entry.Open())
                    using (var fileStream = new FileStream(backupPath, FileMode.Open, FileAccess.Read))
                    {
                        await fileStream.CopyToAsync(entryStream);
                    }
                }

                // Verificar que el archivo comprimido se creó correctamente
                var compressedFileInfo = new FileInfo(compressedPath);
                if (compressedFileInfo.Length == 0)
                {
                    throw new Exception("El archivo comprimido está vacío");
                }

                // Eliminar el archivo .bak original
                File.Delete(backupPath);

                // Guardar información del respaldo
                await SaveBackupInfoAsync(compressedFileName, compressedPath);

                return compressedFileName;
            }
            catch (Exception ex)
            {
                // Limpiar archivos en caso de error
                if (File.Exists(backupPath))
                {
                    try { File.Delete(backupPath); } catch { }
                }
                throw new Exception($"Error al crear el respaldo: {ex.Message}", ex);
            }
        }

        public async Task<byte[]> CreateBackupFileAsync()
        {
            var fileName = await CreateBackupAsync();
            var filePath = Path.Combine(_backupPath, fileName);
            return await File.ReadAllBytesAsync(filePath);
        }

        public async Task<List<BackupInfo>> GetBackupHistoryAsync()
        {
            var backupFiles = Directory.GetFiles(_backupPath, "*.zip")
                .Select(file => new FileInfo(file))
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            var backupHistory = new List<BackupInfo>();

            foreach (var file in backupFiles)
            {
                var backupInfo = new BackupInfo
                {
                    FileName = file.Name,
                    FilePath = file.FullName,
                    Size = file.Length,
                    CreatedDate = file.CreationTime,
                    SizeFormatted = FormatFileSize(file.Length)
                };

                backupHistory.Add(backupInfo);
            }

            return await Task.FromResult(backupHistory);
        }

        public async Task<bool> DeleteBackupAsync(string fileName)
        {
            try
            {
                var filePath = Path.Combine(_backupPath, fileName);
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

        private async Task SaveBackupInfoAsync(string fileName, string filePath)
        {
            var infoFile = Path.Combine(_backupPath, "backup_info.txt");
            var info = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}|{fileName}|{new FileInfo(filePath).Length}\n";
            await File.AppendAllTextAsync(infoFile, info);
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
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
}
