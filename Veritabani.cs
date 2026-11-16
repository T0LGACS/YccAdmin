using MySqlConnector;

namespace YccAdmin;

public enum CezaTur { GAG, MUTE, SILENCE }

public record Ceza(int Id, CezaTur Tur, int Dakika, DateTime? Bitis);   // Bitis UTC, Dakika 0 = kalıcı

// SimpleAdmin'in tablolarını (sa_bans, sa_mutes) aynen kullanır: eski 300 kayıt, aktif cezalar ve panelin "Cezalar"
// sayfası olduğu gibi çalışır. Zamanlar UTC (SimpleAdmin Timezone: "UTC" ile yazmıştı). Tüm sorgular parametreli.
// Çağrılar arka plan iş parçacığında yapılır, oyun karesini bekletmez.
public class Veritabani(VeritabaniAyar a, int sunucuId)
{
    private readonly string _baglanti = new MySqlConnectionStringBuilder
    {
        Server = a.Sunucu, Port = (uint)a.Port, UserID = a.Kullanici, Password = a.Sifre, Database = a.Ad,
        CharacterSet = "utf8mb4", ConnectionTimeout = 5, DefaultCommandTimeout = 10, Pooling = true,
    }.ConnectionString;

    private async Task<MySqlConnection> Ac()
    {
        var c = new MySqlConnection(_baglanti);
        await c.OpenAsync();
        return c;
    }

    private static MySqlCommand Komut(MySqlConnection c, string sql, params (string, object?)[] p)
    {
        var k = new MySqlCommand(sql, c);
        foreach (var (ad, deger) in p) k.Parameters.AddWithValue(ad, deger ?? DBNull.Value);
        return k;
    }

    private const string Aktif = "status = 'ACTIVE' AND (duration = 0 OR ends > UTC_TIMESTAMP())";

    public async Task<(int id, string sebep, int dakika, DateTime? bitis)?> AktifBan(ulong steamId)
    {
        await using var c = await Ac();
        await using var k = Komut(c, $"SELECT id, reason, duration, ends FROM sa_bans WHERE player_steamid = @s AND {Aktif} ORDER BY id DESC LIMIT 1", ("@s", steamId));
        await using var r = await k.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return (r.GetInt32(0), r.GetString(1), r.GetInt32(2), r.IsDBNull(3) ? null : r.GetDateTime(3));
    }

    public async Task<List<Ceza>> AktifSusturmalar(ulong steamId)
    {
        await using var c = await Ac();
        await using var k = Komut(c, $"SELECT id, type, duration, ends FROM sa_mutes WHERE player_steamid = @s AND {Aktif}", ("@s", steamId));
        await using var r = await k.ExecuteReaderAsync();
        var l = new List<Ceza>();
        while (await r.ReadAsync())
            if (Enum.TryParse<CezaTur>(r.GetString(1), out var t))
                l.Add(new Ceza(r.GetInt32(0), t, r.GetInt32(2), r.IsDBNull(3) ? null : r.GetDateTime(3)));
        return l;
    }

    public async Task<int> BanEkle(string? isim, ulong steamId, string? ip, ulong adminId, string adminAd, string sebep, int dakika)
    {
        await using var c = await Ac();
        await using var k = Komut(c, @"INSERT INTO sa_bans (player_name, player_steamid, player_ip, admin_steamid, admin_name, reason, duration, ends, created, server_id, status)
            VALUES (@n, @s, @ip, @as, @an, @r, @d, DATE_ADD(UTC_TIMESTAMP(), INTERVAL @d MINUTE), UTC_TIMESTAMP(), @sv, 'ACTIVE'); SELECT LAST_INSERT_ID();",
            ("@n", isim), ("@s", steamId), ("@ip", string.IsNullOrEmpty(ip) ? null : ip), ("@as", adminId), ("@an", adminAd), ("@r", sebep), ("@d", dakika), ("@sv", sunucuId));
        return Convert.ToInt32(await k.ExecuteScalarAsync());
    }

    public async Task<int> SusturmaEkle(string? isim, ulong steamId, ulong adminId, string adminAd, string sebep, int dakika, CezaTur tur)
    {
        await using var c = await Ac();
        await using var k = Komut(c, @"INSERT INTO sa_mutes (player_name, player_steamid, admin_steamid, admin_name, reason, duration, ends, created, type, server_id, status)
            VALUES (@n, @s, @as, @an, @r, @d, DATE_ADD(UTC_TIMESTAMP(), INTERVAL @d MINUTE), UTC_TIMESTAMP(), @t, @sv, 'ACTIVE'); SELECT LAST_INSERT_ID();",
            ("@n", isim), ("@s", steamId), ("@as", adminId), ("@an", adminAd), ("@r", sebep), ("@d", dakika), ("@t", tur.ToString()), ("@sv", sunucuId));
        return Convert.ToInt32(await k.ExecuteScalarAsync());
    }

    // Kaç satır kalktı döner
    public async Task<int> BanKaldir(ulong steamId)
    {
        await using var c = await Ac();
        await using var k = Komut(c, "UPDATE sa_bans SET status = 'UNBANNED' WHERE player_steamid = @s AND status = 'ACTIVE'", ("@s", steamId));
        return await k.ExecuteNonQueryAsync();
    }

    // SILENCE (yazı + ses) kaldırılınca hepsi kalkar. GAG ya da MUTE kaldırılınca o tür kalkar, SILENCE varsa
    // diğer yarısına dönüşür (silence'lı birine ungag: sesi kapalı kalır, yazısı açılır). Kaç kayıt değişti döner.
    public async Task<int> SusturmaKaldir(ulong steamId, CezaTur tur)
    {
        await using var c = await Ac();
        if (tur == CezaTur.SILENCE)
        {
            await using var k = Komut(c, "UPDATE sa_mutes SET status = 'UNMUTED' WHERE player_steamid = @s AND status = 'ACTIVE'", ("@s", steamId));
            return await k.ExecuteNonQueryAsync();
        }
        await using var k1 = Komut(c, "UPDATE sa_mutes SET status = 'UNMUTED' WHERE player_steamid = @s AND status = 'ACTIVE' AND type = @t",
            ("@s", steamId), ("@t", tur.ToString()));
        var n = await k1.ExecuteNonQueryAsync();
        await using var k2 = Komut(c, "UPDATE sa_mutes SET type = @k WHERE player_steamid = @s AND status = 'ACTIVE' AND type = 'SILENCE'",
            ("@s", steamId), ("@k", Kalan(tur).ToString()));
        return n + await k2.ExecuteNonQueryAsync();
    }

    // SILENCE'tan bir yarısı kalkınca kalan yarı
    public static CezaTur Kalan(CezaTur kalkan) => kalkan == CezaTur.GAG ? CezaTur.MUTE : CezaTur.GAG;

    // Süresi dolanları EXPIRED yapar (panel aktif listesinde kalmasınlar)
    public async Task SuresiDolanlar()
    {
        await using var c = await Ac();
        foreach (var t in new[] { "sa_bans", "sa_mutes" })
        {
            await using var k = Komut(c, $"UPDATE {t} SET status = 'EXPIRED' WHERE status = 'ACTIVE' AND duration > 0 AND ends <= UTC_TIMESTAMP()");
            await k.ExecuteNonQueryAsync();
        }
    }
}
