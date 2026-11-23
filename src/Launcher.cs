// MetroKafeGHub.exe: pencere acmadan G HUB'i baslatir, sag altta bildirim balonu gosterir.
using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace MetroKafeGHub
{
    static class Launcher
    {
        [STAThread]
        static void Main()
        {
            bool first;
            using (new Mutex(true, @"Global\MetroKafeGHubLauncher", out first))
            {
                if (!first) return;   // ikinci tiklama: zaten calisiyor
                Application.EnableVisualStyles();
                using (var tray = new NotifyIcon())
                {
                    tray.Icon = Icons.App();
                    tray.Text = Core.AppName;
                    tray.Visible = true;
                    tray.ShowBalloonTip(15000, "Logitech G Hub", "Logitech G Hub başlatılıyor...", ToolTipIcon.Info);

                    Exception error = null;
                    var worker = new Thread(() =>
                    {
                        try { Core.Start(); } catch (Exception ex) { error = ex; Core.Write("HATA: " + ex.Message); }
                    });
                    worker.Start();
                    // Balonun gorunmesi icin mesaj dongusu, is bitene kadar donmeli.
                    while (worker.IsAlive) { Application.DoEvents(); Thread.Sleep(50); }

                    if (error == null)
                        tray.ShowBalloonTip(4000, "Logitech G Hub", "Logitech G Hub hazır.", ToolTipIcon.Info);
                    else
                        tray.ShowBalloonTip(10000, "Logitech G Hub başlatılamadı", error.Message, ToolTipIcon.Error);

                    var until = DateTime.Now.AddSeconds(error == null ? 4 : 10);
                    while (DateTime.Now < until) { Application.DoEvents(); Thread.Sleep(50); }
                    tray.Visible = false;
                }
            }
        }
    }
}
