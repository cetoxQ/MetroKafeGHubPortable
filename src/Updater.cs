// GitHub Releases uzerinden kendini guncelleme.
// Release'te "MetroKafeGHubPortable.zip" adli bir dosya beklenir (icinde iki exe).
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Web.Script.Serialization;

namespace MetroKafeGHub
{
    static class Updater
    {
        public const string AssetName = "MetroKafeGHubPortable.zip";

        public class ReleaseInfo
        {
            public Version Version;
            public string ZipUrl;
        }

        public static Version CurrentVersion { get { return Assembly.GetExecutingAssembly().GetName().Version; } }

        static WebClient Client()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "MetroKafeGHubPortable";
            return wc;
        }

        public static ReleaseInfo GetLatest()
        {
            string json;
            try
            {
                using (var wc = Client())
                    json = wc.DownloadString("https://api.github.com/repos/" + Core.GitHubRepo + "/releases/latest");
            }
            catch (WebException ex)
            {
                var resp = ex.Response as HttpWebResponse;
                if (resp != null && resp.StatusCode == HttpStatusCode.NotFound) return null;   // henuz release yok
                throw;
            }
            var root = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
            Version v;
            if (!Version.TryParse(((string)root["tag_name"]).TrimStart('v', 'V'), out v)) return null;
            var info = new ReleaseInfo { Version = v };
            var assets = root["assets"] as object[] ?? (root["assets"] as ArrayList ?? new ArrayList()).ToArray();
            foreach (Dictionary<string, object> a in assets)
                if (string.Equals((string)a["name"], AssetName, StringComparison.OrdinalIgnoreCase))
                    info.ZipUrl = (string)a["browser_download_url"];
            return info;
        }

        // Calisan exe silinemez ama yeniden adlandirilabilir: eskiyi .old yapip yenisini yerine koyar.
        public static void Apply(ReleaseInfo rel)
        {
            var tmp = Path.Combine(Path.GetTempPath(), "MetroKafeGHubUpdate");
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            Directory.CreateDirectory(tmp);
            var zip = Path.Combine(tmp, AssetName);
            using (var wc = Client()) wc.DownloadFile(rel.ZipUrl, zip);
            var extract = Path.Combine(tmp, "x");
            ZipFile.ExtractToDirectory(zip, extract);

            foreach (var src in Directory.GetFiles(extract, "*.exe", SearchOption.AllDirectories))
            {
                var dst = Path.Combine(Core.Root, Path.GetFileName(src));
                if (File.Exists(dst))
                {
                    var old = dst + ".old";
                    if (File.Exists(old)) File.Delete(old);
                    File.Move(dst, old);
                }
                File.Copy(src, dst);
            }
            Directory.Delete(tmp, true);
        }

        public static void CleanupOldFiles()
        {
            foreach (var f in Directory.GetFiles(Core.Root, "*.exe.old"))
                try { File.Delete(f); } catch { }
        }
    }
}
