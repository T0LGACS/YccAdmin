using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;

namespace YccAdmin;

// !admin menüsü: oyuncu seç → işlem seç (→ süre seç). W/S/E/R tuşlarıyla gezilir (TusMenu.cs).
// Komutlarla aynı işlevleri çağırır (Banla, Sustur, Kickle...), yetkiler de aynı.
public partial class YccAdmin
{
    private static readonly (string ad, int dk)[] BanSureleri = { ("1 saat", 60), ("1 gün", 1440), ("1 hafta", 10080), ("Kalıcı", 0) };
    private static readonly (string ad, int dk)[] SusSureleri = { ("30 dakika", 30), ("1 saat", 60), ("1 gün", 1440), ("1 hafta", 10080) };
    private const string MenuSebep = "Admin menüsü";

    private void MenuKur()
    {
        AddCommand("css_admin", "Admin menüsü", (p, i) =>
        {
            if (!Gercek(p)) return;
            if (!Yetkili(p, "@css/generic")) { Yaz(p!, "{RED}Admin değilsin."); return; }
            AnaMenu(p!);
        });
    }

    private void AnaMenu(CCSPlayerController p)
    {
        var m = new TusMenu("YCC Admin");
        m.Ekle("Oyuncular", a => OyuncuMenu(a));
        m.Ekle("Harita değiştir", a => HaritaMenu(a), !Yetkili(p, "@css/changemap"));
        m.Ekle("Oyunu yeniden başlat", a => { Server.ExecuteCommand("mp_restartgame 1"); Duyur($"{Ad(a)} oyunu yeniden başlattı."); }, !Yetkili(p, "@css/changemap"));
        Ac(p, m);
    }

    private void OyuncuMenu(CCSPlayerController p)
    {
        var m = new TusMenu("Oyuncu seç");
        foreach (var h in Utilities.GetPlayers().Where(Gercek).OrderBy(x => x.PlayerName))
        {
            var hedef = h;
            m.Ekle($"{hedef.PlayerName} ({hedef.Team switch { CsTeam.Terrorist => "T", CsTeam.CounterTerrorist => "CT", _ => "İzl." }})",
                a => IslemMenu(a, hedef), !AdminManager.CanPlayerTarget(p, hedef));
        }
        Ac(p, m);
    }

    private void IslemMenu(CCSPlayerController p, CCSPlayerController h)
    {
        if (!Gercek(h)) { Yaz(p, "{RED}Oyuncu çıkmış."); return; }
        var m = new TusMenu(h.PlayerName);
        void Ekle(string ad, string yetki, Action f) => m.Ekle(ad, a => { if (Gercek(h)) f(); else Yaz(a, "{RED}Oyuncu çıkmış."); }, !Yetkili(p, yetki));

        Ekle("Kick", "@css/kick", () => Kickle(p, h, MenuSebep));
        Ekle("Ban", "@css/ban", () => SureMenu(p, h, "Ban süresi", BanSureleri, dk => Banla(p, h, dk, MenuSebep)));
        Ekle("Yazısını kapat (gag)", "@css/chat", () => SureMenu(p, h, "Gag süresi", SusSureleri, dk => Sustur(p, h, CezaTur.GAG, dk, MenuSebep)));
        Ekle("Sesini kapat (mute)", "@css/chat", () => SureMenu(p, h, "Mute süresi", SusSureleri, dk => Sustur(p, h, CezaTur.MUTE, dk, MenuSebep)));
        Ekle("İkisini kapat (silence)", "@css/chat", () => SureMenu(p, h, "Silence süresi", SusSureleri, dk => Sustur(p, h, CezaTur.SILENCE, dk, MenuSebep)));
        Ekle("Öldür (slay)", "@css/slay", () => { h.PlayerPawn.Value?.CommitSuicide(false, true); Duyur($"{Ad(p)}, {h.PlayerName} adlı oyuncuyu öldürdü."); });
        Ekle("Yeniden doğur", "@css/cheats", () => { if (h.Team is CsTeam.Terrorist or CsTeam.CounterTerrorist) h.Respawn(); });
        Ekle("T'ye al", "@css/kick", () => h.ChangeTeam(CsTeam.Terrorist));
        Ekle("CT'ye al", "@css/kick", () => h.ChangeTeam(CsTeam.CounterTerrorist));
        Ekle("İzleyiciye al", "@css/kick", () => h.ChangeTeam(CsTeam.Spectator));
        Ac(p, m);
    }

    private void SureMenu(CCSPlayerController p, CCSPlayerController h, string baslik, (string ad, int dk)[] sureler, Action<int> f)
    {
        var m = new TusMenu($"{baslik}: {h.PlayerName}");
        foreach (var (ad, dk) in sureler)
        {
            var uygun = Yetkili(p, "@css/permban") || (dk > 0 && dk <= Config.MaksDakika);
            m.Ekle(ad, a => { if (Gercek(h)) f(dk); else Yaz(a, "{RED}Oyuncu çıkmış."); }, !uygun);
        }
        Ac(p, m);
    }

    private void HaritaMenu(CCSPlayerController p)
    {
        var m = new TusMenu("Harita seç");
        foreach (var (ad, id) in Config.WorkshopHaritalar)
            m.Ekle(ad, a => { Duyur($"{Ad(a)} haritayı {ad} yapıyor..."); AddTimer(3, () => Server.ExecuteCommand($"host_workshop_map {id}")); });
        foreach (var ad in Config.Haritalar)
            m.Ekle(ad, a => { Duyur($"{Ad(a)} haritayı {ad} yapıyor..."); AddTimer(3, () => Server.ExecuteCommand($"changelevel {ad}")); });
        Ac(p, m);
    }
}
