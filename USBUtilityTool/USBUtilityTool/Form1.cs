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

            // FIJAMOS LA URL A LA CONSTANTE EN LUGAR DE LEER EL TEXTBOX
            string url = FIXED_DOWNLOAD_URL;

            if (string.IsNullOrEmpty(selectedDrive)) // Solo necesitamos verificar la unidad
            {
                MessageBox.Show("Por favor, seleccione una unidad USB válida.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Si la URL fija no tiene el formato correcto, esto actuará como una verificación:
            if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                MessageBox.Show("Error de configuración: La URL de descarga no es válida.", "Error Crítico", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // --- 1. Preparar rutas ---
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

            // Asegurar la limpieza en caso de fallos anteriores
            if (File.Exists(tempZipPath))
            {
                try { File.Delete(tempZipPath); } catch { /* Ignorar */ }
            }

            try
            {
                // --- 2. Descargar el Archivo (Asíncrono) ---
                lblStatus.Text = $"Iniciando descarga de {fileName} a la carpeta temporal...";

                using (WebClient client = new WebClient())
                {
                    client.DownloadProgressChanged += (s, args) =>
                    {
                        // Actualiza el estado con el porcentaje de descarga
                        this.Invoke((MethodInvoker)delegate {
                            lblStatus.Text = $"Descargando... {args.ProgressPercentage}% completado.";
                        });
                    };

                    await client.DownloadFileTaskAsync(new Uri(url), tempZipPath);
                    lblStatus.Text = $"✅ Descarga completada. Iniciando descompresión...";
                }

                // --- 3. Descomprimir el Archivo ZIP ---

                if (tempZipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    lblStatus.Text = "Iniciando descompresión manual (sobrescritura garantizada)...";

                    // Usamos ZipFile.OpenRead y un bucle para manejar manualmente la sobrescritura.
                    using (ZipArchive archive = ZipFile.OpenRead(tempZipPath))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            // Ruta completa de destino (raíz del USB + ruta interna del ZIP)
                            string destinationFile = Path.Combine(destinationPath, entry.FullName);

                            // 1. Manejo de Directorios (Entries que terminan en / o tienen longitud 0)
                            if (string.IsNullOrEmpty(entry.Name)) // Es un directorio
                            {
                                // Aseguramos que la carpeta exista
                                Directory.CreateDirectory(destinationFile);
                            }
                            else // 2. Manejo de Archivos
                            {
                                // Aseguramos que la carpeta que contendrá el archivo exista
                                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));

                                // Si el archivo ya existe en el USB, lo borramos primero
                                if (File.Exists(destinationFile))
                                {
                                    File.Delete(destinationFile);
                                }

                                // Extraemos el archivo
                                entry.ExtractToFile(destinationFile);
                            }
                        }
                    }
                    lblStatus.Text = $"✅ Descompresión completada en la raíz de {selectedDrive}";
                }
                else
                {
                    // Si no es ZIP, simplemente lo movemos a la raíz del USB
                    string finalFilePath = Path.Combine(destinationPath, fileName);
                    File.Move(tempZipPath, finalFilePath);
                    lblStatus.Text = $"⚠️ Advertencia: El archivo no es ZIP. Movido a la raíz de {selectedDrive} sin descomprimir.";
                }

                // --- 4. Limpieza ---
                File.Delete(tempZipPath);
                lblStatus.Text = $"✅ Tarea finalizada. Archivo temporal eliminado. USB listo.";
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
                // Limpieza final, incluso si hay fallos
                if (File.Exists(tempZipPath))
                {
                    try { File.Delete(tempZipPath); } catch { /* Intenta limpiar */ }
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
