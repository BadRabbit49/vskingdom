namespace VSKingdom.Constants {
	internal static class GlobalCodes {
		public const string king_GUID = "kingdomGUID";
		public const string cult_GUID = "cultureGUID";
		public const string lead_GUID = "leadersGUID";
		public const string commonerGUID = "00000000";
		public const string banditryGUID = "xxxxxxxx";
		public const string seraphimGUID = "00000000";
		public const string clockwinGUID = "cccccccc";
		public static readonly string[] kingdomIDs = { "00000000", "xxxxxxxx" };
		public static readonly string[] cultureIDs = { "00000000", "xxxxxxxx" };
		public static readonly string[] dressCodes = { "head", "tops", "gear", "pant", "shoe", "hand", "neck", "icon", "mask", "belt", "arms", "coat", "helm", "body", "legs", "left", "weap", "back", "ammo", "heal" };
		public static readonly string[] kingdomCommands = { "create", "delete", "update", "invite", "accept", "reject", "remove", "become", "depart", "revolt", "rebels", "attack", "treaty", "outlaw", "pardon", "wanted", "ballot", "voting" };
		public static readonly string[] cultureCommands = { "create", "delete", "update", "invite", "accept", "reject", "remove", "become" };
		public static readonly string[] rewriteCommands = { "player" };
		public static readonly string[] allowedWeaponry = { "bow", "sling", "arquebus", "pistol", "musket", "carbine" };
		public static readonly string[] compatibleRange = { "maltiezfirearms", "maltiezcrossbows" };
	}
}