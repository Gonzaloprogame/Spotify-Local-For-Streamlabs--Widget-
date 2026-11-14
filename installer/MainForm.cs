using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using SpotifyOverlayShared;

// Instalador de "Spotify Local para Streamlabs".
// Adaptado del instalador de Spotify-Local-For-OBS--Widget- (alvarro71, MIT):
// en vez de copiar un plugin .dll dentro de OBS, instala el servidor y un
// lanzador de bandeja en %LOCALAPPDATA% (sin permisos de administrador), que
// arranca el widget automáticamente cuando se abre Streamlabs Desktop.

namespace SpotifyOverlayInstaller
{
    public class MainForm : Form
    {
        // Colors
        private readonly Color SpotifyGreen = Color.FromArgb(29, 185, 84);
        private readonly Color SpotifyGreenDark = Color.FromArgb(20, 150, 65);
        private readonly Color DarkBg = Color.FromArgb(18, 18, 18);
        private readonly Color DarkSurface = Color.FromArgb(28, 28, 28);
        private readonly Color DarkSurfaceLight = Color.FromArgb(42, 42, 42);
        private readonly Color TextSecondaryColor = Color.FromArgb(180, 180, 180);
        private readonly Color TextMutedColor = Color.FromArgb(120, 120, 120);

        private Button installButton;
        private Button uninstallButton;
        private Button closeButton;
        private CustomProgressBar progressBar;
        private Label statusLabel;
        private TextBox logBox;
        private Panel headerPanel;
        private Panel mainPanel;
        private Panel bottomPanel;
        private System.Windows.Forms.Timer fadeTimer;

        // Custom config controls and fields
        private Label descLabel;
        private Label mainSeparator;
        private Label featuresHeader;
        private Label featureList;
        private Panel configPanel;
        private ComboBox themeComboBox;
        private Button applyThemeButton;
        private Label configStatusLabel;
        private CheckBox autostartCheck;
        private Button copyUrlButton;

        // Theme preview controls
        private Microsoft.Web.WebView2.WinForms.WebView2 webViewPreview;
        private System.Diagnostics.Process temporaryServerProcess = null;
        private bool isWebViewInitialized = false;

        public MainForm()
        {
            this.Opacity = 0; // Start invisible for fade-in animation
            InitializeComponent();
            
            // Initialize Config Panel Controls
            InitializeConfigControls();
            
            // Setup Fade-in Animation Timer
            fadeTimer = new System.Windows.Forms.Timer();
            fadeTimer.Interval = 15;
            fadeTimer.Tick += (s, e) =>
            {
                if (this.Opacity < 1.0)
                {
                    this.Opacity += 0.08;
                }
                else
                {
                    fadeTimer.Stop();
                }
            };
            this.Load += (s, e) => fadeTimer.Start();

            // Check if already installed
            if (InstallInfo.IsInstalled)
            {
                ShowConfigLayout();
            }
            else
            {
                ShowInstallLayout();
            }
        }

        private void InitializeComponent()
        {
            this.Text = "Spotify Local para Streamlabs v" + InstallInfo.Version + " - Instalador";
            this.Size = new Size(560, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = DarkBg;
            this.Font = new Font("Segoe UI", 9, FontStyle.Regular);
            this.ShowIcon = false;
            this.AutoScaleMode = AutoScaleMode.Dpi;

            // Header Panel with custom paint gradient
            headerPanel = new Panel
            {
                Size = new Size(560, 150),
                Location = new Point(0, 0),
                BackColor = SpotifyGreen
            };
            headerPanel.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                using (var brush = new LinearGradientBrush(
                    headerPanel.ClientRectangle,
                    SpotifyGreen,
                    Color.FromArgb(20, 130, 60),
                    LinearGradientMode.Horizontal))
                {
                    g.FillRectangle(brush, headerPanel.ClientRectangle);
                }
            };

            var logoLabel = new Label
            {
                Text = "♫",
                Font = new Font("Segoe UI", 48, FontStyle.Bold),
                ForeColor = Color.White,
                Size = new Size(90, 90),
                Location = new Point(20, 15),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            headerPanel.Controls.Add(logoLabel);

            var titleLabel = new Label
            {
                Text = "Spotify Local",
                Font = new Font("Segoe UI", 28, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(120, 22),
                BackColor = Color.Transparent
            };
            headerPanel.Controls.Add(titleLabel);

            var subtitleLabel = new Label
            {
                Text = "para Streamlabs Desktop",
                Font = new Font("Segoe UI", 12),
                ForeColor = Color.FromArgb(200, 255, 255, 255),
                AutoSize = true,
                Location = new Point(122, 70),
                BackColor = Color.Transparent
            };
            headerPanel.Controls.Add(subtitleLabel);

            var versionLabel = new Label
            {
                Text = "v" + InstallInfo.Version + "  ·  basado en el proyecto de alvarro71",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(180, 255, 255, 255),
                AutoSize = true,
                Location = new Point(122, 95),
                BackColor = Color.Transparent
            };
            headerPanel.Controls.Add(versionLabel);

            // Main Panel
            mainPanel = new Panel
            {
                Size = new Size(540, 400),
                Location = new Point(10, 160),
                BackColor = DarkBg
            };

            descLabel = new Label
            {
                Text = "Este instalador deja el widget listo para usarlo en Streamlabs Desktop (no necesita permisos de administrador).",
                Font = new Font("Segoe UI", 10),
                ForeColor = TextSecondaryColor,
                Size = new Size(520, 30),
                Location = new Point(10, 10)
            };
            mainPanel.Controls.Add(descLabel);

            mainSeparator = new Label
            {
                Text = "",
                BackColor = DarkSurfaceLight,
                Size = new Size(520, 1),
                Location = new Point(10, 50)
            };
            mainPanel.Controls.Add(mainSeparator);

            featuresHeader = new Label
            {
                Text = "✦ Características",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = SpotifyGreen,
                AutoSize = true,
                Location = new Point(10, 65)
            };
            mainPanel.Controls.Add(featuresHeader);

            featureList = new Label
            {
                Text = "  ▶  Detección automática de Spotify y otras apps (SMTC de Windows)\r\n\r\n"
                     + "  ▶  Se activa solo al abrir Streamlabs y se apaga al cerrarlo\r\n\r\n"
                     + "  ▶  100 temas con vista previa, cambiables en cualquier momento\r\n\r\n"
                     + "  ▶  100% local: sin cuentas, sin tokens, sin Internet",
                 Font = new Font("Segoe UI", 9),
                 ForeColor = TextSecondaryColor,
                 Size = new Size(520, 140),
                 Location = new Point(10, 95)
            };
            mainPanel.Controls.Add(featureList);

            autostartCheck = new CheckBox
            {
                Text = "Iniciar con Windows (recomendado: así el widget está listo al abrir Streamlabs)",
                Font = new Font("Segoe UI", 9),
                ForeColor = TextSecondaryColor,
                Size = new Size(520, 24),
                Location = new Point(14, 240),
                Checked = true
            };
            mainPanel.Controls.Add(autostartCheck);

            progressBar = new CustomProgressBar
            {
                Size = new Size(520, 25),
                Location = new Point(10, 275),
                Visible = false
            };
            mainPanel.Controls.Add(progressBar);

            statusLabel = new Label
            {
                Text = "Estado: Esperando para iniciar...",
                Font = new Font("Segoe UI", 9),
                ForeColor = TextMutedColor,
                Size = new Size(520, 25),
                Location = new Point(10, 310)
            };
            mainPanel.Controls.Add(statusLabel);

            logBox = new TextBox
            {
                Size = new Size(520, 70),
                Location = new Point(10, 340),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 8),
                BackColor = Color.FromArgb(24, 24, 24),
                ForeColor = TextSecondaryColor,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false,
                Text = ""
            };
            mainPanel.Controls.Add(logBox);

            // Bottom Panel
            bottomPanel = new Panel
            {
                Size = new Size(560, 90),
                Location = new Point(0, 560),
                BackColor = DarkSurface
            };

            var divider = new Label
            {
                Text = "",
                BackColor = DarkSurfaceLight,
                Size = new Size(560, 1),
                Location = new Point(0, 0)
            };
            bottomPanel.Controls.Add(divider);

            installButton = new Button
            {
                Text = "▶  Instalar ahora",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Size = new Size(180, 50),
                Location = new Point(20, 20),
                BackColor = SpotifyGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 }
            };
            installButton.Click += InstallButton_Click;
            installButton.MouseEnter += (s, e) => installButton.BackColor = Color.FromArgb(35, 205, 96);
            installButton.MouseLeave += (s, e) => installButton.BackColor = SpotifyGreen;
            bottomPanel.Controls.Add(installButton);

            uninstallButton = new Button
            {
                Text = "🗑  Desinstalar",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Size = new Size(185, 50),
                Location = new Point(215, 20),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.FromArgb(255, 100, 100),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 }
            };
            uninstallButton.Click += UninstallButton_Click;
            uninstallButton.MouseEnter += (s, e) => uninstallButton.BackColor = Color.FromArgb(50, 40, 40);
            uninstallButton.MouseLeave += (s, e) => uninstallButton.BackColor = Color.FromArgb(40, 40, 40);
            bottomPanel.Controls.Add(uninstallButton);

            closeButton = new Button
            {
                Text = "Cerrar",
                Font = new Font("Segoe UI", 10),
                Size = new Size(120, 50),
                Location = new Point(415, 20),
                BackColor = DarkSurfaceLight,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Enabled = false,
                Visible = false
            };
            closeButton.Click += (s, e) => this.Close();
            closeButton.MouseEnter += (s, e) => closeButton.BackColor = Color.FromArgb(60, 60, 60);
            closeButton.MouseLeave += (s, e) => closeButton.BackColor = DarkSurfaceLight;
            bottomPanel.Controls.Add(closeButton);

            this.Controls.Add(headerPanel);
            this.Controls.Add(mainPanel);
            this.Controls.Add(bottomPanel);
        }

        private void LogStreamlabsDetection()
        {
            AppendLog("▶ Buscando Streamlabs Desktop...");
            string sl = InstallInfo.FindStreamlabs();
            if (sl != null)
                AppendLog("✓ Streamlabs Desktop detectado: " + sl);
            else
                AppendLog("· No se encontró Streamlabs Desktop (no pasa nada: el widget funciona igual en cualquier programa con fuente de navegador).");
        }

        private void AppendLog(string text)
        {
            if (logBox.InvokeRequired)
            {
                logBox.Invoke(new Action<string>(AppendLog), text);
                return;
            }
            logBox.Text += text + Environment.NewLine;
            logBox.SelectionStart = logBox.Text.Length;
            logBox.ScrollToCaret();
        }

        private void SetStatus(string text, Color? color = null)
        {
            if (statusLabel.InvokeRequired)
            {
                statusLabel.Invoke(new Action(() => SetStatus(text, color)));
                return;
            }
            statusLabel.Text = "Estado: " + text;
            statusLabel.ForeColor = color ?? TextMutedColor;
        }

        private void SetProgress(int value)
        {
            if (progressBar.InvokeRequired)
            {
                progressBar.Invoke(new Action<int>(SetProgress), value);
                return;
            }
            progressBar.Value = value;
        }

        private void InstallButton_Click(object sender, EventArgs e)
        {
            installButton.Enabled = false;
            uninstallButton.Enabled = false;
            autostartCheck.Enabled = false;
            installButton.Text = "⏳ Instalando...";
            progressBar.Visible = true;
            logBox.Visible = true;
            logBox.Text = "";

            try
            {
                LogStreamlabsDetection();
                SetProgress(10);

                // Detener lanzador/servidor de una instalación anterior para liberar archivos
                SetStatus("Cerrando versiones anteriores...", Color.White);
                InstallInfo.StopOurProcesses(AppendLog);
                StopTemporaryServer();
                SetProgress(20);

                string dir = InstallInfo.InstallDir;
                AppendLog("▶ Carpeta de instalación: " + dir);
                Directory.CreateDirectory(dir);
                SetProgress(30);

                SetStatus("Instalando servidor de datos...", Color.White);
                ExtractResource(InstallInfo.ServerExeName, InstallInfo.ServerPath);
                AppendLog("✓ Servidor de datos instalado (" + InstallInfo.ServerExeName + ")");
                SetProgress(60);

                SetStatus("Instalando lanzador...", Color.White);
                ExtractResource(InstallInfo.LauncherExeName, InstallInfo.LauncherPath);
                AppendLog("✓ Lanzador instalado (" + InstallInfo.LauncherExeName + ")");
                SetProgress(75);

                if (!File.Exists(InstallInfo.ConfigPath))
                {
                    File.WriteAllText(InstallInfo.ConfigPath, "{\n  \"theme\": \"glassmorphism\"\n}");
                    AppendLog("✓ Configuración creada (tema: Glassmorphism)");
                }
                else
                {
                    AppendLog("✓ Configuración anterior conservada");
                }

                SetStatus("Registrando en Windows...", Color.White);
                InstallInfo.SetAutostart(autostartCheck.Checked);
                AppendLog(autostartCheck.Checked ? "✓ Se iniciará con Windows" : "· Inicio con Windows desactivado");
                try { InstallInfo.CreateStartMenuShortcut(); AppendLog("✓ Acceso directo creado en el menú Inicio"); }
                catch (Exception scEx) { AppendLog("· No se pudo crear el acceso directo: " + scEx.Message); }
                InstallInfo.RegisterUninstallEntry();
                AppendLog("✓ Añadido a \"Aplicaciones instaladas\" de Windows");
                SetProgress(90);

                // Arrancar el lanzador ya (icono en la bandeja)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = InstallInfo.LauncherPath,
                    Arguments = "--welcome",
                    WorkingDirectory = dir,
                    UseShellExecute = false
                });
                AppendLog("✓ Lanzador iniciado (icono ♫ en la bandeja del sistema)");

                SetProgress(100);
                SetStatus("✓ ¡Instalación completada con éxito!", SpotifyGreen);

                installButton.Text = "✓ ¡Instalado!";
                installButton.BackColor = Color.FromArgb(29, 185, 84);
                closeButton.Enabled = true;
                closeButton.Visible = true;

                AppendLog("");
                AppendLog("═══════════════════════════════════════");
                AppendLog("✓ INSTALACIÓN COMPLETADA CON ÉXITO");
                AppendLog("═══════════════════════════════════════");

                MessageBox.Show(
                    "¡Instalación completada con éxito!\n\nAhora en Streamlabs Desktop:\n\n"
                    + "1. Pulsa + en \"Fuentes\"\n"
                    + "2. Elige \"Fuente de navegador\" (Browser Source)\n"
                    + "3. URL: " + InstallInfo.WidgetUrl + "\n"
                    + "4. Tamaño: " + InstallInfo.WidgetWidth + "×" + InstallInfo.WidgetHeight + "\n"
                    + "5. ¡Reproduce música en Spotify!\n\n"
                    + "El widget se enciende solo cuando abres Streamlabs. Puedes cambiar el tema desde aquí "
                    + "o con clic derecho en el icono ♫ de la bandeja.",
                    "✓ Instalación completada",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                ShowConfigLayout();
                configStatusLabel.Text = "¡Instalado con éxito!";
                configStatusLabel.ForeColor = SpotifyGreen;
            }
            catch (Exception ex)
            {
                SetStatus("✗ Error durante la instalación", Color.Red);
                AppendLog("");
                AppendLog("✗ ERROR: " + ex.Message);
                AppendLog("  Detalles: " + ex.ToString());

                MessageBox.Show(
                    "Ocurrió un error durante la instalación:\n\n" + ex.Message,
                    "Error de instalación",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                installButton.Enabled = true;
                uninstallButton.Enabled = true;
                autostartCheck.Enabled = true;
                installButton.Text = "↻ Reintentar";
            }
        }

        private void UninstallButton_Click(object sender, EventArgs e)
        {
            var confirmResult = MessageBox.Show(
                "¿Estás seguro de que deseas desinstalar Spotify Local para Streamlabs?\n\nSe eliminarán el servidor, el lanzador y la configuración. La fuente de navegador que añadiste en Streamlabs no se toca (puedes borrarla a mano).",
                "Confirmar Desinstalación",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirmResult != DialogResult.Yes)
            {
                return;
            }

            // Mostrar los controles de progreso (en la vista de configuración están ocultos)
            configPanel.Visible = false;
            installButton.Enabled = false;
            uninstallButton.Enabled = false;
            uninstallButton.Text = "⏳ Desinstalando...";
            progressBar.Visible = true;
            statusLabel.Visible = true;
            logBox.Visible = true;
            logBox.Text = "";

            try
            {
                SetStatus("Cerrando el widget...", Color.White);
                AppendLog("▶ Iniciando desinstalación...");
                StopTemporaryServer();
                InstallInfo.StopOurProcesses(AppendLog);
                SetProgress(30);

                SetStatus("Quitando registro de Windows...", Color.White);
                InstallInfo.RemoveRegistration();
                AppendLog("✓ Quitado del inicio de Windows, menú Inicio y Aplicaciones instaladas");
                SetProgress(60);

                SetStatus("Eliminando archivos...", Color.White);
                bool removedAny = false;
                if (Directory.Exists(InstallInfo.InstallDir))
                {
                    Directory.Delete(InstallInfo.InstallDir, true);
                    AppendLog("✓ Eliminada la carpeta: " + InstallInfo.InstallDir);
                    removedAny = true;
                }
                else
                {
                    AppendLog("· No se encontró la carpeta de instalación (ya desinstalado)");
                }

                SetProgress(100);

                if (removedAny)
                {
                    SetStatus("✓ ¡Desinstalado con éxito!", SpotifyGreen);
                    AppendLog("");
                    AppendLog("═══════════════════════════════════════");
                    AppendLog("✓ DESINSTALACIÓN COMPLETADA CON ÉXITO");
                    AppendLog("═══════════════════════════════════════");

                    MessageBox.Show(
                        "¡Spotify Local para Streamlabs se ha desinstalado correctamente!",
                        "Desinstalación completada",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    SetStatus("✓ Ya estaba desinstalado", Color.Yellow);
                }

                uninstallButton.Text = "✓ ¡Desinstalado!";
                uninstallButton.BackColor = Color.FromArgb(50, 50, 50);
                closeButton.Enabled = true;
                closeButton.Visible = true;

                ShowInstallLayout();
                installButton.Text = "▶  Instalar ahora";
                installButton.Enabled = true;
                autostartCheck.Enabled = true;
            }
            catch (Exception ex)
            {
                SetStatus("✗ Error durante la desinstalación", Color.Red);
                AppendLog("✗ ERROR: " + ex.Message);
                uninstallButton.Enabled = true;
                installButton.Enabled = true;
                uninstallButton.Text = "🗑  Desinstalar";

                MessageBox.Show(
                    "Ocurrió un error al intentar desinstalar:\n\n" + ex.Message,
                    "Error de desinstalación",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ExtractResource(string name, string outputPath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string[] resourceNames = assembly.GetManifestResourceNames();
            string resourceName = null;

            foreach (var rName in resourceNames)
            {
                if (rName.EndsWith(name, StringComparison.OrdinalIgnoreCase))
                {
                    resourceName = rName;
                    break;
                }
            }

            if (resourceName == null)
            {
                throw new Exception($"No se encontró el recurso '{name}' incrustado en el instalador.");
            }

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new Exception($"Error al cargar el recurso '{resourceName}'.");

                using (FileStream fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fileStream);
                }
            }
        }

        private void InitializeConfigControls()
        {
            configPanel = new Panel
            {
                Size = new Size(520, 390),
                Location = new Point(10, 10),
                BackColor = DarkBg,
                Visible = false
            };

            var configHeader = new Label
            {
                Text = "✔ ¡Widget instalado!",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = SpotifyGreen,
                Size = new Size(500, 25),
                Location = new Point(10, 5)
            };
            configPanel.Controls.Add(configHeader);

            var configDesc = new Label
            {
                Text = "En Streamlabs añade una Fuente de navegador con la URL " + InstallInfo.WidgetUrl + " (450×150). Aquí puedes elegir su aspecto con vista previa.",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = TextSecondaryColor,
                Size = new Size(500, 35),
                Location = new Point(10, 30)
            };
            configPanel.Controls.Add(configDesc);

            themeComboBox = new ComboBox
            {
                Location = new Point(15, 70),
                Width = 490,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(32, 32, 32),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10.5f)
            };
            themeComboBox.Items.Add("01. Glassmorphism (Elegante y translúcido)");
            themeComboBox.Items.Add("02. Flat Dark (Negro minimalista y plano)");
            themeComboBox.Items.Add("03. Neon Cyberpunk (Brillos neón verde/azul)");
            themeComboBox.Items.Add("04. Compact Pill (Pequeño y redondeado)");
            themeComboBox.Items.Add("05. Retro Arcade (Bordes 8-bit amarillo/magenta)");
            themeComboBox.Items.Add("06. Synthwave (Retro 80s neon rosa/azul)");
            themeComboBox.Items.Add("07. Glass Light (Cristal blanco para temas claros)");
            themeComboBox.Items.Add("08. Spotify Green (Verde oficial de Spotify)");
            themeComboBox.Items.Add("09. AMOLED (Negro puro para pantallas OLED)");
            themeComboBox.Items.Add("10. Kawaii (Pastel rosa muy tierno)");
            themeComboBox.Items.Add("11. Brutalism (Diseño crudo con bordes gruesos)");
            themeComboBox.Items.Add("12. Monochrome (Escala de grises minimalista)");
            themeComboBox.Items.Add("13. Cyberpunk Yellow (Amarillo industrial)");
            themeComboBox.Items.Add("14. Aurora (Gradiente nórdico verde y azul)");
            themeComboBox.Items.Add("15. Futuristic (Tecnológico en azul cian)");
            themeComboBox.Items.Add("16. Minimal Art (Diseño horizontal y limpio)");
            themeComboBox.Items.Add("17. Woodland (Tonos verdes acogedores)");
            themeComboBox.Items.Add("18. Crimson Goth (Vampírico rojo y negro)");
            themeComboBox.Items.Add("19. Sunset Orange (Atardecer cálido)");
            themeComboBox.Items.Add("20. Gold Luxury (Elegante negro y oro)");
            themeComboBox.Items.Add("21. Bubblegum (Rosa chicle y azul cielo)");
            themeComboBox.Items.Add("22. Vinyl Record (Plato de vinilo girando)");
            themeComboBox.Items.Add("23. Polaroid Photo (Foto instantánea retro)");
            themeComboBox.Items.Add("24. Comic Strip (Estilo cómic pop-art)");
            themeComboBox.Items.Add("25. Matrix Terminal (Lluvia de código verde)");
            themeComboBox.Items.Add("26. Split Bleed (Carátula borrosa de fondo)");
            themeComboBox.Items.Add("27. Neon Pulse (Bordes palpitantes neón)");
            themeComboBox.Items.Add("28. Paper Scrap (Papel rasgado con celo)");
            themeComboBox.Items.Add("29. Glitch HUD (Consola militar con interferencias)");
            themeComboBox.Items.Add("30. Neumorphic (Efecto relieve 3D suave)");
            themeComboBox.Items.Add("31. Vaporwave Sunset (Cálido retro con palmeras)");
            themeComboBox.Items.Add("32. Lava Lamp (Gotas de lava orgánica en movimiento)");
            themeComboBox.Items.Add("33. Chalkboard (Pizarra escolar con tiza blanca)");
            themeComboBox.Items.Add("34. Space Odyssey (Estilo cápsula espacial cósmica)");
            themeComboBox.Items.Add("35. Glass Glass (Doble cristal superpuesto)");
            themeComboBox.Items.Add("36. Liquid Metal (Cromo líquido y reflejos)");
            themeComboBox.Items.Add("37. Cardboard Box (Cartón reciclado y estarcido)");
            themeComboBox.Items.Add("38. Pixel Drip (Goteo pixelado de videojuegos)");
            themeComboBox.Items.Add("39. Cyber Hazard (Líneas de peligro amarillo/negro)");
            themeComboBox.Items.Add("40. Watercolor (Efecto acuarela artística fluida)");
            themeComboBox.Items.Add("41. Stained Glass (Mosaico de vidriera gótica)");
            themeComboBox.Items.Add("42. Prismatic Crystal (Gradiente de prisma multicolor)");
            themeComboBox.Items.Add("43. Ghostly Transparency (Translúcido flotante)");
            themeComboBox.Items.Add("44. Terminal Green (Línea de comandos de fósforo verde)");
            themeComboBox.Items.Add("45. Blueprint Engineering (Plano técnico azul y rejilla)");
            themeComboBox.Items.Add("46. Carbon Fiber (Textura deportiva roja y gris)");
            themeComboBox.Items.Add("47. Retro Cassette (Cinta de cassette retro animada)");
            themeComboBox.Items.Add("48. Autumn Leaves (Hojas de otoño y tonos cálidos)");
            themeComboBox.Items.Add("49. Frozen Ice (Cristales de hielo azul y escarcha)");
            themeComboBox.Items.Add("50. Glitch Art (Efectos de interferencia digital RGB)");
            themeComboBox.Items.Add("51. Steampunk Brass (Engranajes de cobre y remaches)");
            themeComboBox.Items.Add("52. Pop Art Bubble (Patrón de puntos retro explosivo)");
            themeComboBox.Items.Add("53. Golden Hour (Colores cálidos del atardecer)");
            themeComboBox.Items.Add("54. Midnight Eclipse (Corona solar alrededor de carátula)");
            themeComboBox.Items.Add("55. Cyber Hacking Grid (Nodos de red ciberseguridad)");
            themeComboBox.Items.Add("56. Origami Folded (Diseño geométrico de papel doblado)");
            themeComboBox.Items.Add("57. Rainbow Wave (Olas de colores fluidos en movimiento)");
            themeComboBox.Items.Add("58. Leather Notebook (Cuero cosido con grabados oro)");
            themeComboBox.Items.Add("59. Deep Sea Abyssal (Bioluminiscencia marina profunda)");
            themeComboBox.Items.Add("60. Vintage Typewriter (Papel envejecido y teclas metal)");
            themeComboBox.Items.Add("61. Techno Club (Efectos estroboscópicos de club nocturno)");
            themeComboBox.Items.Add("62. Walking Cat (Gato caminando sobre barra)");
            themeComboBox.Items.Add("63. Running Dog (Perro corriendo tras barra)");
            themeComboBox.Items.Add("64. Floating Astronaut (Astronauta flotando)");
            themeComboBox.Items.Add("65. Dancing Ghost (Fantasmita flotante)");
            themeComboBox.Items.Add("66. Heart Pulse (Corazón neón latiendo)");
            themeComboBox.Items.Add("67. UFO Abduction (Luz de platillo volador)");
            themeComboBox.Items.Add("68. Retro Pacman (Estilo arcade comecocos)");
            themeComboBox.Items.Add("69. Bouncing CD (Disco CD rebotando)");
            themeComboBox.Items.Add("70. DJ Visualizer (Barras de ecualizador)");
            themeComboBox.Items.Add("71. Matrix Rain (Código binario cayendo)");
            themeComboBox.Items.Add("72. Snowfall (Nieve cayendo suavemente)");
            themeComboBox.Items.Add("73. Rainy Day (Gotas de lluvia deslizándose)");
            themeComboBox.Items.Add("74. Shooting Star (Estrellas fugaces en el cielo)");
            themeComboBox.Items.Add("75. Fire Flame (Llamas de fuego animadas)");
            themeComboBox.Items.Add("76. Neon Sinewave (Onda sinusoidal pulsante)");
            themeComboBox.Items.Add("77. Glitch Shaker (Textos que tiemblan)");
            themeComboBox.Items.Add("78. Disco Strobes (Colores discoteca cambiantes)");
            themeComboBox.Items.Add("79. Retro Tape Spin (Cinta de cassette giratoria)");
            themeComboBox.Items.Add("80. Vinyl Scratch (Aguja en disco giratorio)");
            themeComboBox.Items.Add("81. Neon Runner (Punto neón recorriendo bordes)");
            themeComboBox.Items.Add("82. Scrolling Text (Texto marquesina rodante)");
            themeComboBox.Items.Add("83. Soap Bubbles (Burbujas subiendo)");
            themeComboBox.Items.Add("84. Cyber Target (Mira de fijación de objetivo)");
            themeComboBox.Items.Add("85. Glitch VHS (Interferencias analógicas VHS)");
            themeComboBox.Items.Add("86. Spinning Earth (Planeta Tierra girando)");
            themeComboBox.Items.Add("87. Flying Bird (Pájarito aleteando)");
            themeComboBox.Items.Add("88. Rocket Launch (Cohete espacial despegando)");
            themeComboBox.Items.Add("89. Bouncing Soccer (Balón de fútbol botando)");
            themeComboBox.Items.Add("90. Dancing Cat (Cara de gatito bailando)");
            themeComboBox.Items.Add("91. Pixel Heart (Corazón retro de 8 bits)");
            themeComboBox.Items.Add("92. Vaporwave Sun (Sol retro wireframe)");
            themeComboBox.Items.Add("93. Glitch Matrix (Consola de código con distorsiones)");
            themeComboBox.Items.Add("94. Equalizer Wave (Onda de sonido suave)");
            themeComboBox.Items.Add("95. Party Popper (Confeti festivo explotando)");
            themeComboBox.Items.Add("96. Hourglass sand (Reloj de arena girando)");
            themeComboBox.Items.Add("97. Spinning Star (Estrella dorada de 5 puntas)");
            themeComboBox.Items.Add("98. Floating Balloon (Globo rojo flotando)");
            themeComboBox.Items.Add("99. Running Ninja (Ninja corriendo por barra)");
            themeComboBox.Items.Add("100. Lightning Strike (Tormenta eléctrica y rayos)");

            themeComboBox.SelectedIndex = 0;
            themeComboBox.SelectedIndexChanged += (s, e) => UpdatePreview(themeComboBox.SelectedIndex);
            configPanel.Controls.Add(themeComboBox);

            // Preview Panel Container Label
            var previewTitleLabel = new Label
            {
                Text = "✦ VISTA PREVIA DEL OVERLAY (Miniatura 100% Realista)",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(130, 130, 140),
                Location = new Point(15, 110),
                Size = new Size(490, 15)
            };
            configPanel.Controls.Add(previewTitleLabel);

            // Setup WebView2 Preview Control
            webViewPreview = new Microsoft.Web.WebView2.WinForms.WebView2
            {
                Size = new Size(490, 150),
                Location = new Point(15, 128),
                BackColor = Color.FromArgb(20, 20, 25),
                DefaultBackgroundColor = Color.Transparent
            };
            configPanel.Controls.Add(webViewPreview);

            applyThemeButton = new Button
            {
                Text = "✨ Aplicar Aspecto",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Size = new Size(180, 40),
                Location = new Point(15, 288),
                BackColor = SpotifyGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 }
            };
            applyThemeButton.Click += ApplyThemeButton_Click;
            applyThemeButton.MouseEnter += (s, e) => applyThemeButton.BackColor = Color.FromArgb(35, 205, 96);
            applyThemeButton.MouseLeave += (s, e) => applyThemeButton.BackColor = SpotifyGreen;
            configPanel.Controls.Add(applyThemeButton);

            configStatusLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Italic),
                ForeColor = SpotifyGreen,
                Size = new Size(170, 40),
                Location = new Point(335, 288),
                TextAlign = ContentAlignment.MiddleLeft
            };
            configPanel.Controls.Add(configStatusLabel);

            copyUrlButton = new Button
            {
                Text = "📋 Copiar URL",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = new Size(125, 40),
                Location = new Point(203, 288),
                BackColor = DarkSurfaceLight,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 }
            };
            copyUrlButton.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(InstallInfo.WidgetUrl);
                    configStatusLabel.Text = "✔ URL copiada";
                    configStatusLabel.ForeColor = SpotifyGreen;
                }
                catch { }
            };
            configPanel.Controls.Add(copyUrlButton);

            var helpBox = new Label
            {
                Text = "ℹ Al aplicar el aspecto, el widget de Streamlabs se recarga solo. También puedes cambiarlo luego con clic derecho en el icono ♫ de la bandeja → \"Cambiar aspecto del widget\".",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = TextMutedColor,
                Size = new Size(490, 35),
                Location = new Point(15, 338)
            };
            configPanel.Controls.Add(helpBox);

            mainPanel.Controls.Add(configPanel);
        }

        private async void ApplyThemeButton_Click(object sender, EventArgs e)
        {
            string themeValue = GetThemeValueByIndex(themeComboBox.SelectedIndex);

            try
            {
                // 1) Pedírselo al servidor: guarda config.json y recarga los widgets abiertos
                bool applied = false;
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    try
                    {
                        httpClient.Timeout = TimeSpan.FromSeconds(2);
                        var resp = await httpClient.GetAsync(InstallInfo.WidgetUrl + "api/set-theme?theme=" + themeValue);
                        applied = resp.IsSuccessStatusCode;
                    }
                    catch
                    {
                        // Servidor apagado: escribimos el archivo directamente
                    }
                }

                // 2) Escribir siempre el archivo de la instalación (por si el servidor que
                //    responde es otro, p. ej. la versión de OBS, o no hay ninguno)
                Directory.CreateDirectory(InstallInfo.InstallDir);
                File.WriteAllText(InstallInfo.ConfigPath, "{\n  \"theme\": \"" + themeValue + "\"\n}");

                configStatusLabel.Text = applied ? "✔ ¡Aspecto aplicado!" : "✔ Guardado (se verá al abrir Streamlabs)";
                configStatusLabel.ForeColor = SpotifyGreen;

                var timer = new System.Windows.Forms.Timer { Interval = 3000 };
                timer.Tick += (s, ev) =>
                {
                    configStatusLabel.Text = "";
                    timer.Stop();
                    timer.Dispose();
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                configStatusLabel.Text = "❌ Error: " + ex.Message;
                configStatusLabel.ForeColor = Color.Red;
            }
        }

        private void LoadCurrentTheme()
        {
            try
            {
                if (File.Exists(InstallInfo.ConfigPath))
                {
                    string json = File.ReadAllText(InstallInfo.ConfigPath);
                    var m = System.Text.RegularExpressions.Regex.Match(json, "\"theme\"\\s*:\\s*\"([a-z0-9_]+)\"");
                    if (!m.Success) return;
                    for (int i = 0; i < 100; i++)
                    {
                        if (GetThemeValueByIndex(i) == m.Groups[1].Value)
                        {
                            themeComboBox.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        private void UpdatePreview(int index)
        {
            if (!isWebViewInitialized || webViewPreview.CoreWebView2 == null) return;

            string themeValue = GetThemeValueByIndex(index);
            webViewPreview.CoreWebView2.Navigate("http://localhost:9274/preview?theme=" + themeValue);
        }

        private string GetThemeValueByIndex(int index)
        {
            switch (index)
            {
                case 0: return "glassmorphism";
                case 1: return "flat_dark";
                case 2: return "neon";
                case 3: return "compact";
                case 4: return "retro";
                case 5: return "synthwave";
                case 6: return "glass_light";
                case 7: return "spotify_green";
                case 8: return "amoled";
                case 9: return "kawaii";
                case 10: return "brutalism";
                case 11: return "monochrome";
                case 12: return "cyberpunk_yellow";
                case 13: return "aurora";
                case 14: return "futuristic";
                case 15: return "minimal_art";
                case 16: return "woodland";
                case 17: return "crimson";
                case 18: return "sunset";
                case 19: return "gold_luxury";
                case 20: return "bubblegum";
                case 21: return "vinyl";
                case 22: return "polaroid";
                case 23: return "comic_strip";
                case 24: return "matrix";
                case 25: return "split_bleed";
                case 26: return "neon_pulse";
                case 27: return "paper_scrap";
                case 28: return "glitch_hud";
                case 29: return "neumorphic";
                case 30: return "vaporwave_sunset";
                case 31: return "lava_lamp";
                case 32: return "chalkboard";
                case 33: return "space_odyssey";
                case 34: return "glass_glass";
                case 35: return "liquid_metal";
                case 36: return "cardboard_box";
                case 37: return "pixel_drip";
                case 38: return "cyber_hazard";
                case 39: return "watercolor";
                case 40: return "stained_glass";
                case 41: return "prismatic_crystal";
                case 42: return "ghostly";
                case 43: return "terminal_green";
                case 44: return "blueprint";
                case 45: return "carbon_fiber";
                case 46: return "retro_cassette";
                case 47: return "autumn_leaves";
                case 48: return "frozen_ice";
                case 49: return "glitch_art";
                case 50: return "steampunk_brass";
                case 51: return "pop_art_bubble";
                case 52: return "golden_hour";
                case 53: return "midnight_eclipse";
                case 54: return "cyber_grid";
                case 55: return "origami";
                case 56: return "rainbow_wave";
                case 57: return "leather";
                case 58: return "abyssal";
                case 59: return "typewriter";
                case 60: return "techno_club";
                case 61: return "walking_cat";
                case 62: return "running_dog";
                case 63: return "walking_astronaut";
                case 64: return "dancing_ghost";
                case 65: return "heart_pulse";
                case 66: return "ufo_abduction";
                case 67: return "retro_pacman";
                case 68: return "bouncing_cd";
                case 69: return "dj_visualizer";
                case 70: return "matrix_rain";
                case 71: return "snowfall";
                case 72: return "rainy_day";
                case 73: return "shooting_star";
                case 74: return "fire_flame";
                case 75: return "neon_sinewave";
                case 76: return "glitch_shaker";
                case 77: return "disco_strobes";
                case 78: return "retro_tape_spin";
                case 79: return "vinyl_scratch";
                case 80: return "neon_runner";
                case 81: return "scrolling_text";
                case 82: return "bubbles";
                case 83: return "cyber_target";
                case 84: return "glitch_vhs";
                case 85: return "spinning_earth";
                case 86: return "flying_bird";
                case 87: return "rocket_launch";
                case 88: return "bouncing_soccer";
                case 89: return "dancing_cat";
                case 90: return "pixel_heart";
                case 91: return "vaporwave_sun";
                case 92: return "glitch_matrix";
                case 93: return "equalizer_wave";
                case 94: return "party_popper";
                case 95: return "hourglass";
                case 96: return "spinning_star";
                case 97: return "floating_balloon";
                case 98: return "running_ninja";
                case 99: return "lightning_strike";
                default: return "glassmorphism";
            }
        }

        /// Para la vista previa hace falta el servidor. Si no está en marcha (por
        /// ejemplo, Streamlabs cerrado), lanzamos uno temporal que se apaga solo
        /// al cerrar el instalador (--parent-pid).
        private void StartTemporaryServer()
        {
            if (InstallInfo.IsPortInUse()) return;

            try
            {
                if (File.Exists(InstallInfo.ServerPath))
                {
                    temporaryServerProcess = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = InstallInfo.ServerPath,
                            Arguments = "--parent-pid " + Environment.ProcessId + " --config-dir \"" + InstallInfo.InstallDir + "\"",
                            WorkingDirectory = InstallInfo.InstallDir,
                            CreateNoWindow = true,
                            UseShellExecute = false
                        }
                    };
                    temporaryServerProcess.Start();

                    // Esperar a que abra el puerto (máx. ~5 s)
                    for (int i = 0; i < 33 && !InstallInfo.IsPortInUse(); i++)
                        System.Threading.Thread.Sleep(150);
                }
            }
            catch {}
        }

        private void StopTemporaryServer()
        {
            if (temporaryServerProcess != null)
            {
                try
                {
                    if (!temporaryServerProcess.HasExited)
                    {
                        temporaryServerProcess.Kill();
                    }
                }
                catch {}
                temporaryServerProcess = null;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopTemporaryServer();
            base.OnFormClosing(e);
        }

        private async void ShowConfigLayout()
        {
            descLabel.Visible = false;
            mainSeparator.Visible = false;
            featuresHeader.Visible = false;
            featureList.Visible = false;
            progressBar.Visible = false;
            statusLabel.Visible = false;
            logBox.Visible = false;
            
            autostartCheck.Visible = false;
            configPanel.Visible = true;
            LoadCurrentTheme();

            installButton.Visible = false;
            uninstallButton.Location = new Point(20, 20);
            closeButton.Visible = true;
            closeButton.Enabled = true;
            closeButton.Location = new Point(415, 20);

            StartTemporaryServer();

            if (!isWebViewInitialized)
            {
                try
                {
                    string userDataFolder = Path.Combine(Path.GetTempPath(), "SpotifyLocalStreamlabsWebView2");
                    var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder);
                    await webViewPreview.EnsureCoreWebView2Async(env);
                    isWebViewInitialized = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al inicializar la vista previa web: " + ex.Message, "Error de Vista Previa", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            UpdatePreview(themeComboBox.SelectedIndex);
        }

        private void ShowInstallLayout()
        {
            descLabel.Visible = true;
            mainSeparator.Visible = true;
            featuresHeader.Visible = true;
            featureList.Visible = true;
            autostartCheck.Visible = true;
            configPanel.Visible = false;

            installButton.Visible = true;
            uninstallButton.Location = new Point(215, 20);
            closeButton.Visible = false;
            closeButton.Enabled = false;
        }
    }

    // Custom Rounded Progress Bar Control
    public class CustomProgressBar : Control
    {
        private int value = 0;
        public int Value
        {
            get => value;
            set
            {
                this.value = Math.Min(100, Math.Max(0, value));
                Invalidate();
            }
        }

        public CustomProgressBar()
        {
            this.DoubleBuffered = true;
            this.Size = new Size(520, 25);
            this.BackColor = Color.FromArgb(18, 18, 18);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Background track with rounded corners
            using (var bgBrush = new SolidBrush(Color.FromArgb(40, 40, 40)))
            {
                GraphicsExtensions.FillRoundedRectangle(g, bgBrush, 0, 0, this.Width, this.Height, 8);
            }

            // Foreground bar with horizontal gradient and rounded corners
            if (value > 0)
            {
                int progressWidth = (int)((value / 100.0) * this.Width);
                if (progressWidth > 16) // Enough width to draw arcs correctly
                {
                    using (var fillBrush = new LinearGradientBrush(
                        new Point(0, 0), new Point(this.Width, 0),
                        Color.FromArgb(29, 185, 84), Color.FromArgb(30, 215, 96)))
                    {
                        GraphicsExtensions.FillRoundedRectangle(g, fillBrush, 0, 0, progressWidth, this.Height, 8);
                    }
                }
                else if (progressWidth > 0)
                {
                    // Draw simple rect for small values to prevent path errors
                    using (var fillBrush = new SolidBrush(Color.FromArgb(29, 185, 84)))
                    {
                        g.FillRectangle(fillBrush, 0, 0, progressWidth, this.Height);
                    }
                }
            }

            // Centered Percentage Text
            string text = value + "%";
            using (var font = new Font("Segoe UI", 9, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.White))
            {
                Size textSize = TextRenderer.MeasureText(text, font);
                g.DrawString(text, font, brush, (this.Width - textSize.Width) / 2.0f, (this.Height - textSize.Height) / 2.0f);
            }
        }
    }

    // Helper for Rounded Shapes in GDI+
    public static class GraphicsExtensions
    {
        public static void FillRoundedRectangle(Graphics g, Brush brush, float x, float y, float width, float height, float radius)
        {
            using (var path = new GraphicsPath())
            {
                float r = radius * 2;
                if (r > width) r = width;
                if (r > height) r = height;

                path.AddArc(x, y, r, r, 180, 90);
                path.AddArc(x + width - r, y, r, r, 270, 90);
                path.AddArc(x + width - r, y + height - r, r, r, 0, 90);
                path.AddArc(x, y + height - r, r, r, 90, 90);
                path.CloseAllFigures();
                g.FillPath(brush, path);
            }
        }
    }
}
