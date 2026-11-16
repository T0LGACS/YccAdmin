# YccAdmin

Admin sistemi: ban, kick, susturma (gag = yazı, mute = ses, silence = ikisi), oyuncu komutları (slay, slap, respawn, hp, speed, freeze, noclip, god, team, bring/goto), harita değiştirme ve W/S/E/R tuşlarıyla gezilen !admin menüsü. Cezalar veritabanında tutulur.

> Admin system: bans, kicks, gag/mute/silence, player commands and a WASD-style !admin menu. Punishments are stored in MySQL (sa_bans / sa_mutes tables).

[YCC TEAM](https://yccteam.com.tr) CS2 sunucusu için sıfırdan yazıldı ve sunucuda her gün çalışıyor. Başka eklentilerden kod alınmadı.

## Gereken

- CS2 sunucusu, [Metamod:Source](https://www.sourcemm.net/) ve [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) (1.0.371 ve üstü)
- MySQL / MariaDB veritabanı

## Kurulum

1. Sunucuda [Metamod:Source](https://www.sourcemm.net/downloads.php?branch=dev) ve [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp/releases) (with-runtime sürümü) kurulu olmalı.
2. [.NET 10 SDK](https://dotnet.microsoft.com/download) ile derle: `dotnet build -c Release`
3. `bin/Release/net10.0/` içindeki her şeyi sunucuda `addons/counterstrikesharp/plugins/YccAdmin/` klasörüne kopyala.
4. Sunucuyu başlat. Ayar dosyası ilk açılışta `addons/counterstrikesharp/configs/plugins/YccAdmin/YccAdmin.json` olarak kendiliğinden oluşur; düzenleyip sunucuyu yeniden başlat (önce aşağıdaki **Veritabanı** bölümünü yap).

## Komutlar

Sohbette `!` ile, konsolda `css_` ile yazılır.

| Komut | Ne yapar |
|---|---|
| `!admin` | Admin menüsü (W/S gez, E seç, Ctrl geri, R kapat) |
| `!ban <oyuncu> [dk] [sebep]` | Banla (0 = kalıcı) |
| `!addban <steamid64> [dk] [sebep]` | Oyunda olmayanı banla |
| `!unban <steamid64>` | Banı kaldır |
| `!kick <oyuncu> [sebep]` | At |
| `!gag / !mute / !silence <oyuncu> [dk] [sebep]` | Yazı / ses / ikisini kapat |
| `!ungag / !unmute / !unsilence` | Kaldır |
| `!slay, !slap, !respawn, !hp, !speed, !freeze, !noclip, !god` | Oyuncu komutları |
| `!team <oyuncu> <t|ct|spec>` | Takım değiştir |
| `!map / !wsmap` | Harita değiştir |
| `!players, !who, !asay, !csay, !psay, !cvar` | Bilgi ve duyuru |

## Notlar

Adminler CounterStrikeSharp'ın `configs/admins.json` dosyasından okunur. Hedef olarak #userid, isim parçası, SteamID64 ya da @all/@t/@ct/@alive/@dead/@me yazılabilir.

## Veritabanı (MySQL / MariaDB) kurulumu

Bu eklenti kayıtlarını veritabanında tutar. Eklenti tabloları kendisi **oluşturmaz**; depodaki `sql/kurulum.sql` dosyası oluşturur.
Kullandığı tablolar: sa_bans, sa_mutes. YccAdmin, YccRank ve YccSkin aynı veritabanını paylaşabilir.

**1. MariaDB'yi kur** (Ubuntu / Debian; veritabanın zaten varsa bu adımı geç):

```bash
sudo apt update && sudo apt install -y mariadb-server
sudo systemctl enable --now mariadb
```

**2. Veritabanı ve kullanıcı oluştur.** `sudo mysql` ile gir ve şunları yaz (şifreyi kendin belirle, kimseyle paylaşma):

```sql
CREATE DATABASE IF NOT EXISTS cs2 CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
CREATE USER 'cs2'@'127.0.0.1' IDENTIFIED BY 'BURAYA-GUCLU-BIR-SIFRE';
CREATE USER 'cs2'@'localhost' IDENTIFIED BY 'BURAYA-GUCLU-BIR-SIFRE';
GRANT SELECT, INSERT, UPDATE, DELETE ON cs2.* TO 'cs2'@'127.0.0.1', 'cs2'@'localhost';
FLUSH PRIVILEGES;
```

Eklentiye okuma/yazma yetkisi yeter; tabloları sen kuruyorsun (3. adım).

**3. Tabloları kur** (depo klasöründeyken):

```bash
sudo mysql cs2 < sql/kurulum.sql
```

Tablo zaten varsa dokunulmaz (`CREATE TABLE IF NOT EXISTS`), mevcut verin silinmez.

**4. Ayar dosyasına bağlantıyı yaz.** Sunucu bir kez açılınca `addons/counterstrikesharp/configs/plugins/YccAdmin/YccAdmin.json` oluşur; içindeki `Veritabani` bölümünü doldur:

```json
"Veritabani": {
  "Sunucu": "127.0.0.1",
  "Port": 3306,
  "Kullanici": "cs2",
  "Sifre": "BURAYA-GUCLU-BIR-SIFRE",
  "Ad": "cs2"
}
```

Sonra CS2 sunucusunu yeniden başlat. Bağlanamazsa sunucu konsolunda `[YccAdmin]` ile başlayan bir hata satırı çıkar.

**Güvenlik:** 3306 portunu internete açma; veritabanı oyun sunucusuyla aynı makinedeyse `127.0.0.1` yeterli. Başka bir makinedeyse
kullanıcıyı o makinenin IP'siyle oluştur (`'cs2'@'1.2.3.4'`) ve güvenlik duvarında sadece o IP'ye izin ver. Ayar dosyası şifre içerir:
kimseyle paylaşma, GitHub'a koyma.

Ayardaki `SunucuId`, ceza kayıtlarına yazılan sunucu numarasıdır; aynı veritabanını birden fazla sunucu kullanıyorsa her birine farklı numara ver.

## Lisans

MIT. Kullanabilir, değiştirebilir, paylaşabilirsin; lisans dosyasını koruman yeterli.

Sunucu: **yccteam.com.tr** · Site: https://yccteam.com.tr
