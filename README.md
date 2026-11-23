<p align="center"><img src="docs/logo.png" width="200" alt="MetroKafe E-Spor"></p>

# MetroKafe G Hub Portable

Logitech G HUB'ı kurulum yapmadan, bir klasörden (USB bellek vb.) çalıştırır. İşiniz bitince bilgisayarda iz bırakmaz; profiller, makrolar ve ayarlar pakette kalır.

## Neden?

Logitech'in sitesinden G HUB'ın son sürümünü indirip kurduğunuzda, **kurulumdan sonra bilgisayarı yeniden başlatmanız gerekiyor**. Kafede, turnuvada ya da başka birinin bilgisayarında bu çok can sıkıcı.

MetroKafe G Hub Portable ile **yeniden başlatma yok**: `MetroKafeGHub.exe`'ye tıklayın, ~10 saniye içinde G HUB açılır.

## İndirme

[Releases](https://github.com/cetoxQ/MetroKafeGHubPortable/releases/latest) sayfasında iki dosya var:

| Dosya | İçinde ne var |
|---|---|
| `MetroKafeGHubPortable-Tam-Paket.zip` | Hazır paket: iki exe + G HUB 2023.10.493246 + MetroKafe ayarları. Aç, çalıştır. (~450 MB, açılınca ~1 GB) |
| `MetroKafeGHubPortable.zip` | Sadece iki exe. Kendi paketinizi oluşturmak için. |

Zip'i boş bir klasöre açın (NTFS biçimli disk/USB önerilir).

## Kullanım

| Dosya | Görevi |
|---|---|
| `MetroKafeGHub.exe` | Pencere açmadan G HUB'ı başlatır. Sağ altta "Logitech G Hub başlatılıyor..." bildirimi çıkar. |
| `MetroKafeGHubService.exe` | Servis, sürücü ve bağlantı durumunu gösterir. Servis/sürücüleri kaldırır, paket oluşturur, açılışta güncelleme denetler. |

- **Başlatmak:** `MetroKafeGHub.exe`'ye çift tıklayın.
- **Kaldırmak:** `MetroKafeGHubService.exe` → **Servis ve Sürücüleri Kaldır**. G HUB kapanır; servis, sürücü ve bağlantılar silinir.

Her iki program da yönetici yetkisiyle çalışır.

## Kendi G HUB'ınızı kullanmak

Kendi profillerinizi, makrolarınızı ya da farklı bir G HUB sürümünü kullanmak istiyorsanız:

1. Paket klasöründe **`MetroKafeGHub.exe` ve `MetroKafeGHubService.exe` dışındaki her şeyi silin** (`App`, `ProgramData`, `UserData`, `Loglar`).
2. G HUB'ın **kurulu olduğu** ve istediğiniz gibi ayarladığınız bir bilgisayarda `MetroKafeGHubService.exe` → **Paket Oluştur**.
3. G HUB ve ayarlarınız klasöre kopyalanır. Artık bu klasör sizin taşınabilir G HUB'ınız.

## Nasıl çalışır

G HUB dosyalarını hep `C:\Program Files\LGHUB`, `C:\ProgramData\LGHUB` ve `%LOCALAPPDATA%\LGHUB` altında arar. Program bu üç yere, paketteki klasörleri gösteren bağlantılar (junction) açar, G HUB servisini ve sanal klavye sürücüsünü geçici olarak kurar. Sürücü imzalı ve "tak-çalıştır" kurulduğu için yeniden başlatma gerekmez.

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
set OPTS=/nologo /codepage:65001 /optimize /target:winexe /win32manifest:src\app.manifest /win32icon:src\app.ico

%CSC% %OPTS% %REFS% /out:dist\MetroKafeGHub.exe src\Core.cs src\Icons.cs src\AssemblyInfo.cs src\Launcher.cs
%CSC% %OPTS% %REFS% /out:dist\MetroKafeGHubService.exe src\Core.cs src\Icons.cs src\AssemblyInfo.cs src\Manager.cs src\Updater.cs
```

Yeni sürüm yayınlamak için `src\AssemblyInfo.cs` içindeki sürümü artırın, iki exe'yi `MetroKafeGHubPortable.zip` adıyla sıkıştırıp `vX.Y.Z` etiketli bir release'e ekleyin. Program açılışta bu release'i denetler ve sadece iki exe'yi günceller; paket içeriğine dokunmaz.

---

Logitech, Logitech G ve G HUB, Logitech International S.A.'nın ticari markalarıdır. Bu proje Logitech ile bağlantılı değildir; G HUB dosyaları Logitech'e aittir.
