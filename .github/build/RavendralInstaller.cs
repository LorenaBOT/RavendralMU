using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RavendralInstaller
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
        }
    }

    public class InstallerForm : Form
    {
        TextBox pathBox;
        Button installButton;
        Button browseButton;
        Button cancelButton;
        CheckBox desktopShortcut;
        ProgressBar progress;
        Label status;
        Panel topBar;
        const string MainExe = "MU Client Ravendral.exe";
        const string Marker = "RAVENDRALPAYLOAD";

        public InstallerForm()
        {
            Text = "MU Ravendral - Instalador";
            Width = 850;
            Height = 540;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = Color.FromArgb(13, 15, 18);
            Font = new Font("Segoe UI", 10F);
            Icon = SystemIcons.Shield;

            topBar = new Panel();
            topBar.Dock = DockStyle.Top;
            topBar.Height = 155;
            topBar.Paint += HeaderPaint;
            Controls.Add(topBar);

            Label title = new Label();
            title.Text = "MU RAVENDRAL";
            title.ForeColor = Color.FromArgb(220, 174, 86);
            title.BackColor = Color.Transparent;
            title.Font = new Font("Georgia", 28F, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(42, 34);
            topBar.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "SEASON 6 EPISODE 3";
            subtitle.ForeColor = Color.Gainsboro;
            subtitle.BackColor = Color.Transparent;
            subtitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            subtitle.AutoSize = true;
            subtitle.Location = new Point(47, 90);
            topBar.Controls.Add(subtitle);

            Label intro = new Label();
            intro.Text = "Instala el cliente oficial de MU Ravendral.";
            intro.ForeColor = Color.FromArgb(190, 190, 190);
            intro.AutoSize = true;
            intro.Location = new Point(42, 180);
            Controls.Add(intro);

            Label pathLabel = new Label();
            pathLabel.Text = "Ubicación de instalación (se creará la carpeta MU Ravendral)";
            pathLabel.ForeColor = Color.WhiteSmoke;
            pathLabel.AutoSize = true;
            pathLabel.Location = new Point(42, 226);
            Controls.Add(pathLabel);

            pathBox = new TextBox();
            pathBox.Location = new Point(45, 254);
            pathBox.Width = 650;
            pathBox.Height = 30;
            pathBox.BackColor = Color.FromArgb(28, 31, 36);
            pathBox.ForeColor = Color.WhiteSmoke;
            pathBox.BorderStyle = BorderStyle.FixedSingle;
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (String.IsNullOrEmpty(pf)) pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            pathBox.Text = Path.Combine(pf, "MU Ravendral");
            Controls.Add(pathBox);

            browseButton = MakeButton("Examinar", 710, 252, 95, 32, false);
            browseButton.Click += BrowseClicked;
            Controls.Add(browseButton);

            desktopShortcut = new CheckBox();
            desktopShortcut.Text = "Crear acceso directo en el escritorio";
            desktopShortcut.Checked = true;
            desktopShortcut.ForeColor = Color.Gainsboro;
            desktopShortcut.AutoSize = true;
            desktopShortcut.Location = new Point(45, 307);
            Controls.Add(desktopShortcut);

            progress = new ProgressBar();
            progress.Location = new Point(45, 354);
            progress.Width = 760;
            progress.Height = 18;
            progress.Style = ProgressBarStyle.Continuous;
            Controls.Add(progress);

            status = new Label();
            status.Text = "Preparado para instalar";
            status.ForeColor = Color.FromArgb(170, 170, 170);
            status.AutoSize = true;
            status.Location = new Point(45, 383);
            Controls.Add(status);

            installButton = MakeButton("INSTALAR", 575, 438, 110, 38, true);
            installButton.Click += async delegate { await InstallAsync(); };
            Controls.Add(installButton);

            cancelButton = MakeButton("Cancelar", 695, 438, 110, 38, false);
            cancelButton.Click += delegate { Close(); };
            Controls.Add(cancelButton);
        }

        void HeaderPaint(object sender, PaintEventArgs e)
        {
            using (LinearGradientBrush b = new LinearGradientBrush(topBar.ClientRectangle,
                Color.FromArgb(34, 28, 20), Color.FromArgb(9, 11, 14), 12F))
                e.Graphics.FillRectangle(b, topBar.ClientRectangle);
            using (Pen p = new Pen(Color.FromArgb(150, 220, 174, 86), 1F))
                e.Graphics.DrawLine(p, 0, topBar.Height - 1, topBar.Width, topBar.Height - 1);
        }

        Button MakeButton(string text, int x, int y, int w, int h, bool gold)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, h);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = gold ? Color.FromArgb(220, 174, 86) : Color.FromArgb(80, 84, 90);
            b.BackColor = gold ? Color.FromArgb(93, 66, 28) : Color.FromArgb(30, 33, 38);
            b.ForeColor = gold ? Color.White : Color.Gainsboro;
            b.Cursor = Cursors.Hand;
            return b;
        }

        static string EnsureAppFolder(string selectedPath)
        {
            string full = Path.GetFullPath(selectedPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string leaf = Path.GetFileName(full);
            if (!String.Equals(leaf, "MU Ravendral", StringComparison.OrdinalIgnoreCase))
                full = Path.Combine(full, "MU Ravendral");
            return full;
        }

        void BrowseClicked(object sender, EventArgs e)
        {
            using (FolderBrowserDialog f = new FolderBrowserDialog())
            {
                f.Description = "Selecciona la carpeta de instalación de MU Ravendral";
                f.SelectedPath = pathBox.Text;
                if (f.ShowDialog(this) == DialogResult.OK)
                    pathBox.Text = EnsureAppFolder(f.SelectedPath);
            }
        }

        async Task InstallAsync()
        {
            string destination = pathBox.Text.Trim();
            if (String.IsNullOrEmpty(destination))
            {
                MessageBox.Show(this, "Selecciona una carpeta de instalación.", "MU Ravendral", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string fullDestination;
            try
            {
                fullDestination = EnsureAppFolder(destination);
            }
            catch
            {
                MessageBox.Show(this, "La ruta de instalación no es válida.", "MU Ravendral", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string pf64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            bool inProgramFiles =
                (!String.IsNullOrEmpty(pf64) && (fullDestination.Equals(pf64, StringComparison.OrdinalIgnoreCase) || fullDestination.StartsWith(pf64 + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) ||
                (!String.IsNullOrEmpty(pf86) && (fullDestination.Equals(pf86, StringComparison.OrdinalIgnoreCase) || fullDestination.StartsWith(pf86 + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)));

            if (inProgramFiles)
            {
                MessageBox.Show(this,
                    "MU Ravendral no puede instalarse dentro de Program Files ni Program Files (x86).\n\nUsa, por ejemplo:\nC:\\Games\\MU Ravendral",
                    "Ruta no permitida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            destination = fullDestination;

            installButton.Enabled = false;
            browseButton.Enabled = false;
            pathBox.Enabled = false;
            desktopShortcut.Enabled = false;
            cancelButton.Enabled = false;
            progress.Value = 0;
            status.Text = "Preparando archivos...";

            try
            {
                await Task.Run(() => ExtractAndVerify(destination));

                if (!File.Exists(Path.Combine(destination, MainExe)))
                    throw new Exception("No se encontró " + MainExe + " después de la instalación.");

                CreateShortcuts(destination, desktopShortcut.Checked);

                progress.Value = 100;
                status.Text = "Instalación completada correctamente.";
                DialogResult r = MessageBox.Show(this,
                    "MU Ravendral se ha instalado correctamente.\n\n¿Quieres iniciar el juego ahora?",
                    "MU Ravendral", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (r == DialogResult.Yes)
                {
                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
                    psi.FileName = Path.Combine(destination, MainExe);
                    psi.WorkingDirectory = destination;
                    psi.UseShellExecute = true;
                    System.Diagnostics.Process.Start(psi);
                }
                Close();
            }
            catch (Exception ex)
            {
                status.Text = "La instalación no se pudo completar.";
                MessageBox.Show(this, ex.Message, "MU Ravendral - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                installButton.Enabled = true;
                browseButton.Enabled = true;
                pathBox.Enabled = true;
                desktopShortcut.Enabled = true;
                cancelButton.Enabled = true;
            }
        }

        void ExtractAndVerify(string destination)
        {
            Directory.CreateDirectory(destination);
            string self = Assembly.GetExecutingAssembly().Location;
            string tmp = Path.Combine(Path.GetTempPath(), "RavendralPayload_" + Guid.NewGuid().ToString("N") + ".zip");

            long payloadLength;
            long payloadStart;
            using (FileStream fs = new FileStream(self, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (fs.Length < 24) throw new Exception("El instalador no contiene el paquete del cliente.");
                fs.Seek(-24, SeekOrigin.End);
                byte[] markerBytes = new byte[16];
                if (fs.Read(markerBytes, 0, 16) != 16) throw new Exception("No se pudo leer la firma del paquete.");
                string marker = Encoding.ASCII.GetString(markerBytes);
                if (marker != Marker) throw new Exception("Paquete interno no válido.");
                byte[] lenBytes = new byte[8];
                if (fs.Read(lenBytes, 0, 8) != 8) throw new Exception("No se pudo leer el tamaño del paquete.");
                payloadLength = BitConverter.ToInt64(lenBytes, 0);
                payloadStart = fs.Length - 24 - payloadLength;
                if (payloadLength <= 0 || payloadStart <= 0) throw new Exception("Paquete interno corrupto.");

                fs.Seek(payloadStart, SeekOrigin.Begin);
                using (FileStream o = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[1024 * 1024];
                    long remaining = payloadLength;
                    while (remaining > 0)
                    {
                        int toRead = (int)Math.Min(buffer.Length, remaining);
                        int read = fs.Read(buffer, 0, toRead);
                        if (read <= 0) throw new EndOfStreamException();
                        o.Write(buffer, 0, read);
                        remaining -= read;
                    }
                }
            }

            try
            {
                Dictionary<string, string> hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                using (ZipArchive z = ZipFile.OpenRead(tmp))
                {
                    ZipArchiveEntry manifest = z.GetEntry("_ravendral_manifest.sha256");
                    if (manifest == null) throw new Exception("Falta el manifiesto de integridad del cliente.");
                    using (StreamReader sr = new StreamReader(manifest.Open(), Encoding.UTF8))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            int bar = line.IndexOf('|');
                            if (bar > 0)
                                hashes[line.Substring(bar + 1)] = line.Substring(0, bar);
                        }
                    }

                    int total = z.Entries.Count - 1;
                    int done = 0;
                    string destRoot = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

                    foreach (ZipArchiveEntry entry in z.Entries)
                    {
                        if (entry.FullName == "_ravendral_manifest.sha256") continue;
                        string normalized = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                        string outPath = Path.GetFullPath(Path.Combine(destination, normalized));
                        if (!outPath.StartsWith(destRoot, StringComparison.OrdinalIgnoreCase))
                            throw new Exception("Ruta no válida dentro del paquete: " + entry.FullName);

                        if (String.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(outPath);
                        }
                        else
                        {
                            string dir = Path.GetDirectoryName(outPath);
                            if (!String.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                            using (Stream input = entry.Open())
                            using (FileStream output = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None))
                                input.CopyTo(output);

                            string expected;
                            if (!hashes.TryGetValue(entry.FullName, out expected))
                                throw new Exception("Archivo sin hash de verificación: " + entry.FullName);
                            string actual = Sha256(outPath);
                            if (!String.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                                throw new Exception("Archivo dañado durante la instalación: " + entry.FullName);
                        }

                        done++;
                        int pct = total <= 0 ? 0 : Math.Min(95, (done * 95) / total);
                        try { BeginInvoke((Action)(() => { progress.Value = pct; status.Text = "Instalando " + entry.FullName; })); } catch { }
                    }
                }
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }

        static string Sha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(path))
            {
                byte[] h = sha.ComputeHash(fs);
                StringBuilder sb = new StringBuilder(h.Length * 2);
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        static void CreateShortcuts(string destination, bool desktop)
        {
            string target = Path.Combine(destination, MainExe);
            string programs = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
            string startFolder = Path.Combine(programs, "MU Ravendral");
            Directory.CreateDirectory(startFolder);
            CreateShortcut(Path.Combine(startFolder, "MU Ravendral.lnk"), target, destination);

            if (desktop)
            {
                string desk = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                CreateShortcut(Path.Combine(desk, "MU Ravendral.lnk"), target, destination);
            }
        }

        static void CreateShortcut(string shortcutPath, string target, string workingDir)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            Type st = shortcut.GetType();
            st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
            st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDir });
            st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { target + ",0" });
            st.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "MU Ravendral" });
            st.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
    }
}
