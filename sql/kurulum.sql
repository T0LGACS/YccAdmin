-- YccAdmin veritabanı tabloları (YCC TEAM sunucusundaki yapının aynısı, içinde veri yok).
-- Kullanım: mysql -u cs2 -p cs2 < sql/kurulum.sql   (tablo zaten varsa dokunulmaz)

CREATE TABLE IF NOT EXISTS `sa_bans` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `player_name` varchar(128) DEFAULT NULL,
  `player_steamid` bigint(20) DEFAULT NULL,
  `player_ip` varchar(128) DEFAULT NULL,
  `admin_steamid` bigint(20) NOT NULL,
  `admin_name` varchar(128) NOT NULL,
  `reason` varchar(255) NOT NULL,
  `duration` int(11) NOT NULL,
  `ends` timestamp NULL DEFAULT NULL,
  `created` timestamp NOT NULL DEFAULT current_timestamp(),
  `server_id` int(11) DEFAULT NULL,
  `unban_id` int(11) DEFAULT NULL,
  `status` enum('ACTIVE','UNBANNED','EXPIRED','') NOT NULL DEFAULT 'ACTIVE',
  `updated_at` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`id`),
  KEY `unban_id` (`unban_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `sa_mutes` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `player_name` varchar(128) DEFAULT NULL,
  `player_steamid` bigint(20) DEFAULT NULL,
  `admin_steamid` bigint(20) NOT NULL,
  `admin_name` varchar(128) NOT NULL,
  `reason` varchar(255) NOT NULL,
  `duration` int(11) NOT NULL,
  `passed` int(11) DEFAULT NULL,
  `ends` timestamp NULL DEFAULT NULL,
  `created` timestamp NOT NULL DEFAULT current_timestamp(),
  `type` enum('GAG','MUTE','SILENCE','') NOT NULL DEFAULT 'GAG',
  `server_id` int(11) DEFAULT NULL,
  `unmute_id` int(11) DEFAULT NULL,
  `status` enum('ACTIVE','UNMUTED','EXPIRED','') NOT NULL DEFAULT 'ACTIVE',
  PRIMARY KEY (`id`),
  KEY `unmute_id` (`unmute_id`),
  KEY `player_steamid` (`player_steamid`,`status`,`ends`),
  KEY `player_steamid_2` (`player_steamid`,`status`,`server_id`,`duration`),
  KEY `player_steamid_3` (`player_steamid`,`type`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
