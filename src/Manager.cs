// MetroKafeGHubService.exe: servis/surucu durumu, kaldirma, paket olusturma ve guncelleme.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace MetroKafeGHub
{
    static class ManagerProgram
    {
        [STAThread]
        static void Main()
        {
            Updater.CleanupOldFiles();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ManagerForm());
        }
    }

    class ManagerForm : Form
    {
        readonly ListView list = new ListView();
        readonly Button btnRefresh = new Button(), btnStart = new Button(), btnRemove = new Button(), btnPackage = new Button();
        readonly Button btnCheck = new Button(), btnUpdate = new Button();
        readonly Label lblUpdate = new Label();
        readonly TextBox log = new TextBox();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        Updater.ReleaseInfo pendingRelease;
        bool busy, restartAfterBusy;

        static readonly Dictionary<string, string> Tr = new Dictionary<string, string>
        {
            { "Calisiyor", "Çalışıyor" }, { "Durdu", "Durdu" }, { "Yuklu degil", "Yüklü değil" }
        };

        public ManagerForm()
        {
            Text = Core.AppName + " - Servis Yönetimi  v" + Updater.CurrentVersion.ToString(3);
            Icon = Icons.App();
            ClientSize = new Size(640, 520);
            MinimumSize = new Size(560, 480);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);

            list.View = View.Details;
            list.FullRowSelect = true;
            list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            list.Columns.Add("Bileşen", 400);
            list.Columns.Add("Durum", 200);
            list.SetBounds(12, 12, 616, 230);
            list.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            var buttons = new FlowLayoutPanel { Left = 12, Top = 250, Width = 616, Height = 36, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Setup(btnRefresh, "Yenile", 90, (s, e) => RefreshStatus());
            Setup(btnStart, "G HUB'ı Başlat", 120, (s, e) => RunBusy("Başlatılıyor...", Core.Start));
            Setup(btnRemove, "Servis ve Sürücüleri Kaldır", 190, OnRemove);
            Setup(btnPackage, "Paket Oluştur", 120, OnPackage);
            buttons.Controls.AddRange(new Control[] { btnRefresh, btnStart, btnRemove, btnPackage });

            var grp = new GroupBox { Text = "Güncelleme", Left = 12, Top = 292, Width = 616, Height = 62, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            lblUpdate.SetBounds(12, 26, 330, 22);
            Setup(btnCheck, "Denetle", 90, (s, e) => CheckUpdate(true));
            Setup(btnUpdate, "Güncelle", 100, OnUpdate);
            btnCheck.Location = new Point(400, 22);
            btnUpdate.Location = new Point(500, 22);
            btnUpdate.Enabled = false;
            grp.Controls.AddRange(new Control[] { lblUpdate, btnCheck, btnUpdate });

            log.Multiline = true;
            log.ReadOnly = true;
            log.ScrollBars = ScrollBars.Vertical;
            log.SetBounds(12, 362, 616, 146);
            log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            Controls.AddRange(new Control[] { list, buttons, grp, log });

            Core.Log = msg => { if (IsHandleCreated) BeginInvoke(new Action(() => AppendLog(msg))); };
            timer.Interval = 3000;
            timer.Tick += (s, e) => { if (!busy) RefreshStatus(); };
            Shown += (s, e) => { RefreshStatus(); timer.Start(); CheckUpdate(false); };
        }

        static void Setup(Button b, string text, int width, EventHandler click)
        {
            b.Text = text;
            b.Width = width;
            b.Height = 28;
            b.Click += click;
        }

        void AppendLog(string msg)
        {
            log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + msg + Environment.NewLine);
        }

        void AddRow(string name, string status, bool ok)
        {
            var item = new ListViewItem(new[] { name, status });
            item.UseItemStyleForSubItems = false;
            item.SubItems[1].ForeColor = ok ? Color.SeaGreen : Color.Firebrick;
            list.Items.Add(item);
        }

        void RefreshStatus()
        {
            list.BeginUpdate();
            list.Items.Clear();
            try
            {
                var svc = Core.ServiceStatus();
                AddRow("G HUB servisi (" + Core.ServiceName + ")", Tr[svc], svc == "Calisiyor");
                foreach (var d in Core.DriverStatus())
                    AddRow("Sürücü: " + d.Name, Tr[d.Status], d.Status == "Calisiyor");

                var real = Core.HasRealInstall();
                foreach (var l in Core.Links)
                {
                    string st; bool ok;
                    var target = Core.JunctionTarget(l.SystemPath);
                    if (target != null && target.StartsWith(Core.Root, StringComparison.OrdinalIgnoreCase)) { st = "Pakete bağlı"; ok = true; }
                    else if (target != null) { st = "Başka pakete bağlı"; ok = false; }
                    else if (Directory.Exists(l.SystemPath)) { st = "Yerel kurulum"; ok = real; }
                    else { st = "Bağlı değil"; ok = false; }
                    AddRow("Bağlantı: " + l.SystemPath, st, ok);
                }
                var ui = Core.IsGHubUiRunning();
                AddRow("G HUB uygulaması", ui ? "Açık" : "Kapalı", ui);

                btnPackage.Enabled = !busy && real;
                btnStart.Enabled = !busy && !real && Core.PackageReady();
                btnRemove.Enabled = !busy && !real;
            }
            catch (Exception ex) { AppendLog("Durum okunamadı: " + ex.Message); }
            finally { list.EndUpdate(); }
        }

        void RunBusy(string title, Action work)
        {
            if (busy) return;
            busy = true;
            foreach (var b in new[] { btnStart, btnRemove, btnPackage, btnUpdate, btnCheck }) b.Enabled = false;
            AppendLog(title);
            UseWaitCursor = true;
            new Thread(() =>
            {
                string err = null;
                try { work(); } catch (Exception ex) { err = ex.Message; }
                BeginInvoke(new Action(() =>
                {
                    busy = false;
                    UseWaitCursor = false;
                    btnCheck.Enabled = true;
                    btnUpdate.Enabled = pendingRelease != null;
                    var restart = restartAfterBusy;
                    restartAfterBusy = false;
                    if (err != null) { AppendLog("HATA: " + err); MessageBox.Show(this, err, Core.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    else if (restart)
                    {
                        System.Diagnostics.Process.Start(Application.ExecutablePath);
                        Close();
                        return;
                    }
                    RefreshStatus();
                }));
            }) { IsBackground = true }.Start();
        }

        void OnRemove(object sender, EventArgs e)
        {
            var r = MessageBox.Show(this,
                "G HUB kapatılacak; servis, sanal sürücü ve klasör bağlantıları bu PC'den kaldırılacak.\n\nAyarlarınız pakette kalır. Devam edilsin mi?",
                Core.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r == DialogResult.Yes) RunBusy("Kaldırılıyor...", () => Core.Stop(true));
        }

        void OnPackage(object sender, EventArgs e)
        {
            var r = MessageBox.Show(this,
                "Bu PC'deki kurulu G HUB ve ayarları şu klasöre kopyalanacak:\n" + Core.Root + "\n\nYaklaşık 1 GB yer kaplar. Devam edilsin mi?",
                Core.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r == DialogResult.Yes) RunBusy("Paket oluşturuluyor (birkaç dakika sürebilir)...", Core.CreatePackage);
        }

        void CheckUpdate(bool manual)
        {
            lblUpdate.Text = "Güncelleme denetleniyor...";
            btnCheck.Enabled = false;
            new Thread(() =>
            {
                Updater.ReleaseInfo rel = null; string err = null;
                try { rel = Updater.GetLatest(); } catch (Exception ex) { err = ex.Message; }
                BeginInvoke(new Action(() =>
                {
                    btnCheck.Enabled = !busy;
                    if (err != null) { lblUpdate.Text = "Denetlenemedi: " + err; return; }
                    if (rel != null && rel.Version > Updater.CurrentVersion && rel.ZipUrl != null)
                    {
                        pendingRelease = rel;
                        lblUpdate.Text = "Yeni sürüm var: v" + rel.Version.ToString(3);
                        lblUpdate.ForeColor = Color.DarkOrange;
                        btnUpdate.Enabled = !busy;
                        if (!manual) AppendLog("Yeni sürüm mevcut: v" + rel.Version.ToString(3));
                    }
                    else
                    {
                        pendingRelease = null;
                        lblUpdate.Text = "Güncel (v" + Updater.CurrentVersion.ToString(3) + ")";
                        lblUpdate.ForeColor = Color.SeaGreen;
                        btnUpdate.Enabled = false;
                    }
                }));
            }) { IsBackground = true }.Start();
        }

        void OnUpdate(object sender, EventArgs e)
        {
            var rel = pendingRelease;
            if (rel == null) return;
            restartAfterBusy = true;
            RunBusy("v" + rel.Version.ToString(3) + " indiriliyor...", () => Updater.Apply(rel));
        }
    }
}
