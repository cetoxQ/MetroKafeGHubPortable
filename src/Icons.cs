using System.Drawing;
using System.Windows.Forms;

namespace MetroKafeGHub
{
    static class Icons
    {
        // exe'ye /win32icon:src\app.ico ile gomulu MetroKafe ikonu.
        public static Icon App()
        {
            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { return SystemIcons.Application; }
        }
    }
}
