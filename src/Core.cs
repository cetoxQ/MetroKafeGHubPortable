// MetroKafe G Hub Portable - ortak islemler (baslatma, durdurma, paketleme, durum).
// C# 5 / .NET Framework 4.x: Windows'un kendi csc.exe'si ile derlenir.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32.SafeHandles;

namespace MetroKafeGHub
{
    public class LinkInfo
    {
        public string PackagePath;  // paket icindeki klasor
        public string SystemPath;   // G HUB'in bekledigi sistem yolu
    }

    public class DriverInfo
    {
        public string Name;
        public string Status;       // "Calisiyor", "Durdu", "Yuklu degil"
    }

    public static class Core
    {
        public const string AppName = "MetroKafe G Hub Portable";
        public const string ServiceName = "LGHUBUpdaterService";
        public const string GitHubRepo = "cetoxQ/MetroKafeGHubPortable";
        public static readonly string[] DriverNames = { "logi_joy_bus_enum", "logi_joy_vir_hid", "logi_joy_xlcore" };
        static readonly string[] ProcessNames = { "lghub", "lghub_agent", "lghub_system_tray", "lghub_updater", "logi_crashpad_handler" };

        public static string Root { get { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'); } }
        static string LogDir { get { return Path.Combine(Root, "Loglar"); } }
        static string StateFile { get { return Path.Combine(Root, "durum.txt"); } }

        public static string ProgramFilesGHub { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LGHUB"); } }

        public static List<LinkInfo> Links
        {
            get
            {
                return new List<LinkInfo>
                {
                    new LinkInfo { PackagePath = Path.Combine(Root, @"App\LGHUB"), SystemPath = ProgramFilesGHub },
                    new LinkInfo { PackagePath = Path.Combine(Root, @"ProgramData\LGHUB"),
                                   SystemPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LGHUB") },
                    new LinkInfo { PackagePath = Path.Combine(Root, @"UserData\LGHUB"),
                                   SystemPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LGHUB") },
                };
            }
        }

        public static Action<string> Log = delegate { };

        public static void Write(string msg)
        {
            Log(msg);
            try
            {
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(Path.Combine(LogDir, "MetroKafeGHub.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine);
            }
            catch { }
        }

        // ---------- Junction yardimcilari ----------

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetFinalPathNameByHandle(SafeFileHandle h, StringBuilder buf, uint len, uint flags);

        public static bool IsJunction(string path)
        {
            if (!Directory.Exists(path)) return false;
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }

        // Junction'in gosterdigi gercek klasor (hedef yoksa null).
        public static string JunctionTarget(string path)
        {
            if (!IsJunction(path)) return null;
            const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
            using (var h = CreateFile(path, 0, 7, IntPtr.Zero, 3, FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero))
            {
                if (h.IsInvalid) return null;
                var sb = new StringBuilder(1024);
                if (GetFinalPathNameByHandle(h, sb, 1024, 0) == 0) return null;
                var s = sb.ToString();
                if (s.StartsWith(@"\\?\UNC\")) return @"\\" + s.Substring(8);
                if (s.StartsWith(@"\\?\")) return s.Substring(4);
                return s;
            }
        }

        static bool PointsIntoPackage(string path)
        {
            var t = JunctionTarget(path);
            return t != null && t.StartsWith(Root, StringComparison.OrdinalIgnoreCase);
        }

        // Directory.Delete(junction, false) sadece baglantiyi siler, hedefin icerigine dokunmaz.
        static void RemoveJunction(string path)
        {
            if (IsJunction(path)) Directory.Delete(path, false);
        }

        static void CreateJunction(string link, string target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(link));
            Run("cmd.exe", "/c mklink /J \"" + link + "\" \"" + target + "\"");
            if (!IsJunction(link)) throw new Exception("Baglanti olusturulamadi: " + link);
        }

        // ---------- Surec / servis yardimcilari ----------

        public static int Run(string exe, string args, int timeoutMs = 120000)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using (var p = Process.Start(psi))
            {
                var output = p.StandardOutput.ReadToEndAsync();
                var error = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return -1; }
                var text = (output.Result + error.Result).Trim();
                if (p.ExitCode != 0 && text.Length > 0) Write(Path.GetFileName(exe) + ": " + text);
                return p.ExitCode;
            }
        }

        public static void KillGHub()
        {
            foreach (var name in ProcessNames)
                foreach (var p in Process.GetProcessesByName(name))
                    try { p.Kill(); p.WaitForExit(5000); } catch { }
        }

        public static bool IsGHubUiRunning() { return Process.GetProcessesByName("lghub").Length > 0; }

        static ServiceController FindService(string name)
        {
            return ServiceController.GetServices().FirstOrDefault(s => s.ServiceName.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public static string ServiceStatus()
        {
            var s = FindService(ServiceName);
            if (s == null) return "Yuklu degil";
            return s.Status == ServiceControllerStatus.Running ? "Calisiyor" : "Durdu";
        }

        public static List<DriverInfo> DriverStatus()
        {
            var devices = ServiceController.GetDevices();
            return DriverNames.Select(n =>
            {
                var d = devices.FirstOrDefault(x => x.ServiceName.Equals(n, StringComparison.OrdinalIgnoreCase));
                var st = d == null ? "Yuklu degil" : (d.Status == ServiceControllerStatus.Running ? "Calisiyor" : "Durdu");
                return new DriverInfo { Name = n, Status = st };
            }).ToList();
        }

        static void DeleteService()
        {
            var s = FindService(ServiceName);
            if (s == null) return;
            try { if (s.Status != ServiceControllerStatus.Stopped) { s.Stop(); s.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15)); } }
            catch { }
            Run("sc.exe", "delete " + ServiceName);
            // "Silinmek uzere isaretli" durumdan cikmasini bekle.
            for (int i = 0; i < 20 && FindService(ServiceName) != null; i++) Thread.Sleep(500);
            Write("Servis kaldirildi.");
        }

        static void CreateAndStartService()
        {
            if (FindService(ServiceName) != null)
                throw new Exception(ServiceName + " silinmeyi bekliyor. PC'yi yeniden baslatip tekrar deneyin.");
            var exe = Path.Combine(ProgramFilesGHub, "lghub_updater.exe");
            int rc = Run("sc.exe", "create " + ServiceName + " binPath= \"\\\"" + exe + "\\\" --run-as-service\" start= demand DisplayName= \"LGHUB Updater Service\"");
            if (rc != 0) throw new Exception("Servis olusturulamadi (sc cikis kodu " + rc + ").");
            using (var s = new ServiceController(ServiceName))
            {
                s.Start();
                s.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            }
            Write("Servis baslatildi.");
        }

        // Depot manifest'lerindeki surucu kurucularini calistirir (G HUB updater'in yaptigi gibi).
        static void RunDepotExtensions(string kind)
        {
            var depotsRoot = Path.Combine(Links[1].SystemPath, "depots");
            if (!Directory.Exists(depotsRoot)) return;
            var depot = new DirectoryInfo(depotsRoot).GetDirectories()
                .OrderBy(d => { long n; return long.TryParse(d.Name, out n) ? n : 0; }).LastOrDefault();
            if (depot == null) return;
            Directory.CreateDirectory(LogDir);
            var json = new JavaScriptSerializer();
            foreach (var manifest in depot.GetDirectories().Select(d => Path.Combine(d.FullName, "manifest.json")).Where(File.Exists))
            {
                ArrayList exts;
                try
                {
                    var root = (Dictionary<string, object>)json.DeserializeObject(File.ReadAllText(manifest));
                    var installer = root.ContainsKey("installer") ? root["installer"] as Dictionary<string, object> : null;
                    if (installer == null || !installer.ContainsKey("extensions")) continue;
                    exts = ToList(installer["extensions"]);
                }
                catch { continue; }

                foreach (Dictionary<string, object> ext in exts)
                {
                    if (!ext.ContainsKey("autoInstall") || !(ext["autoInstall"] is bool) || !(bool)ext["autoInstall"]) continue;
                    if (!ext.ContainsKey(kind)) continue;
                    var spec = (Dictionary<string, object>)ext[kind];
                    var rel = ((string)spec["pipelineUri"]).Replace("pipeline://", "").Replace('/', '\\');
                    var exe = Path.Combine(depot.FullName, rel);
                    if (!File.Exists(exe)) continue;
                    var name = (string)ext["name"];
                    var log = Path.Combine(LogDir, name + "_" + kind + ".log");
                    var args = string.Join(" ", ToList(spec["arguments"]).Cast<object>().Select(a => (string)a)) + " \"" + log + "\"";
                    Write(name + ": " + (kind == "installer" ? "surucu kuruluyor" : "surucu kaldiriliyor"));
                    int rc = Run(exe, args);
                    if (rc != 0) Write(name + " cikis kodu " + rc + " (log: " + log + ")");
                }
            }
        }

        static ArrayList ToList(object o)
        {
            var arr = o as object[];
            return arr != null ? new ArrayList(arr) : (o as ArrayList ?? new ArrayList());
        }

        // ---------- Ana islemler ----------

        public static bool HasRealInstall()
        {
            return Directory.Exists(ProgramFilesGHub) && !IsJunction(ProgramFilesGHub);
        }

        public static bool PackageReady()
        {
            return Links.All(l => Directory.Exists(l.PackagePath));
        }

        public static bool IsStarted()
        {
            return Links.All(l => PointsIntoPackage(l.SystemPath)) && ServiceStatus() == "Calisiyor";
        }

        public static void Start()
        {
            if (!PackageReady()) throw new Exception("Paket eksik (App, ProgramData, UserData). Once paket olusturun.");
            if (HasRealInstall()) throw new Exception("Bu PC'de G HUB zaten kurulu (" + ProgramFilesGHub + ").");

            if (IsStarted())
            {
                Write("Zaten hazir, sadece arayuz aciliyor.");
                LaunchUi();
                return;
            }

            KillGHub();
            DeleteService();

            var backups = new List<string>();
            foreach (var l in Links)
            {
                RemoveJunction(l.SystemPath);
                if (Directory.Exists(l.SystemPath))
                {
                    // Eski bir G HUB kalintisi varsa yedekle, Durdur'da geri koy.
                    var bak = l.SystemPath + ".portable-yedek";
                    if (Directory.Exists(bak)) throw new Exception(l.SystemPath + " ve " + bak + " ikisi birden var; elle kontrol edin.");
                    Directory.Move(l.SystemPath, bak);
                    backups.Add(bak);
                    Write("Mevcut klasor yedeklendi: " + bak);
                }
                CreateJunction(l.SystemPath, l.PackagePath);
                Write("Baglandi: " + l.SystemPath);
            }
            File.WriteAllLines(StateFile, backups);

            CreateAndStartService();
            RunDepotExtensions("installer");
            LaunchUi();
            Write("G HUB baslatildi.");
        }

        static void LaunchUi()
        {
            var tray = Path.Combine(ProgramFilesGHub, @"system_tray\lghub_system_tray.exe");
            if (File.Exists(tray) && Process.GetProcessesByName("lghub_system_tray").Length == 0)
                Process.Start(new ProcessStartInfo(tray, "--minimized") { UseShellExecute = false });
            if (!IsGHubUiRunning())
                Process.Start(new ProcessStartInfo(Path.Combine(ProgramFilesGHub, "lghub.exe")) { UseShellExecute = false });
        }

        public static void Stop(bool removeDrivers)
        {
            Write("G HUB kapatiliyor...");
            KillGHub();
            // Surucu kaldirici depot'ta; junction'lar kalkmadan once calistirilmali.
            if (removeDrivers && PointsIntoPackage(Links[1].SystemPath)) RunDepotExtensions("uninstaller");
            DeleteService();
            KillGHub();

            foreach (var l in Links)
                if (PointsIntoPackage(l.SystemPath)) { RemoveJunction(l.SystemPath); Write("Baglanti kaldirildi: " + l.SystemPath); }

            if (File.Exists(StateFile))
            {
                foreach (var bak in File.ReadAllLines(StateFile).Where(s => s.Length > 0 && Directory.Exists(s)))
                {
                    var orig = bak.Substring(0, bak.Length - ".portable-yedek".Length);
                    if (!Directory.Exists(orig)) { Directory.Move(bak, orig); Write("Yedek geri yuklendi: " + orig); }
                }
                File.Delete(StateFile);
            }
            Write("Tamam. Bu PC'de G HUB izi kalmadi; ayarlar pakette.");
        }

        // Kurulu G HUB'dan paketin klasorlerini (App, ProgramData, UserData) bu klasore kopyalar.
        public static void CreatePackage()
        {
            if (!HasRealInstall()) throw new Exception("Paket olusturmak icin bu PC'de G HUB kurulu olmali.");
            foreach (var l in Links)
            {
                if (!Directory.Exists(l.SystemPath)) throw new Exception(l.SystemPath + " bulunamadi.");
                Write("Kopyalaniyor: " + l.SystemPath);
                var exclude = l.SystemPath == Links[1].SystemPath ? " /XD \"" + Path.Combine(l.SystemPath, "cache") + "\"" : "";
                int rc = Run("robocopy.exe", "\"" + l.SystemPath + "\" \"" + l.PackagePath + "\" /E /COPY:DAT /R:1 /W:1 /NFL /NDL /NJH /NP" + exclude, 30 * 60 * 1000);
                if (rc >= 8 || rc < 0) throw new Exception("Kopyalama hatasi (robocopy " + rc + "): " + l.SystemPath);
            }
            // installation.json eski kullanicinin yollarini tutuyor; updater ilk acilista yeniden olusturur.
            var inst = Path.Combine(Links[1].PackagePath, "installation.json");
            if (File.Exists(inst)) File.Delete(inst);
            Write("Paket hazir: " + Root);
        }
    }
}
