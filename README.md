# MetroKafe G Hub Portable

Logitech G HUB'ı kurulum yapmadan, bir klasörden (USB bellek vb.) çalıştırır. İşiniz bitince bilgisayarda iz bırakmaz; profiller, makrolar ve ayarlar pakette kalır.

> Logitech G HUB dosyaları bu depoda **yok**. Paketi, G HUB kurulu bir bilgisayardan kendiniz oluşturursunuz.

## Nasıl çalışır

G HUB dosyalarını hep `C:\Program Files\LGHUB`, `C:\ProgramData\LGHUB` ve `%LOCALAPPDATA%\LGHUB` altında arar. Program bu üç yere, paketteki klasörleri gösteren bağlantılar (junction) açar, G HUB servisini ve sanal klavye sürücüsünü geçici olarak kurar.

| Dosya | Görevi |
|---|---|
| `MetroKafeGHub.exe` | Pencere açmadan G HUB'ı başlatır. Sağ altta "Logitech G Hub başlatılıyor..." bildirimi gösterir. |
| `MetroKafeGHubService.exe` | Servis, sürücü ve bağlantı durumunu gösterir. Servis/sürücüleri kaldırır, paket oluşturur, açılışta güncelleme denetler. |

Her iki program da yönetici yetkisiyle çalışır.

## Kurulum

1. [Releases](https://github.com/cetoxQ/MetroKafeGHubPortable/releases) sayfasından `MetroKafeGHubPortable.zip` dosyasını indirip boş bir klasöre açın.
2. **G HUB kurulu** bir bilgisayarda `MetroKafeGHubService.exe` → **Paket Oluştur**. G HUB ve ayarlarınız klasöre kopyalanır (~1 GB).
3. Klasörü USB'ye ya da başka bilgisayara kopyalayın (NTFS önerilir).

## Kullanım

- **Başlatmak:** `MetroKafeGHub.exe`'ye çift tıklayın. İlk açılış ~10 saniye sürer.
- **Kaldırmak:** `MetroKafeGHubService.exe` → **Servis ve Sürücüleri Kaldır**. G HUB kapanır, servis, sürücü ve bağlantılar silinir.

Paket klasör yapısı:

```
MetroKafeGHub.exe
MetroKafeGHubService.exe
App\LGHUB\           -> C:\Program Files\LGHUB
ProgramData\LGHUB\   -> C:\ProgramData\LGHUB
UserData\LGHUB\      -> %LOCALAPPDATA%\LGHUB   (ayarlar)
Loglar\
```

## Notlar

- G HUB'ın kurulu olduğu bir bilgisayarda başlatma reddedilir; orada taşınabilir sürüme gerek yoktur.
- Logitech hesap girişi bilgisayara özel şifrelenir; yeni bilgisayarda yeniden giriş gerekebilir.
- G HUB kendini güncellerse güncelleme paketin içine iner.
- Direksiyon güç geri bildirimi (force feedback) sürücüsü kurulmaz.

## Derleme

Ek araç gerekmez; Windows'taki .NET Framework derleyicisi yeterli:

```
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set REFS=/r:System.ServiceProcess.dll /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Core.dll
set OPTS=/nologo /codepage:65001 /optimize /target:winexe /win32manifest:src\app.manifest

%CSC% %OPTS% %REFS% /out:dist\MetroKafeGHub.exe src\Core.cs src\Icons.cs src\AssemblyInfo.cs src\Launcher.cs
%CSC% %OPTS% %REFS% /out:dist\MetroKafeGHubService.exe src\Core.cs src\Icons.cs src\AssemblyInfo.cs src\Manager.cs src\Updater.cs
```

Yeni sürüm yayınlamak için `src\AssemblyInfo.cs` içindeki sürümü artırın, iki exe'yi `MetroKafeGHubPortable.zip` adıyla sıkıştırıp `vX.Y.Z` etiketli bir release'e ekleyin. Program açılışta bu release'i denetler.

---

Logitech, Logitech G ve G HUB, Logitech International S.A.'nın ticari markalarıdır. Bu proje Logitech ile bağlantılı değildir.
