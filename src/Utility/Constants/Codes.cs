namespace VSKingdom.Constants {
	internal static class GlobalCodes {
		public const string KingdomUID = "kingdomGUID";
		public const string CultureUID = "cultureGUID";
		public const string LeadersUID = "leadersGUID";
		public const string CommonerID = "00000000";
		public const string BanditryID = "xxxxxxxx";
		public const string SeraphimID = "00000000";
		public const string ClockwinID = "cccccccc";
		public const string FriendFire = "Allowed_SentryTeamHurt";
		public const string FallDamage = "Allowed_SentryTripping";
		public const string CanDrownIn = "Allowed_SentryDrowning";
		public const string TeleportTo = "Allowed_SentryTeleport";
		public const string NpcCanLoot = "Allowed_SentryDropLoot";
		public const string NpcInfAmmo = "Allowed_InfiniteArrows";
		public const string PlayerLoot = "Allowed_PlayerDropLoot";
		public const string CanStartCW = "Allowed_StartCivilWars";
		public const string NpcRearmms = "Limited_SentryResupply";
		public const string NpcRevives = "Limited_SentryReviving";
		public const string PlayerTags = "Nametag_PlayerNametags";
		public const string SentryTags = "Nametag_SentryNametags";
		public const string RenderTags = "Nametag_RenderDistance";
		public const string CultCreate = "Culture_MinCreateLevel";
		public const string CultCooldn = "Culture_CreateCooldown";
		public const string CultMaxUsr = "Culture_MaxUserCreated";
		public const string KingCreate = "Kingdom_MinCreateLevel";
		public const string KingCooldn = "Kingdom_CreateCooldown";
		public const string KingMaxUsr = "Kingdom_MaxUserCreated";
		public static readonly string[] kingdomIDs = { "00000000", "xxxxxxxx" };
		public static readonly string[] cultureIDs = { "00000000", "xxxxxxxx" };
		public static readonly string[] dressCodes = { "head", "tops", "gear", "pant", "shoe", "hand", "neck", "icon", "mask", "belt", "arms", "coat", "helm", "body", "legs", "left", "weap", "back", "ammo", "heal" };
		public static readonly string[] kingdomCommands = { "create", "delete", "update", "invite", "accept", "reject", "remove", "become", "depart", "revolt", "rebels", "attack", "treaty", "outlaw", "pardon", "wanted", "ballot", "voting" };
		public static readonly string[] cultureCommands = { "create", "delete", "update", "invite", "accept", "reject", "remove", "become" };
		public static readonly string[] allowedWeaponry = { "bow", "sling", "arquebus", "pistol", "musket", "carbine" };
		public static readonly string[] compatibleRange = { "maltiezfirearms", "maltiezcrossbows" };
	}
}