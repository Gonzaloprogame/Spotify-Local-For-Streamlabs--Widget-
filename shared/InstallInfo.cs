// Código compartido entre el lanzador (SpotifyOverlay.exe, .NET Framework 4.8)
// y el instalador (.NET 9). Mantener compatible con ambos.
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using Microsoft.Win32;

namespace SpotifyOverlayShared
{
    public static class InstallInfo
    {
        public const string AppName = "Spotify Local para Streamlabs";
        public const string Version = "1.0.0";
        public const string Publisher = "ElGonzaaTK (basado en el proyecto de alvarro71)";
        public const int Port = 9274;
        public const string WidgetUrl = "http://localhost:9274/";
        public const string ConfigUrl = "http://localhost:9274/config";
        public const int WidgetWidth = 450;
        public const int WidgetHeight = 150;

        public const string ServerExeName = "SpotifyOverlayServer.exe";
        public const string LauncherExeName = "SpotifyOverlay.exe";
        public const string ServerProcessName = "SpotifyOverlayServer";
        public const string LauncherProcessName = "SpotifyOverlay";

        const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValueName = "SpotifyLocalStreamlabs";
        const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SpotifyLocalStreamlabs";
        public const string SettingsKeyPath = @"Software\SpotifyLocalStreamlabs";

        // Nombres de proceso de Streamlabs Desktop (antes "Streamlabs OBS").
        static readonly string[] StreamlabsProcessNames = { "Streamlabs OBS", "Streamlabs Desktop", "slobs" };

        /// %LOCALAPPDATA%\SpotifyLocalStreamlabs — escribible sin permisos de administrador
        /// y fuera de la carpeta de Streamlabs (que se sobrescribe en cada actualización).
        public static string InstallDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpotifyLocalStreamlabs");

        public static string ServerPath => Path.Combine(InstallDir, ServerExeName);
        public static string LauncherPath => Path.Combine(InstallDir, LauncherExeName);
        public static string ConfigPath => Path.Combine(InstallDir, "config.json");

        static string StartMenuShortcutPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk");

        public static bool IsInstalled => File.Exists(ServerPath) && File.Exists(LauncherPath);

        // ---------------- Streamlabs ----------------

        public static bool IsStreamlabsRunning()
        {
            foreach (var name in StreamlabsProcessNames)
            {
                var procs = Process.GetProcessesByName(name);
                bool any = procs.Length > 0;
                foreach (var p in procs) p.Dispose();
                if (any) return true;
            }
            return false;
        }

        /// Busca una instalación de Streamlabs Desktop (solo informativo).
        public static string FindStreamlabs()
        {
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] candidates =
            {
                Path.Combine(pf, @"Streamlabs OBS\Streamlabs OBS.exe"),
                Path.Combine(pf, @"Streamlabs Desktop\Streamlabs Desktop.exe"),
                Path.Combine(pf86, @"Streamlabs OBS\Streamlabs OBS.exe"),
                Path.Combine(local, @"Programs\streamlabs-obs\Streamlabs OBS.exe"),
                Path.Combine(local, @"Programs\Streamlabs OBS\Streamlabs OBS.exe"),
                Path.Combine(local, @"Programs\Streamlabs Desktop\Streamlabs Desktop.exe"),
            };
            foreach (var c in candidates)
                if (File.Exists(c)) return c;

            // Datos de usuario de Streamlabs: si existen, está (o estuvo) instalado
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string slobsData = Path.Combine(appData, "slobs-client");
            return Directory.Exists(slobsData) ? slobsData : null;
        }

        // ---------------- Servidor ----------------

        public static bool IsPortInUse()
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect("127.0.0.1", Port, null, null);
                    bool ok = result.AsyncWaitHandle.WaitOne(150);
                    if (ok) { client.EndConnect(result); return true; }
                }
            }
            catch { }
            return false;
        }

        /// Detiene el lanzador y el servidor de ESTA instalación (no toca la versión de OBS,
        /// cuyo servidor se llama igual pero vive en la carpeta de plugins de OBS).
        public static void StopOurProcesses(Action<string> log = null)
        {
            int me = Process.GetCurrentProcess().Id;
            foreach (var name in new[] { LauncherProcessName, ServerProcessName })
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    try
                    {
                        if (p.Id == me) continue;
                        string file = null;
                        try { file = p.MainModule?.FileName; } catch { }
                        if (file != null && !file.StartsWith(InstallDir, StringComparison.OrdinalIgnoreCase))
                            continue; // proceso de otra instalación (p. ej. el plugin de OBS)
                        log?.Invoke("· Deteniendo " + name + ".exe (PID " + p.Id + ")...");
                        p.Kill();
                        p.WaitForExit(3000);
                    }
                    catch (Exception ex) { log?.Invoke("· No se pudo detener " + name + ": " + ex.Message); }
                    finally { p.Dispose(); }
                }
            }
        }

        // ---------------- Inicio con Windows ----------------

        public static bool IsAutostartEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath))
                return key?.GetValue(RunValueName) != null;
        }

        public static void SetAutostart(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (enabled) key.SetValue(RunValueName, "\"" + LauncherPath + "\"");
                else if (key.GetValue(RunValueName) != null) key.DeleteValue(RunValueName);
            }
        }

        // ---------------- Accesos directos / Panel de control ----------------

        public static void CreateStartMenuShortcut()
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            object shell = Activator.CreateInstance(shellType);
            object sc = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { StartMenuShortcutPath });
            Type scType = sc.GetType();
            scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { LauncherPath });
            scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { InstallDir });
            scType.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { "Widget de Spotify para Streamlabs" });
            scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { LauncherPath + ",0" });
            scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
        }

        public static void RemoveStartMenuShortcut()
        {
            if (File.Exists(StartMenuShortcutPath)) File.Delete(StartMenuShortcutPath);
        }

        /// Registra la app en "Aplicaciones instaladas" de Windows (por usuario).
        /// Desinstalar desde ahí ejecuta "SpotifyOverlay.exe --uninstall".
        public static void RegisterUninstallEntry()
        {
            using (var key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath))
            {
                key.SetValue("DisplayName", AppName);
                key.SetValue("DisplayVersion", Version);
                key.SetValue("Publisher", Publisher);
                key.SetValue("DisplayIcon", LauncherPath + ",0");
                key.SetValue("InstallLocation", InstallDir);
                key.SetValue("UninstallString", "\"" + LauncherPath + "\" --uninstall");
                key.SetValue("URLInfoAbout", "https://github.com/gonzaloprogame/spotify-local-for-streamlabs--widget-");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        /// Quita todo rastro salvo los archivos de la carpeta de instalación.
        public static void RemoveRegistration()
        {
            SetAutostart(false);
            RemoveStartMenuShortcut();
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, false);
            Registry.CurrentUser.DeleteSubKeyTree(SettingsKeyPath, false);
        }
    }
}
