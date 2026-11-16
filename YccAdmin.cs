using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;

namespace YccAdmin;

// YCC admin sistemi (kullanıcı 2026-11-15: 2. aşama, SimpleAdmin + FunCommands yerine kendi eklentimiz).
// Komut adları ve biçimleri SimpleAdmin'le aynı: panel (oyun.js, server.js), KickGuard/SwearGuard/ReklamGuard ve
// YccTemel kısayolları değişmeden çalışır. Hedef: #userid, @all/@ct/@t/@dead/@alive/@me, SteamID64 ya da isim parçası.
// Adminler CounterStrikeSharp'ın configs/admins.json dosyasından (panel yönetiyor).
public partial class YccAdmin : BasePlugin, IPluginConfig<Ayarlar>
{
    public override string ModuleName => "YccAdmin";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "Claude";
    public override string ModuleDescription => "YCC admin sistemi: ceza, oyuncu ve harita komutları";

    public Ayarlar Config { get; set; } = new();
    public void OnConfigParsed(Ayarlar config) => Config = config;
    private Veritabani _db = null!;

    public override void Load(bool hotReload)
    {
        _db = new Veritabani(Config.Veritabani, Config.SunucuId);
        CezalarKur(hotReload);
        KomutlarKur();
        TusMenuKur();
        MenuKur();
    }

    // ---- ortak yardımcılar

    private static string Ad(CCSPlayerController? p) => p?.PlayerName ?? "Konsol";
    private static ulong Id(CCSPlayerController? p) => p?.SteamID ?? 0;
    private static bool Gercek(CCSPlayerController? p) => p != null && p.IsValid && !p.IsBot && !p.IsHLTV;

    // Konsol (p == null) her şeyi yapabilir
    private static bool Yetkili(CCSPlayerController? p, string yetki) => p == null || AdminManager.PlayerHasPermissions(p, yetki);

    private bool YetkiVar(CCSPlayerController? p, CommandInfo info, string yetki)
    {
        if (Yetkili(p, yetki)) return true;
        Cevap(p, info, "{RED}Bu komut için yetkin yok.");
        return false;
    }

    private void Yaz(CCSPlayerController p, string s) => p.PrintToChat(" " + Metin.Renk(Config.On + " " + s));
    private void Duyur(string s) => Server.PrintToChatAll(" " + Metin.Renk(Config.On + " " + s));

    // Oyuncuya sohbetten, konsola/panele düz yazı
    private void Cevap(CCSPlayerController? p, CommandInfo info, string s)
    {
        if (p != null && p.IsValid) Yaz(p, s);
        else info.ReplyToCommand(System.Text.RegularExpressions.Regex.Replace(s, @"\{[A-Za-z]+\}", ""));
    }

    private static List<string> Argumanlar(CommandInfo info, int bas) =>
        Enumerable.Range(bas, Math.Max(0, info.ArgCount - bas)).Select(info.GetArg).ToList();

    // Hedefleri çözer; bulunamazsa ya da dokunulmazlığı yüksekse açıklama yazar. tek=true: birden çok eşleşme reddedilir.
    private List<CCSPlayerController> Hedefler(CCSPlayerController? p, CommandInfo info, int index, bool tek = false)
    {
        var arg = info.GetArg(index);
        if (arg == "") { Cevap(p, info, "{RED}Hedef yazılmadı (#userid, isim, SteamID64, @all, @t, @ct)."); return new(); }
        List<CCSPlayerController> l;
        try { l = info.GetArgTargetResult(index).Players.Where(Gercek).ToList(); }
        catch { l = new(); }
        if (l.Count == 0) { Cevap(p, info, $"{{RED}}Oyuncu bulunamadı: {arg}"); return l; }
        var izinli = l.Where(h => p == null || AdminManager.CanPlayerTarget(p, h)).ToList();
        if (izinli.Count < l.Count) Cevap(p, info, "{RED}Bazı hedeflerin yetkisi seninkinden yüksek, onlara dokunulmadı.");
        if (tek && izinli.Count > 1) { Cevap(p, info, $"{{RED}}Birden fazla oyuncu eşleşti ({izinli.Count}), daha net yaz ya da #userid kullan."); return new(); }
        return izinli;
    }

    // Arka planda veritabanı işi; hata olursa konsola yazar, oyunu durdurmaz
    private static void Arka(string ne, Func<Task> f) => Task.Run(async () =>
    {
        try { await f(); }
        catch (Exception e) { Console.WriteLine($"[YccAdmin] {ne} başarısız: {e.Message}"); }
    });
}
