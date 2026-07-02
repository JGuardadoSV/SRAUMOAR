using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SRAUMOAR.Servicios;

namespace SRAUMOAR.Pages.administracion
{
    [Authorize(Roles = "Administrador")]
    public class RespaldosModel : PageModel
    {
        private readonly IBackupService _backupService;

        public RespaldosModel(IBackupService backupService)
        {
            _backupService = backupService;
        }

        public List<BackupInfo> BackupHistory { get; set; } = new();
        public string DatabaseName { get; private set; } = string.Empty;
        public string? ConfigurationWarning { get; private set; }

        public async Task OnGetAsync()
        {
            DatabaseName = _backupService.GetDatabaseName();
            ConfigurationWarning = _backupService.GetStorageConfigurationError();
            BackupHistory = await _backupService.GetBackupHistoryAsync();
        }



        public async Task<IActionResult> OnPostCreateBackupAsync()
        {
            try
            {
                TempData["InfoMessage"] = "Iniciando creación de respaldo...";

                if (_backupService == null)
                {
                    TempData["ErrorMessage"] = "Error: Servicio de respaldos no disponible";
                    return RedirectToPage();
                }

                TempData["InfoMessage"] = "Conectando a la base de datos...";
                var fileName = await _backupService.CreateBackupAsync();
                TempData["SuccessMessage"] = $"Respaldo creado exitosamente: {fileName}";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error al crear el respaldo: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error completo: {ex}");
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDownloadBakAsync()
        {
            try
            {
                if (_backupService == null)
                {
                    TempData["ErrorMessage"] = "Error: Servicio de respaldos no disponible";
                    return RedirectToPage();
                }

                var backupFile = await _backupService.CreateRawBackupFileAsync();
                if (backupFile.FileBytes == null || backupFile.FileBytes.Length == 0)
                {
                    TempData["ErrorMessage"] = "No se pudo generar el archivo .bak";
                    return RedirectToPage();
                }

                return File(backupFile.FileBytes, backupFile.ContentType, backupFile.FileName);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error al descargar el respaldo .bak: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error completo: {ex}");
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostDeleteBackupAsync(string fileName)
        {
            try
            {
                var success = await _backupService.DeleteBackupAsync(fileName);
                if (success)
                {
                    TempData["SuccessMessage"] = $"Respaldo eliminado exitosamente: {fileName}";
                }
                else
                {
                    TempData["ErrorMessage"] = "No se pudo eliminar el respaldo";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error al eliminar el respaldo: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetDownloadAsync(string fileName)
        {
            try
            {
                var backupHistory = await _backupService.GetBackupHistoryAsync();
                var backup = backupHistory.FirstOrDefault(b => b.FileName == fileName);

                if (backup == null)
                {
                    TempData["ErrorMessage"] = "El archivo de respaldo no existe";
                    return RedirectToPage();
                }

                var fileBytes = await System.IO.File.ReadAllBytesAsync(backup.FilePath);
                return File(fileBytes, "application/zip", fileName);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error al descargar el respaldo: {ex.Message}";
                return RedirectToPage();
            }
        }
    }
}
