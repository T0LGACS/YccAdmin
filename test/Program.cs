using System.Text.RegularExpressions;
using YccAdmin;

// YccAdmin saf yardımcılarının denemesi: dotnet run (bu klasörde)
var hata = 0;
void Esit(object? beklenen, object? gelen, string ne)
{
    if (Equals(beklenen, gelen)) return;
    hata++; Console.WriteLine($"HATA {ne}: beklenen [{beklenen}] gelen [{gelen}]");
}

Esit("kalıcı", Metin.Sure(0), "süre 0");
Esit("45 dakika", Metin.Sure(45), "süre dk");
Esit("2 saat", Metin.Sure(120), "süre saat");
Esit("1 saat 30 dakika", Metin.Sure(90), "süre saat+dk");
Esit("1 gün", Metin.Sure(1440), "süre gün");
Esit("15 gün", Metin.Sure(21600), "süre 15 gün");

Esit(60, Metin.DakikaOku("60"), "dakika sayı");
Esit(120, Metin.DakikaOku("2h"), "dakika h");
Esit(4320, Metin.DakikaOku("3d"), "dakika d");
Esit(30, Metin.DakikaOku("30dk"), "dakika dk");
Esit(null, Metin.DakikaOku("Kufur"), "kelime süre değil");
Esit(null, Metin.DakikaOku("-5"), "eksi süre yok");

// KickGuard: css_gag #{userid} {minutes} "{reason}" → argümanlar [60, "Kick isteme (2. uyari)"]
var (dk, sebep) = Metin.SureVeSebep(new[] { "60", "Kick isteme (2. uyari)" }, 60, "Sebep yok");
Esit(60, dk, "korumaların süresi"); Esit("Kick isteme (2. uyari)", sebep, "korumaların sebebi");
(dk, sebep) = Metin.SureVeSebep(new[] { "hile", "yapıyor" }, 60, "Sebep yok");
Esit(60, dk, "süresiz yazılınca varsayılan"); Esit("hile yapıyor", sebep, "süresiz sebep");
(dk, sebep) = Metin.SureVeSebep(Array.Empty<string>(), 30, "Sebep yok");
Esit((30, "Sebep yok"), (dk, sebep), "boş");
(dk, sebep) = Metin.SureVeSebep(new[] { "0" }, 60, "Sebep yok");
Esit(0, dk, "kalıcı ban");

Esit(true, Metin.SteamId64("76561197960287930", out var sid) && sid == 76561197960287930, "steamid64");
Esit(false, Metin.SteamId64("#12", out _), "userid steamid değil");
Esit(false, Metin.SteamId64("12345", out _), "kısa sayı steamid değil");

// Panelin ayrıştırıcısı (panel/lib/rcon.js) birebir
var satir = Metin.OyuncuSatiri(263, "Ali \"Veli\"", "1.2.3.4", 76561197960287930);
var m = Regex.Match(satir, @"\[#(\d+)\]\s+""(.*)""\s+\(IP Address:\s*""[^""]*""\s+SteamID64:\s*""(\d+)""\)");
Esit(true, m.Success, "panel oyuncu satırı");
Esit("263", m.Groups[1].Value, "panel userid"); Esit("Ali 'Veli'", m.Groups[2].Value, "panel isim"); Esit("76561197960287930", m.Groups[3].Value, "panel steamid");

Esit(true, Metin.TehlikeliCvar("RCON_PASSWORD"), "rcon_password korunur");
Esit(false, Metin.TehlikeliCvar("mp_timelimit"), "normal cvar serbest");
Esit("1.2.3.4", Metin.IpYalin("1.2.3.4:27005"), "ip");

Console.WriteLine(hata == 0 ? "TAMAM: tüm denemeler geçti" : $"{hata} deneme başarısız");
return hata == 0 ? 0 : 1;
