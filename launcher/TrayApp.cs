using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
using SpotifyOverlayShared;

namespace SpotifyOverlayLauncher
{
    /// Icono de bandeja que arranca/para SpotifyOverlayServer.exe.
    ///
    /// Modo "Solo con Streamlabs" (por defecto): imita al plugin de OBS. El servidor
    /// se enciende cuando se abre Streamlabs Desktop y se apaga cuando se cierra.
    /// Modo "Siempre": el servidor está encendido mientras el lanzador esté abierto
    /// (útil para OBS, navegadores u otros programas de streaming).
    internal sealed class TrayApp : IDisposable
    {
        enum Mode { Streamlabs, Always }

        readonly NotifyIcon tray;
        readonly Timer timer;
        readonly ToolStripMenuItem statusItem;
        readonly ToolStripMenuItem modeStreamlabsItem;
        readonly ToolStripMenuItem modeAlwaysItem;
        readonly ToolStripMenuItem autostartItem;

        Process server;
        Mode mode;
        DateTime keepAliveUntil = DateTime.MinValue; // para abrir /config con Streamlabs cerrado
        int quickExits;                               // reinicios seguidos (evita bucles)
        DateTime lastStart = DateTime.MinValue;

        public TrayApp(bool showWelcome)
        {
            mode = LoadMode();

            statusItem = new ToolStripMenuItem("Iniciando...") { Enabled = false };
            modeStreamlabsItem = new ToolStripMenuItem("Solo cuando Streamlabs está abierto", null, (s, e) => SetMode(Mode.Streamlabs));
            modeAlwaysItem = new ToolStripMenuItem("Siempre activo", null, (s, e) => SetMode(Mode.Always));
            autostartItem = new ToolStripMenuItem("Iniciar con Windows", null, (s, e) => ToggleAutostart());

            var modeMenu = new ToolStripMenuItem("Modo");
            modeMenu.DropDownItems.Add(modeStreamlabsItem);
            modeMenu.DropDownItems.Add(modeAlwaysItem);

            var menu = new ContextMenuStrip();
            menu.Items.Add(statusItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("🎨 Cambiar aspecto del widget...", null, (s, e) => OpenConfig()) { Font = new Font(SystemFonts.MenuFont, FontStyle.Bold) });
            menu.Items.Add(new ToolStripMenuItem("📋 Copiar URL del widget", null, (s, e) => CopyUrl()));
            menu.Items.Add(new ToolStripMenuItem("❓ Cómo añadirlo a Streamlabs", null, (s, e) => ShowHelp()));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(modeMenu);
            menu.Items.Add(autostartItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Salir", null, (s, e) => Exit()));
            menu.Opening += (s, e) => RefreshMenu();

            Icon icon;
            try { icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application; }
            catch { icon = SystemIcons.Application; }

            tray = new NotifyIcon
            {
                Icon = icon,
                Text = "Spotify Local para Streamlabs",
                ContextMenuStrip = menu,
                Visible = true
            };
            tray.DoubleClick += (s, e) => OpenConfig();

            timer = new Timer { Interval = 3000 };
            timer.Tick += (s, e) => Tick();
            timer.Start();

            SystemEvents.SessionEnding += (s, e) => StopServer();

            Tick();
            RefreshMenu();

            if (showWelcome)
            {
                tray.ShowBalloonTip(6000, "Spotify Local para Streamlabs",
                    mode == Mode.Streamlabs
                        ? "Listo. El widget se activará solo al abrir Streamlabs.\nURL: " + InstallInfo.WidgetUrl
                        : "Servidor activo en " + InstallInfo.WidgetUrl,
                    ToolTipIcon.Info);
            }
        }

        // ---------------- Lógica principal ----------------

        bool ShouldRun()
        {
            if (mode == Mode.Always) return true;
            if (DateTime.Now < keepAliveUntil) return true;
            return InstallInfo.IsStreamlabsRunning();
        }

        bool ServerRunning => server != null && !server.HasExited;

        void Tick()
        {
            try
            {
                if (ShouldRun())
                {
                    if (!ServerRunning) StartServer();
                }
                else if (ServerRunning)
                {
                    StopServer();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            UpdateStatusText();
        }

        void StartServer()
        {
            if (server != null)
            {
                // Si el servidor anterior murió casi al instante, contamos el fallo
                if ((DateTime.Now - lastStart).TotalSeconds < 10) quickExits++;
                else quickExits = 0;
                server.Dispose();
                server = null;
            }

            // Otro servidor (p. ej. la versión para OBS) ya ocupa el puerto: el
            // widget funciona igual con él, así que no lanzamos otro.
            if (InstallInfo.IsPortInUse()) return;

            if (quickExits >= 5)
            {
                // Esperar un poco antes de reintentar para no entrar en bucle
                if ((DateTime.Now - lastStart).TotalSeconds < 60) return;
                quickExits = 0;
            }

            string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
            string serverPath = Path.Combine(exeDir, InstallInfo.ServerExeName);
            if (!File.Exists(serverPath))
            {
                statusItem.Text = "⚠ No se encuentra " + InstallInfo.ServerExeName;
                return;
            }

            lastStart = DateTime.Now;
            server = Process.Start(new ProcessStartInfo
            {
                FileName = serverPath,
                Arguments = "--parent-pid " + Process.GetCurrentProcess().Id + " --config-dir \"" + exeDir.TrimEnd('\\') + "\"",
                WorkingDirectory = exeDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }

        void StopServer()
        {
            if (server == null) return;
            try
            {
                if (!server.HasExited)
                {
                    server.Kill();
                    server.WaitForExit(2000);
                }
            }
            catch { }
            server.Dispose();
            server = null;
            quickExits = 0;
        }

        void UpdateStatusText()
        {
            string text;
            if (ServerRunning) text = "● Widget activo — " + InstallInfo.WidgetUrl;
            else if (InstallInfo.IsPortInUse()) text = "● Usando otro servidor ya activo (puerto " + InstallInfo.Port + ")";
            else if (mode == Mode.Streamlabs) text = "○ En espera: abre Streamlabs Desktop";
            else text = "○ Servidor detenido";
            statusItem.Text = text;

            string tip = "Spotify Local - " + (ServerRunning || InstallInfo.IsPortInUse() ? "activo" : "en espera");
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
        }

        void RefreshMenu()
        {
            modeStreamlabsItem.Checked = mode == Mode.Streamlabs;
            modeAlwaysItem.Checked = mode == Mode.Always;
            autostartItem.Checked = InstallInfo.IsAutostartEnabled();
            UpdateStatusText();
        }

        // ---------------- Acciones del menú ----------------

        void OpenConfig()
        {
            // Con Streamlabs cerrado el servidor está apagado: lo encendemos 10 min
            keepAliveUntil = DateTime.Now.AddMinutes(10);
            Tick();
            var deadline = DateTime.Now.AddSeconds(5);
            while (!InstallInfo.IsPortInUse() && DateTime.Now < deadline)
                System.Threading.Thread.Sleep(150);
            OpenUrl(InstallInfo.ConfigUrl);
        }

        void CopyUrl()
        {
            try
            {
                Clipboard.SetText(InstallInfo.WidgetUrl);
                tray.ShowBalloonTip(2500, "URL copiada", InstallInfo.WidgetUrl + "\nPégala en una Fuente de navegador de 450×150.", ToolTipIcon.Info);
            }
            catch { }
        }

        void ShowHelp()
        {
            MessageBox.Show(
                "Cómo añadir el widget en Streamlabs Desktop:\n\n" +
                "1. En el panel \"Fuentes\" pulsa el botón +\n" +
                "2. Elige \"Fuente de navegador\" (Browser Source)\n" +
                "3. URL: " + InstallInfo.WidgetUrl + "\n" +
                "4. Ancho: " + InstallInfo.WidgetWidth + "   Alto: " + InstallInfo.WidgetHeight + "\n" +
                "5. Pon música en Spotify: el widget aparece solo y se oculta al pausar.\n\n" +
                "Para cambiar el aspecto: clic derecho en este icono → \"Cambiar aspecto del widget\".",
                InstallInfo.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void SetMode(Mode m)
        {
            mode = m;
            SaveMode(m);
            Tick();
            RefreshMenu();
        }

        void ToggleAutostart()
        {
            try { InstallInfo.SetAutostart(!InstallInfo.IsAutostartEnabled()); }
            catch (Exception ex) { MessageBox.Show("No se pudo cambiar el inicio automático:\n" + ex.Message); }
            RefreshMenu();
        }

        void Exit()
        {
            timer.Stop();
            StopServer();
            tray.Visible = false;
            Application.Exit();
        }

        public static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        // ---------------- Ajustes ----------------

        static Mode LoadMode()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(InstallInfo.SettingsKeyPath))
                    return (key?.GetValue("Mode") as string) == "always" ? Mode.Always : Mode.Streamlabs;
            }
            catch { return Mode.Streamlabs; }
        }

        static void SaveMode(Mode m)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(InstallInfo.SettingsKeyPath))
                    key.SetValue("Mode", m == Mode.Always ? "always" : "streamlabs");
            }
            catch { }
        }

        public void Dispose()
        {
            timer.Dispose();
            StopServer();
            tray.Visible = false;
            tray.Dispose();
        }
    }
}
