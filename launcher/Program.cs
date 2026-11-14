using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using SpotifyOverlayShared;

namespace SpotifyOverlayLauncher
{
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (Array.Exists(args, a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
                return Uninstall(args);

            bool createdNew;
            using (var mutex = new Mutex(true, @"Local\SpotifyLocalStreamlabsLauncher", out createdNew))
            {
                if (!createdNew)
                {
                    // Ya está abierto: abrir la página de configuración y salir.
                    if (InstallInfo.IsPortInUse()) TrayApp.OpenUrl(InstallInfo.ConfigUrl);
                    else MessageBox.Show(
                        "Spotify Local para Streamlabs ya está en marcha.\n\nBusca el icono ♫ en la bandeja del sistema (junto al reloj).",
                        InstallInfo.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }

                bool showWelcome = Array.Exists(args, a => a.Equals("--welcome", StringComparison.OrdinalIgnoreCase));
                using (var app = new TrayApp(showWelcome))
                    Application.Run();
                return 0;
            }
        }

        /// Desinstalación desde "Aplicaciones instaladas" de Windows.
        static int Uninstall(string[] args)
        {
            bool quiet = Array.Exists(args, a => a.Equals("--quiet", StringComparison.OrdinalIgnoreCase));
            if (!quiet && MessageBox.Show(
                    "¿Quieres desinstalar " + InstallInfo.AppName + "?\n\nSe eliminarán el servidor, el lanzador y la configuración.\nLa fuente de navegador que añadiste en Streamlabs no se toca (puedes borrarla a mano).",
                    "Desinstalar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return 1;

            try
            {
                InstallInfo.StopOurProcesses();
                InstallInfo.RemoveRegistration();

                // No podemos borrarnos a nosotros mismos mientras corremos: lo hace
                // un cmd oculto un par de segundos después de que salgamos.
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"" + InstallInfo.InstallDir + "\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = Path.GetTempPath()
                });

                if (!quiet)
                    MessageBox.Show(InstallInfo.AppName + " se ha desinstalado correctamente.", "Desinstalar",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al desinstalar:\n\n" + ex.Message, "Desinstalar", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 2;
            }
        }
    }
}
