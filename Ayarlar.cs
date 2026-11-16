using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace YccAdmin;

public class Ayarlar : BasePluginConfig
{
    [JsonPropertyName("Veritabani")] public VeritabaniAyar Veritabani { get; set; } = new();
    // sa_bans / sa_mutes satırlarındaki server_id (eski kayıtlarla aynı: bu VDS = 3)
    [JsonPropertyName("SunucuId")] public int SunucuId { get; set; } = 3;
    // @css/permban yetkisi olmayan admin en fazla bu kadar dakika ban/susturma verebilir ve kalıcı veremez
    [JsonPropertyName("MaksDakika")] public int MaksDakika { get; set; } = 10080;
    [JsonPropertyName("VarsayilanDakika")] public int VarsayilanDakika { get; set; } = 60;
    // !wsmap aim_redline → Workshop id
    [JsonPropertyName("WorkshopHaritalar")] public Dictionary<string, ulong> WorkshopHaritalar { get; set; } = new();
    [JsonPropertyName("Haritalar")] public List<string> Haritalar { get; set; } = new();
    [JsonPropertyName("On")] public string On { get; set; } = "{RED}[YCC]{DEFAULT}";
}

public class VeritabaniAyar
{
    [JsonPropertyName("Sunucu")] public string Sunucu { get; set; } = "127.0.0.1";
    [JsonPropertyName("Port")] public int Port { get; set; } = 3306;
    [JsonPropertyName("Kullanici")] public string Kullanici { get; set; } = "cs2";
    [JsonPropertyName("Sifre")] public string Sifre { get; set; } = "";
    [JsonPropertyName("Ad")] public string Ad { get; set; } = "cs2";
}
