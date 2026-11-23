using System.Drawing;
using System.IO;

namespace MetroKafeGHub
{
    static class Icons
    {
        // Logitech ikonu dagitilmiyor; paketteki lghub.exe'den calisma aninda alinir.
        public static Icon App()
        {
            try
            {
                var exe = Path.Combine(Core.Root, @"App\LGHUB\lghub.exe");
                if (File.Exists(exe)) return Icon.ExtractAssociatedIcon(exe);
            }
            catch { }
            return SystemIcons.Application;
        }
    }
}
