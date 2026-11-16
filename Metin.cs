using System.Globalization;
using System.Reflection;
using CounterStrikeSharp.API.Modules.Utils;

namespace YccAdmin;

// Oyun motoruna dokunmayan saf yardımcılar; test/ altındaki deneme bunları doğrudan çağırır.
public static class Metin
{
    // Ceza süresi dakika; 0 = kalıcı
    public static string Sure(int dakika) => dakika switch
    {
        <= 0 => "kalıcı",
        < 60 => $"{dakika} dakika",
        < 1440 when dakika % 60 == 0 => $"{dakika / 60} saat",
        < 1440 => $"{dakika / 60} saat {dakika % 60} dakika",
        _ when dakika % 1440 == 0 => $"{dakika / 1440} gün",
        _ => $"{dakika / 1440} gün {dakika % 1440 / 60} saat",
    };

    // "60", "1h"/"2s" (saat), "3d"/"3g" (gün), "30m"/"30dk" → dakika. Okunamazsa null.
    public static int? DakikaOku(string s)
    {
        s = s.Trim().ToLowerInvariant();
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var d)) return d >= 0 ? d : null;
        foreach (var (son, carpan) in new[] { ("dk", 1), ("m", 1), ("sa", 60), ("h", 60), ("s", 60), ("gün", 1440), ("g", 1440), ("d", 1440) })
            if (s.EndsWith(son) && int.TryParse(s[..^son.Length], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 0)
                return n * carpan;
        return null;
    }

    // 7656119xxxxxxxxxx biçiminde SteamID64 mü
    public static bool SteamId64(string s, out ulong id) =>
        ulong.TryParse(s.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out id) && id >= 76561190000000000UL && id < 76561299999999999UL;

    // css_ban <hedef> [süre] [sebep...]: hedeften sonraki argümanları süre + sebep olarak ayırır.
    // Süre yazılmadıysa (ilk kelime sayı değilse) varsayılan süre kullanılır ve o kelime sebebin başıdır.
    public static (int dakika, string sebep) SureVeSebep(IReadOnlyList<string> arg, int varsayilanDakika, string varsayilanSebep)
    {
        if (arg.Count == 0) return (varsayilanDakika, varsayilanSebep);
        var d = DakikaOku(arg[0]);
        var kalan = d == null ? arg : arg.Skip(1).ToList();
        var sebep = string.Join(' ', kalan.Select(Tirnaksiz)).Trim();
        return (d ?? varsayilanDakika, sebep == "" ? varsayilanSebep : Kisalt(sebep, 200));
    }

    public static string Tirnaksiz(string s)
    {
        s = s.Trim();
        return s.Length >= 2 && s[0] == '"' && s[^1] == '"' ? s[1..^1] : s;
    }

    public static string Kisalt(string s, int n) => s.Length <= n ? s : s[..n];

    // Panelin css_players ayrıştırıcısının beklediği satır (panel/lib/rcon.js):
    // • [#263] "Ad" (IP Address: "1.2.3.4" SteamID64: "7656...")
    public static string OyuncuSatiri(int userId, string isim, string ip, ulong steamId) =>
        $"• [#{userId}] \"{isim.Replace("\"", "'")}\" (IP Address: \"{ip}\" SteamID64: \"{steamId}\")";

    public static string IpYalin(string? ip) => string.IsNullOrEmpty(ip) ? "" : ip.Split(':')[0];

    // css_cvar ile hiçbir adminin değiştiremeyeceği ayarlar (sadece sunucu konsolu)
    public static bool TehlikeliCvar(string ad) =>
        ad.Equals("rcon_password", StringComparison.OrdinalIgnoreCase) || ad.Equals("sv_setsteamaccount", StringComparison.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> Renkler = BuildRenkler();
    private static Dictionary<string, string> BuildRenkler()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in typeof(ChatColors).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (f.GetValue(null) is char c) d["{" + f.Name + "}"] = c.ToString();
        if (d.TryGetValue("{Grey}", out var g)) d["{GRAY}"] = g;
        return d;
    }

    public static string Renk(string s)
    {
        if (string.IsNullOrEmpty(s) || !s.Contains('{')) return s;
        foreach (var (k, v) in Renkler) s = s.Replace(k, v, StringComparison.OrdinalIgnoreCase);
        return s;
    }
}
