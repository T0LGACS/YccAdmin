using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;

namespace YccAdmin;

// Ban, kick ve susturma (GAG = yazı, MUTE = ses, SILENCE = ikisi). Kayıtlar sa_bans / sa_mutes'ta.
// Oyuncu girince aktif banı varsa atılır, aktif susturmaları belleğe alınır; süresi dolanlar yarım dakikada bir kalkar.
public partial class YccAdmin
{
    private readonly Dictionary<ulong, List<Ceza>> _sus = new();   // sadece ana iş parçacığında kullanılır

    private static bool Suruyor(Ceza c) => c.Dakika <= 0 || c.Bitis == null || c.Bitis > DateTime.UtcNow;
    private bool Gagli(ulong sid) => _sus.TryGetValue(sid, out var l) && l.Any(c => c.Tur != CezaTur.MUTE && Suruyor(c));
    private bool SesKapali(ulong sid) => _sus.TryGetValue(sid, out var l) && l.Any(c => c.Tur != CezaTur.GAG && Suruyor(c));

    private void CezalarKur(bool hotReload)
    {
        RegisterListener<Listeners.OnClientAuthorized>((slot, steam) => Yukle(slot, steam.SteamId64));
        RegisterListener<Listeners.OnClientDisconnect>(slot =>
        {
            var p = Utilities.GetPlayerFromSlot(slot);
            if (p != null) _sus.Remove(p.SteamID);
        });
        if (hotReload) foreach (var p in Utilities.GetPlayers().Where(Gercek)) Yukle(p.Slot, p.SteamID);

        foreach (var k in new[] { "say", "say_team" })
            AddCommandListener(k, (p, info) =>
            {
                if (!Gercek(p) || !Gagli(p!.SteamID)) return HookResult.Continue;
                var l = _sus[p.SteamID].Where(c => c.Tur != CezaTur.MUTE && Suruyor(c)).ToList();
                var kalici = l.Any(c => c.Dakika <= 0 || c.Bitis == null);
                var kalan = kalici ? "kalıcı" : Metin.Sure((int)Math.Ceiling((l.Max(c => c.Bitis!.Value) - DateTime.UtcNow).TotalMinutes));
                Yaz(p, $"{{RED}}Sohbetin kapalı. Kalan süre: {kalan}");
                return HookResult.Handled;
            });

        // Süresi dolan susturmalar: bellekten kalkar, sesi açılır; veritabanında EXPIRED yapılır
        var sayac = 0;
        AddTimer(30, () =>
        {
            foreach (var (sid, l) in _sus.ToList())
            {
                if (l.RemoveAll(c => !Suruyor(c)) == 0) continue;
                var p = Utilities.GetPlayerFromSteamId(sid);
                if (Gercek(p)) { SesUygula(p!); Yaz(p!, "{GREEN}Susturma süren doldu."); }
            }
            if (++sayac % 4 == 0) Arka("süresi dolan cezalar", _db.SuresiDolanlar);
        }, TimerFlags.REPEAT);

        AddCommand("css_ban", "Oyuncuyu banlar: css_ban <hedef> [dakika] [sebep]", (p, i) => BanKomut(p, i));
        AddCommand("css_addban", "Çevrimdışı ban: css_addban <steamid64> [dakika] [sebep]", AddBanKomut);
        AddCommand("css_unban", "Banı kaldırır: css_unban <steamid64>", (p, i) => KaldirKomut(p, i, null));
        AddCommand("css_kick", "Oyuncuyu atar: css_kick <hedef> [sebep]", KickKomut);
        AddCommand("css_gag", "Yazıyı kapatır", (p, i) => SusturKomut(p, i, CezaTur.GAG));
        AddCommand("css_mute", "Sesi kapatır", (p, i) => SusturKomut(p, i, CezaTur.MUTE));
        AddCommand("css_silence", "Yazı + ses kapatır", (p, i) => SusturKomut(p, i, CezaTur.SILENCE));
        AddCommand("css_ungag", "Yazı cezasını kaldırır", (p, i) => KaldirKomut(p, i, CezaTur.GAG));
        AddCommand("css_unmute", "Ses cezasını kaldırır", (p, i) => KaldirKomut(p, i, CezaTur.MUTE));
        AddCommand("css_unsilence", "Yazı + ses cezasını kaldırır", (p, i) => KaldirKomut(p, i, CezaTur.SILENCE));
    }

    private void Yukle(int slot, ulong sid)
    {
        Arka("ceza sorgusu", async () =>
        {
            var ban = await _db.AktifBan(sid);
            var sus = ban == null ? await _db.AktifSusturmalar(sid) : new();
            Server.NextFrame(() =>
            {
                var p = Utilities.GetPlayerFromSlot(slot);
                if (!Gercek(p) || p!.SteamID != sid) return;
                if (ban != null)
                {
                    var b = ban.Value;
                    var bitis = b.dakika <= 0 || b.bitis == null ? "kalıcı" : $"bitiş {b.bitis.Value.ToLocalTime():dd.MM HH:mm}";
                    At(p, $"Banlisin ({bitis}). Sebep: {b.sebep}");
                    return;
                }
                _sus[sid] = sus;
                SesUygula(p);
                if (Gagli(sid)) Yaz(p, "{RED}Sohbet cezan devam ediyor.");
            });
        });
    }

    private void SesUygula(CCSPlayerController p) =>
        p.VoiceFlags = SesKapali(p.SteamID) ? p.VoiceFlags | VoiceFlags.Muted : p.VoiceFlags & ~VoiceFlags.Muted;

    private static void At(CCSPlayerController p, string sebep) =>
        Server.ExecuteCommand($"kickid {p.UserId} \"{sebep.Replace('"', '\'').Replace(';', ',')}\"");

    // @css/permban yoksa süre sınırlı ve kalıcı yasak
    private bool SureUygun(CCSPlayerController? p, CommandInfo info, int dakika)
    {
        if (Yetkili(p, "@css/permban") || (dakika > 0 && dakika <= Config.MaksDakika)) return true;
        Cevap(p, info, $"{{RED}}En fazla {Metin.Sure(Config.MaksDakika)} verebilirsin (kalıcı için @css/permban gerekir).");
        return false;
    }

    public void BanKomut(CCSPlayerController? p, CommandInfo info)
    {
        if (!YetkiVar(p, info, "@css/ban")) return;
        var hedef = Hedefler(p, info, 1, tek: true).FirstOrDefault();
        if (hedef == null) return;
        var (dk, sebep) = Metin.SureVeSebep(Argumanlar(info, 2), Config.VarsayilanDakika, "Sebep yok");
        if (SureUygun(p, info, dk)) Banla(p, hedef, dk, sebep);
    }

    // Menü de bunu kullanır
    public void Banla(CCSPlayerController? p, CCSPlayerController hedef, int dk, string sebep)
    {
        string isim = hedef.PlayerName, ip = Metin.IpYalin(hedef.IpAddress), adminAd = Ad(p);
        ulong sid = hedef.SteamID, adminId = Id(p);
        At(hedef, $"Banlandin ({Metin.Sure(dk)}). Sebep: {sebep}");
        Duyur($"{{LIGHTRED}}{isim}{{DEFAULT}}, {adminAd} tarafından {{RED}}{Metin.Sure(dk)}{{DEFAULT}} banlandı. Sebep: {sebep}");
        Arka("ban kaydı", async () => await _db.BanEkle(isim, sid, ip, adminId, adminAd, sebep, dk));
    }

    private void AddBanKomut(CCSPlayerController? p, CommandInfo info)
    {
        if (!YetkiVar(p, info, "@css/ban")) return;
        if (!Metin.SteamId64(info.GetArg(1), out var sid)) { Cevap(p, info, "{RED}Kullanım: css_addban <steamid64> [dakika] [sebep]"); return; }
        var (dk, sebep) = Metin.SureVeSebep(Argumanlar(info, 2), Config.VarsayilanDakika, "Sebep yok");
        if (!SureUygun(p, info, dk)) return;
        var cevrimici = Utilities.GetPlayerFromSteamId(sid);
        if (Gercek(cevrimici)) { Banla(p, cevrimici!, dk, sebep); return; }
        string adminAd = Ad(p); ulong adminId = Id(p);
        Arka("ban kaydı", async () => await _db.BanEkle(null, sid, null, adminId, adminAd, sebep, dk));
        Cevap(p, info, $"{{GREEN}}{sid} {Metin.Sure(dk)} banlandı. Sebep: {sebep}");
    }

    private void KickKomut(CCSPlayerController? p, CommandInfo info)
    {
        if (!YetkiVar(p, info, "@css/kick")) return;
        var sebep = string.Join(' ', Argumanlar(info, 2).Select(Metin.Tirnaksiz)).Trim();
        foreach (var h in Hedefler(p, info, 1)) Kickle(p, h, sebep == "" ? "Sebep yok" : sebep);
    }

    public void Kickle(CCSPlayerController? p, CCSPlayerController h, string sebep)
    {
        Duyur($"{{LIGHTRED}}{h.PlayerName}{{DEFAULT}}, {Ad(p)} tarafından atıldı. Sebep: {sebep}");
        At(h, $"Atildin. Sebep: {sebep}");
    }

    private void SusturKomut(CCSPlayerController? p, CommandInfo info, CezaTur tur)
    {
        if (!YetkiVar(p, info, "@css/chat")) return;
        var hedefler = Hedefler(p, info, 1);
        if (hedefler.Count == 0) return;
        var (dk, sebep) = Metin.SureVeSebep(Argumanlar(info, 2), Config.VarsayilanDakika, "Sebep yok");
        if (!SureUygun(p, info, dk)) return;
        foreach (var h in hedefler) Sustur(p, h, tur, dk, sebep);
    }

    private static string TurAdi(CezaTur t) => t switch { CezaTur.GAG => "yazısı", CezaTur.MUTE => "sesi", _ => "yazısı ve sesi" };

    public void Sustur(CCSPlayerController? p, CCSPlayerController h, CezaTur tur, int dk, string sebep)
    {
        string isim = h.PlayerName, adminAd = Ad(p);
        ulong sid = h.SteamID, adminId = Id(p);
        if (!_sus.TryGetValue(sid, out var l)) _sus[sid] = l = new();
        l.Add(new Ceza(0, tur, dk, dk <= 0 ? null : DateTime.UtcNow.AddMinutes(dk)));
        SesUygula(h);
        Duyur($"{{LIGHTRED}}{isim}{{DEFAULT}} adlı oyuncunun {TurAdi(tur)}, {adminAd} tarafından {{RED}}{Metin.Sure(dk)}{{DEFAULT}} kapatıldı. Sebep: {sebep}");
        Arka("susturma kaydı", async () => await _db.SusturmaEkle(isim, sid, adminId, adminAd, sebep, dk, tur));
    }

    // css_unban / css_ungag / css_unmute / css_unsilence <steamid64 | hedef>. Panel SteamID64 yollar.
    private void KaldirKomut(CCSPlayerController? p, CommandInfo info, CezaTur? tur)
    {
        if (!YetkiVar(p, info, tur == null ? "@css/unban" : "@css/chat")) return;
        List<(ulong sid, string isim)> kimler;
        if (Metin.SteamId64(info.GetArg(1), out var id)) kimler = new() { (id, Utilities.GetPlayerFromSteamId(id)?.PlayerName ?? id.ToString()) };
        else kimler = Hedefler(p, info, 1).Select(h => (h.SteamID, h.PlayerName)).ToList();
        foreach (var (sid, isim) in kimler)
        {
            if (tur != null && _sus.TryGetValue(sid, out var l))
            {
                if (tur == CezaTur.SILENCE) l.Clear();
                else
                {
                    l.RemoveAll(c => c.Tur == tur);
                    for (var j = 0; j < l.Count; j++) if (l[j].Tur == CezaTur.SILENCE) l[j] = l[j] with { Tur = Veritabani.Kalan(tur.Value) };
                }
                var h = Utilities.GetPlayerFromSteamId(sid);
                if (Gercek(h)) { SesUygula(h!); Yaz(h!, $"{{GREEN}}{TurAdi(tur.Value)} açıldı."); }
            }
            var adminAd = Ad(p);
            Arka("ceza kaldırma", async () =>
            {
                var n = tur == null ? await _db.BanKaldir(sid) : await _db.SusturmaKaldir(sid, tur.Value);
                Server.NextFrame(() =>
                {
                    var ne = tur == null ? "banı" : $"{TurAdi(tur.Value)} cezası";
                    var s = n > 0 ? $"{{GREEN}}{isim} adlı oyuncunun {ne} {adminAd} tarafından kaldırıldı." : $"{{GREY}}{isim} için aktif {ne} yok.";
                    if (p != null && p.IsValid) Yaz(p, s); else Console.WriteLine("[YccAdmin] " + System.Text.RegularExpressions.Regex.Replace(s, @"\{[A-Za-z]+\}", ""));
                });
            });
        }
    }
}
