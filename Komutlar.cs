using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;

namespace YccAdmin;

// Oyuncu komutları (slay, slap, respawn, hp, speed, freeze, noclip, god, team, bring/goto, rename), bilgi ve
// duyuru komutları (players, who, asay, csay, psay), sunucu komutları (cvar, reloadadmins, map, wsmap).
public partial class YccAdmin
{
    private readonly Dictionary<int, float> _hiz = new();   // slot → hız çarpanı (her karede uygulanır)
    private static readonly Random Zar = new();

    private void KomutlarKur()
    {
        Komut("css_slay", "@css/slay", "Öldürür: css_slay <hedef>", (p, i) =>
            Her(p, i, h => h.PlayerPawn.Value?.CommitSuicide(false, true), "öldürdü"));

        Komut("css_slap", "@css/slay", "Tokatlar: css_slap <hedef> [hasar]", (p, i) =>
        {
            var hasar = int.TryParse(i.GetArg(2), out var d) ? Math.Clamp(d, 0, 100) : 0;
            Her(p, i, h => Tokat(h, hasar), "tokatladı");
        });

        Komut("css_respawn", "@css/cheats", "Yeniden doğurur: css_respawn <hedef>", (p, i) =>
            Her(p, i, h => { if (h.Team is CsTeam.Terrorist or CsTeam.CounterTerrorist) h.Respawn(); }, "yeniden doğurdu", sadeceCanli: false));

        Komut("css_hp", "@css/slay", "Can ayarlar: css_hp <hedef> <can>", (p, i) =>
        {
            if (!int.TryParse(i.GetArg(2), out var can)) { Cevap(p, i, "{RED}Kullanım: css_hp <hedef> <can>"); return; }
            Her(p, i, h =>
            {
                var pawn = h.PlayerPawn.Value!;
                if (can <= 0) { pawn.CommitSuicide(false, true); return; }
                pawn.Health = can;
                if (can > pawn.MaxHealth) pawn.MaxHealth = can;
                Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
            }, $"canını {can} yaptı");
        });

        Komut("css_speed", "@css/slay", "Hız çarpanı: css_speed <hedef> <çarpan> (1 = normal)", (p, i) =>
        {
            if (!float.TryParse(i.GetArg(2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) || x <= 0 || x > 10)
            { Cevap(p, i, "{RED}Kullanım: css_speed <hedef> <0.1-10>"); return; }
            Her(p, i, h => { if (x == 1) _hiz.Remove(h.Slot); else _hiz[h.Slot] = x; }, $"hızını {x}x yaptı", sadeceCanli: false);
        });
        RegisterListener<Listeners.OnTick>(() =>
        {
            if (_hiz.Count == 0) return;
            foreach (var (slot, x) in _hiz)
            {
                var pawn = Utilities.GetPlayerFromSlot(slot)?.PlayerPawn.Value;
                if (pawn != null && pawn.IsValid && pawn.LifeState == (byte)LifeState_t.LIFE_ALIVE) pawn.VelocityModifier = x;
            }
        });
        RegisterListener<Listeners.OnMapStart>(_ => _hiz.Clear());
        RegisterListener<Listeners.OnClientDisconnect>(slot => _hiz.Remove(slot));

        Komut("css_freeze", "@css/slay", "Dondurur: css_freeze <hedef> [saniye] (-1/boş = süresiz)", (p, i) =>
        {
            var sn = float.TryParse(i.GetArg(2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : -1;
            Her(p, i, h =>
            {
                Hareket(h, MoveType_t.MOVETYPE_OBSOLETE);
                if (sn > 0) AddTimer(sn, () => { if (Gercek(h)) Hareket(h, MoveType_t.MOVETYPE_WALK); });
            }, sn > 0 ? $"{sn} saniye dondurdu" : "dondurdu");
        });
        Komut("css_unfreeze", "@css/slay", "Çözer: css_unfreeze <hedef>", (p, i) =>
            Her(p, i, h => Hareket(h, MoveType_t.MOVETYPE_WALK), "çözdü"));

        Komut("css_noclip", "@css/cheats", "Duvardan geçme aç/kapa: css_noclip <hedef>", (p, i) =>
            Her(p, i, h => Hareket(h, h.PlayerPawn.Value!.MoveType == MoveType_t.MOVETYPE_NOCLIP ? MoveType_t.MOVETYPE_WALK : MoveType_t.MOVETYPE_NOCLIP), "noclip'ini değiştirdi"));

        Komut("css_god", "@css/cheats", "Ölümsüzlük aç/kapa: css_god <hedef>", (p, i) =>
            Her(p, i, h => { var pawn = h.PlayerPawn.Value!; pawn.TakesDamage = !pawn.TakesDamage; }, "ölümsüzlüğünü değiştirdi"));

        Komut("css_team", "@css/kick", "Takım: css_team <hedef> <t|ct|spec>", (p, i) =>
        {
            CsTeam? t = i.GetArg(2).ToLowerInvariant() switch
            {
                "t" or "tr" or "terrorist" => CsTeam.Terrorist, "ct" or "counterterrorist" => CsTeam.CounterTerrorist,
                "spec" or "s" or "spectator" or "izle" => CsTeam.Spectator, _ => null,
            };
            if (t == null) { Cevap(p, i, "{RED}Kullanım: css_team <hedef> <t|ct|spec>"); return; }
            Her(p, i, h => h.ChangeTeam(t.Value), $"{(t == CsTeam.Spectator ? "izleyiciye" : t == CsTeam.Terrorist ? "T'ye" : "CT'ye")} aldı", sadeceCanli: false);
        });

        Komut("css_bring", "@css/kick", "Yanına getirir: css_bring <hedef>", (p, i) =>
        {
            var yer = p?.PlayerPawn.Value?.AbsOrigin;
            if (yer == null) { Cevap(p, i, "{RED}Bu komut oyun içinden kullanılır."); return; }
            Her(p, i, h => h.PlayerPawn.Value!.Teleport(yer, null, new Vector(0, 0, 0)), "yanına getirdi");
        });
        Komut("css_goto", "@css/kick", "Oyuncunun yanına gider: css_goto <hedef>", (p, i) =>
        {
            var benim = p?.PlayerPawn.Value;
            if (benim == null || !p!.PawnIsAlive) { Cevap(p, i, "{RED}Bu komut oyun içinden, canlıyken kullanılır."); return; }
            var h = Hedefler(p, i, 1, tek: true).FirstOrDefault();
            var yer = h?.PlayerPawn.Value?.AbsOrigin;
            if (yer != null) benim.Teleport(yer, null, new Vector(0, 0, 0));
        });

        Komut("css_rename", "@css/kick", "İsim değiştirir: css_rename <hedef> <yeni ad>", (p, i) =>
        {
            var yeni = Metin.Kisalt(string.Join(' ', Argumanlar(i, 2).Select(Metin.Tirnaksiz)).Trim(), 32);
            if (yeni == "") { Cevap(p, i, "{RED}Kullanım: css_rename <hedef> <yeni ad>"); return; }
            var h = Hedefler(p, i, 1, tek: true).FirstOrDefault();
            if (h == null) return;
            Duyur($"{h.PlayerName} adlı oyuncunun adı {Ad(p)} tarafından {yeni} yapıldı.");
            h.PlayerName = yeni;
            Utilities.SetStateChanged(h, "CBasePlayerController", "m_iszPlayerName");
        });

        // Panel bu çıktıyı ayrıştırıyor (panel/lib/rcon.js), biçim Metin.OyuncuSatiri'nda
        Komut("css_players", "@css/generic", "Oyuncu listesi", (p, i) =>
        {
            Cevap(p, i, "--------- PLAYER LIST ---------");
            foreach (var h in Utilities.GetPlayers().Where(Gercek))
                Cevap(p, i, Metin.OyuncuSatiri(h.UserId ?? -1, h.PlayerName, p == null ? Metin.IpYalin(h.IpAddress) : "-", h.SteamID));
            Cevap(p, i, "--------- END PLAYER LIST ---------");
        });

        Komut("css_who", "@css/generic", "Oyuncu bilgisi: css_who <hedef>", (p, i) =>
        {
            foreach (var h in Hedefler(p, i, 1))
            {
                var yetki = AdminManager.GetPlayerAdminData(h)?.GetAllFlags() is { Count: > 0 } f ? string.Join(",", f) : "yok";
                Cevap(p, i, $"#{h.UserId} {h.PlayerName} | {h.SteamID} | takım {h.Team} | yetki {yetki}");
            }
        });

        Komut("css_asay", "@css/chat", "Adminlere mesaj: css_asay <mesaj>", (p, i) =>
        {
            var m = Metin.Tirnaksiz(i.ArgString);
            foreach (var a in Utilities.GetPlayers().Where(x => Gercek(x) && Yetkili(x, "@css/chat")))
                a.PrintToChat(" " + Metin.Renk($"{{LIME}}(ADMIN) {Ad(p)}:{{DEFAULT}} {m}"));
        });
        Komut("css_csay", "@css/chat", "Ekran ortasına mesaj: css_csay <mesaj>", (p, i) =>
        {
            var m = Metin.Tirnaksiz(i.ArgString);
            foreach (var h in Utilities.GetPlayers().Where(Gercek)) h.PrintToCenter($"{Ad(p)}: {m}");
        });
        Komut("css_psay", "@css/chat", "Özel mesaj: css_psay <hedef> <mesaj>", (p, i) =>
        {
            var m = string.Join(' ', Argumanlar(i, 2).Select(Metin.Tirnaksiz));
            foreach (var h in Hedefler(p, i, 1)) h.PrintToChat(" " + Metin.Renk($"{{LIGHTPURPLE}}(Özel) {Ad(p)}:{{DEFAULT}} {m}"));
        });

        Komut("css_cvar", "@css/cvar", "Ayar oku/değiştir: css_cvar <ad> [değer]", (p, i) =>
        {
            var ad = i.GetArg(1);
            if (ad == "") { Cevap(p, i, "{RED}Kullanım: css_cvar <ad> [değer]"); return; }
            if (p != null && Metin.TehlikeliCvar(ad)) { Cevap(p, i, "{RED}Bu ayar sadece sunucu konsolundan değiştirilir."); return; }
            var cv = ConVar.Find(ad);
            if (cv == null) { Cevap(p, i, $"{{RED}}Böyle bir ayar yok: {ad}"); return; }
            if (i.ArgCount < 3) { Cevap(p, i, $"{ad} = {cv.StringValue}"); return; }
            var deger = Metin.Tirnaksiz(string.Join(' ', Argumanlar(i, 2)));
            Server.ExecuteCommand($"{ad} \"{deger.Replace("\"", "").Replace(";", "")}\"");   // ; ile ikinci komut eklenemesin
            Cevap(p, i, $"{{GREEN}}{ad} = {deger}");
        });

        // Panel admin ekleyip çıkarınca bunu çağırıyor; CounterStrikeSharp'ın kendi yeniden yükleme komutu
        Komut("css_reloadadmins", "@css/root", "admins.json'u yeniden yükler", (p, i) =>
        {
            Server.ExecuteCommand("css_admins_reload");
            Server.ExecuteCommand("css_groups_reload");
            Cevap(p, i, "{GREEN}Adminler yeniden yüklendi.");
        });

        Komut("css_map", "@css/changemap", "Harita değiştirir: css_map <harita>", (p, i) => HaritaDegistir(p, i, i.GetArg(1)));
        Komut("css_wsmap", "@css/changemap", "Workshop haritası: css_wsmap <id | ad>", (p, i) => HaritaDegistir(p, i, i.GetArg(1), workshop: true));
    }

    private void Komut(string ad, string yetki, string aciklama, Action<CCSPlayerController?, CommandInfo> f) =>
        AddCommand(ad, aciklama, (p, i) => { if (YetkiVar(p, i, yetki)) f(p, i); });

    // Hedeflerin her birine uygular ve tek bir duyuru yapar
    private void Her(CCSPlayerController? p, CommandInfo i, Action<CCSPlayerController> f, string ne, bool sadeceCanli = true)
    {
        var hedefler = Hedefler(p, i, 1).Where(h => !sadeceCanli || (h.PawnIsAlive && h.PlayerPawn.Value != null)).ToList();
        if (hedefler.Count == 0) return;
        foreach (var h in hedefler) f(h);
        var kim = hedefler.Count == 1 ? hedefler[0].PlayerName : $"{hedefler.Count} oyuncuyu";
        Duyur($"{Ad(p)}, {{LIGHTRED}}{kim}{{DEFAULT}} {ne}.");
    }

    private static void Hareket(CCSPlayerController h, MoveType_t t)
    {
        var pawn = h.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) return;
        pawn.MoveType = t;
        pawn.ActualMoveType = t;
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");
    }

    private static void Tokat(CCSPlayerController h, int hasar)
    {
        var pawn = h.PlayerPawn.Value!;
        if (hasar > 0)
        {
            if (pawn.Health <= hasar) { pawn.CommitSuicide(false, true); return; }
            pawn.Health -= hasar;
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
        }
        var v = pawn.AbsVelocity;
        pawn.Teleport(null, null, new Vector(v.X + Zar.Next(-200, 201), v.Y + Zar.Next(-200, 201), v.Z + Zar.Next(150, 301)));
    }

    private void HaritaDegistir(CCSPlayerController? p, CommandInfo i, string ad, bool workshop = false)
    {
        if (ad == "") { Cevap(p, i, $"{{RED}}Harita yaz. Workshop: {string.Join(", ", Config.WorkshopHaritalar.Keys)}"); return; }
        string komut;
        if (Config.WorkshopHaritalar.TryGetValue(ad, out var wid)) komut = $"host_workshop_map {wid}";
        else if (ulong.TryParse(ad, out var id)) komut = $"host_workshop_map {id}";
        else if (!workshop && Server.IsMapValid(ad)) komut = $"changelevel {ad}";
        else { Cevap(p, i, $"{{RED}}Harita bulunamadı: {ad}"); return; }
        Duyur($"{Ad(p)} haritayı {{GREEN}}{ad}{{DEFAULT}} yapıyor...");
        AddTimer(3, () => Server.ExecuteCommand(komut));
    }
}
