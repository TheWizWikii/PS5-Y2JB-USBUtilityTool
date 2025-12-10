using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.IO.Compression; // Importante para manejar archivos ZIP
using System.Management;     // Importante para listar unidades (requiere referencia)

namespace USBUtilityTool
{
    public partial class Form1 : Form
    {
        // (USA TU ENLACE REAL AQUÍ)
        private const string FIXED_DOWNLOAD_URL = "https://github.com/TheWizWikii/PS5-Stuff/releases/download/1/PS5.zip";
        public Form1()
        {
            InitializeComponent();
            // Carga las unidades al iniciar el formulario
            this.Load += Form1_Load;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Llamar a la función que llena el ComboBox
            LoadUSBDrives();
        }

        // =================================================================
        // FUNCIÓN DE UTILIDAD: Cargar Unidades USB
        // =================================================================
        private void LoadUSBDrives()
        {
            cmbUSBSelector.Items.Clear();
            try
            {
                // Usa DriveInfo para listar todas las unidades
                DriveInfo[] allDrives = DriveInfo.GetDrives();

                foreach (DriveInfo d in allDrives)
                {
                    // Filtra solo unidades extraíbles (USB) y que estén listas
                    if (d.DriveType == DriveType.Removable && d.IsReady)
                    {
                        // Añade solo la letra de la unidad (ej. "E:")
                        cmbUSBSelector.Items.Add(d.Name.Substring(0, 2));
                    }
                }

                if (cmbUSBSelector.Items.Count > 0)
                {
                    cmbUSBSelector.SelectedIndex = 0;
                    lblStatus.Text = "Unidades USB detectadas. Listo.";
                }
                else
                {
                    lblStatus.Text = "No se detectaron unidades extraíbles.";
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error al cargar unidades: {ex.Message}";
            }
        }

        // =================================================================
        // FUNCIÓN 1: FORMATO A EXFAT
        // (Requiere permisos de Administrador para funcionar)
        // =================================================================
        private void btnFormat_Click(object sender, EventArgs e)
        {
            string selectedDrive = cmbUSBSelector.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedDrive))
            {
                MessageBox.Show("Por favor, seleccione una unidad USB válida.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show($"ADVERTENCIA: ¿Desea formatear {selectedDrive} a exFAT? ¡SE PERDERÁN TODOS LOS DATOS!", "Confirmar Formato", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
            {
                lblStatus.Text = $"Iniciando formato de {selectedDrive}...";

                // Usamos Task.Run para no bloquear la interfaz de usuario (UI)
                Task.Run(() =>
                {
                    try
                    {
                        // Comando: format X: /FS:exFAT /Q /Y
                        string arguments = $"{selectedDrive} /FS:exFAT /Q /Y";

                        ProcessStartInfo startInfo = new ProcessStartInfo
                        {
                            FileName = "format.com",
                            Arguments = arguments,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };

                        using (Process process = Process.Start(startInfo))
                        {
                            process.WaitForExit();

                            // Usamos Invoke para actualizar la UI desde el hilo de fondo
                            this.Invoke((MethodInvoker)delegate
                            {
                                if (process.ExitCode == 0)
                                {
                                    lblStatus.Text = $"✅ Formato de {selectedDrive} completado exitosamente.";
                                }
                                else
                                {
                                    string errorOutput = process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
                                    lblStatus.Text = $"❌ Error al formatear (Código: {process.ExitCode}). ¿Ejecutó como Administrador? \n Detalle: {errorOutput.Trim()}";
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            lblStatus.Text = $"Error inesperado al formatear: {ex.Message}";
                        });
                    }
                });
            }
        }

        // =================================================================
        // FUNCIÓN 2: DESCARGA Y DESCOMPRESIÓN ZIP
        // =================================================================
        private async void btnDownload_Click(object sender, EventArgs e)
        {
            string selectedDrive = cmbUSBSelector.SelectedItem?.ToString();

            // Usamos la constante fija
            string url = FIXED_DOWNLOAD_URL;

            // --- 1. Verificaciones Iniciales ---
            if (string.IsNullOrEmpty(selectedDrive))
            {
                MessageBox.Show("Por favor, seleccione una unidad USB válida.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                MessageBox.Show("Error de configuración: La URL de descarga no es válida.", "Error Crítico", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // --- 2. Preparar Rutas Temporales ---
            string tempDirectory = Path.GetTempPath();
            string fileName;

            try
            {
                fileName = Path.GetFileName(new Uri(url).LocalPath);
            }
            catch (UriFormatException)
            {
                MessageBox.Show("La URL ingresada no es válida.", "Error de URL", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.IsNullOrEmpty(fileName))
            {
                fileName = "downloaded_archive.zip";
            }

            string tempZipPath = Path.Combine(tempDirectory, fileName);
            string destinationPath = selectedDrive;

            // Limpieza preventiva
            if (File.Exists(tempZipPath))
            {
                try { File.Delete(tempZipPath); } catch { /* Ignorar */ }
            }

            // Aseguramos que la barra de progreso se oculte si hay errores
            bool downloadSuccess = false;

            try
            {
                // --- 3. Descarga con Barra de Progreso ---

                // **MOSTRAR E INICIALIZAR BARRA**
                this.progressBarDownload.Value = 0;
                this.progressBarDownload.Visible = true;
                lblStatus.Text = $"Iniciando descarga de {fileName} a la carpeta temporal...";

                using (WebClient client = new WebClient())
                {
                    client.DownloadProgressChanged += (s, args) =>
                    {
                        // Actualización Asíncrona de UI: Barra y Label
                        this.Invoke((MethodInvoker)delegate
                        {
                            this.progressBarDownload.Value = args.ProgressPercentage;
                            lblStatus.Text = $"Descargando... {args.ProgressPercentage}% completado.";
                        });
                    };

                    await client.DownloadFileTaskAsync(new Uri(url), tempZipPath);

                    this.Invoke((MethodInvoker)delegate {
                        this.progressBarDownload.Value = 100; // Barra al 100%
                        lblStatus.Text = $"✅ Descarga completada. Iniciando descompresión...";
                    });
                }

                // --- 4. Descomprimir el Archivo ZIP (MISMA LÓGICA ROBUSTA) ---

                if (tempZipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    lblStatus.Text = "Iniciando descompresión manual (sobrescritura garantizada)...";

                    using (ZipArchive archive = ZipFile.OpenRead(tempZipPath))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            string destinationFile = Path.Combine(destinationPath, entry.FullName);

                            if (string.IsNullOrEmpty(entry.Name)) // Es un directorio
                            {
                                Directory.CreateDirectory(destinationFile);
                            }
                            else // Es un archivo
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));

                                // SOBRESCRITURA: Borrar antes de extraer
                                if (File.Exists(destinationFile))
                                {
                                    File.Delete(destinationFile);
                                }

                                entry.ExtractToFile(destinationFile);
                            }
                        }
                    }
                    lblStatus.Text = $"✅ Descompresión completada en la raíz de {selectedDrive}";
                    downloadSuccess = true; // Marcar éxito solo si todo salió bien
                }
                else
                {
                    // Manejo si el archivo no es ZIP
                    string finalFilePath = Path.Combine(destinationPath, fileName);
                    File.Move(tempZipPath, finalFilePath);
                    lblStatus.Text = $"⚠️ Advertencia: El archivo no es ZIP. Movido a la raíz de {selectedDrive} sin descomprimir.";
                    downloadSuccess = true; // El movimiento se considera éxito
                }
            }
            catch (WebException wex)
            {
                lblStatus.Text = $"❌ Error de red/HTTP: {wex.Message}";
            }
            catch (InvalidDataException)
            {
                lblStatus.Text = "❌ Error: El archivo descargado no es un archivo ZIP válido o está corrupto.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"❌ Error crítico durante la operación: {ex.Message}";
            }
            finally
            {
                // --- 5. Limpieza Final y Ocultar Barra ---

                // Ocultamos y reseteamos la barra en CUALQUIER caso (éxito o error)
                this.Invoke((MethodInvoker)delegate {
                    this.progressBarDownload.Visible = false;
                    this.progressBarDownload.Value = 0;
                });

                if (File.Exists(tempZipPath))
                {
                    try
                    {
                        File.Delete(tempZipPath);
                        if (downloadSuccess)
                        {
                            lblStatus.Text = $"✅ Tarea finalizada. Archivo temporal eliminado. USB listo.";
                        }
                    }
                    catch { /* Ignorar errores de limpieza de archivos bloqueados */ }
                }
                else if (downloadSuccess)
                {
                    lblStatus.Text = $"✅ Tarea finalizada. USB listo.";
                }
            }
        }

        // Opcional: Función para el botón Refrescar
        private void btnRefresh_Click(object sender, EventArgs e)
        {
            LoadUSBDrives();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://www.youtube.com/channel/UClh9mrZULUGRQf-2oqPnPaw");
        }
    }
}
