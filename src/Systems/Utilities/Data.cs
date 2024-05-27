using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Util;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;

namespace VSKingdom {
	internal static class DataUtility {
		private static byte[] kingdomData => VSKingdom.serverAPI.WorldManager.SaveGame.GetData("kingdomData");
		private static byte[] cultureData => VSKingdom.serverAPI.WorldManager.SaveGame.GetData("cultureData");
		private static List<Kingdom> kingdomList => kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);
		private static List<Culture> cultureList => cultureList is null ? new List<Culture>() : SerializerUtil.Deserialize<List<Culture>>(cultureData);

		public static string GetKingdomName(string KingdomGUID) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == KingdomGUID)?.KingdomName;
		}

		public static string GetKingdomGUID(string KingdomName) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomName.ToLowerInvariant() == KingdomName.ToLowerInvariant())?.KingdomGUID;
		}

		public static string GetKingdomGUID(IServerPlayer Player) {
			return Player.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID") ?? null;
		}

		public static string GetLeadersName(string LeadersUID) {
			return VSKingdom.serverAPI.PlayerData.GetPlayerDataByUid(LeadersUID)?.LastKnownPlayername;
		}

		public static string GetLeadersUID(string KingdomGUID, string PlayersUID) {
			if (KingdomGUID is not null) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == KingdomGUID)?.LeadersGUID;
			}
			if (PlayersUID is not null) {
				foreach (Kingdom kingdom in kingdomList) {
					if (kingdom.PlayerUIDs.Contains(PlayersUID)) {
						return kingdom.LeadersGUID;
					}
				}
			}
			return null;
		}

		public static string[] GetPlayerUIDs(string KingdomGUID) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == KingdomGUID)?.PlayerUIDs.ToArray<string>();
		}

		public static string[] GetEnemieUIDs(string KingdomGUID) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == KingdomGUID)?.EnemieUIDs.ToArray<string>();
		}

		public static string[] GetOnlineUIDs(string KingdomGUID) {
			string[] AllMembers = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == KingdomGUID)?.PlayerUIDs.ToArray<string>();
			IPlayer[] AllPlayers = VSKingdom.serverAPI.World.AllOnlinePlayers;
			string[] AllOnlines = Array.Empty<string>();
			foreach (var player in AllPlayers) {
				if (AllMembers.Contains(player.PlayerUID)) {
					AllOnlines.AddItem(player.PlayerUID);
				}
			}
			return AllOnlines;
		}

		public static string[] GetOfflineUIDs(string KingdomGUID) {
			string[] AllMembers = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == KingdomGUID)?.PlayerUIDs.ToArray<string>();
			IServerPlayer[] AllPlayers = VSKingdom.serverAPI.World.AllPlayers as IServerPlayer[];
			string[] AllOffline = Array.Empty<string>();
			foreach (var player in AllPlayers) {
				if (!AllMembers.Contains(player.PlayerUID)) {
					AllOffline.AddItem(player.PlayerUID);
				}
			}
			return AllOffline;
		}

		public static long[] GetEntityUIDs(string KingdomGUID) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == KingdomGUID)?.EntityUIDs.ToArray<long>();
		}
		
		public static bool KingdomExists(string KingdomName, string KingdomGUID) {
			if (KingdomName is not null) {
				return kingdomList.Contains(kingdomList.Find(kingdomMatch => kingdomMatch.KingdomName.ToLowerInvariant() == KingdomName.ToLowerInvariant()));
			}
			if (KingdomGUID is not null) {
				return kingdomList.Contains(kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == KingdomGUID));
			}
			return false;
		}

		public static bool IsAnEnemy(string thisKingdomGUID, string thatKingdomGUID) {
			if (thisKingdomGUID == thatKingdomGUID) {
				return false;
			}
			return AtWarWith(thisKingdomGUID, thatKingdomGUID);
		}

		public static bool IsAnEnemy(string thisKingdomGUID, Entity target) {
			if (target is not EntityPlayer && target is not EntityArcher && target is not EntityKnight) {
				return true;
			}
			if (thisKingdomGUID is null) {
				return false;
			}
			if (!target.WatchedAttributes.HasAttribute("loyalties")) {
				return true;
			}
			if (kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thisKingdomGUID).EntityUIDs.Contains(target.EntityId)) {
				return false;
			}

			string thatKingdomGUID = target.WatchedAttributes.GetTreeAttribute("loyalties").GetString("kingdomUID");

			if (thisKingdomGUID == thatKingdomGUID) {
				return false;
			}
			if (target is EntityPlayer) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thisKingdomGUID).EnemieUIDs.Contains((target as EntityPlayer).PlayerUID);
			}
			if (kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thisKingdomGUID).AtWarsUIDs.Contains(thatKingdomGUID)) {
				return true;
			}
			if (kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thatKingdomGUID).AtWarsUIDs.Contains(thisKingdomGUID)) {
				return true;
			}
			return false;
		}

		public static bool IsAnEnemy(Entity entity, Entity target) {
			if (target is not EntityPlayer && target is not EntityArcher && target is not EntityKnight) {
				return true;
			}
			if (!target.WatchedAttributes.HasAttribute("loyalties")) {
				return true;
			}

			string thisKingdomGUID = entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID");
			string thatKingdomGUID = target.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID");

			if (thisKingdomGUID == thatKingdomGUID) {
				return false;
			}
			if (target is EntityPlayer playerTarget) {
				if (entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("leadersUID") == playerTarget.PlayerUID) {
					return false;
				}
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thisKingdomGUID).EnemieUIDs.Contains(playerTarget.PlayerUID);
			}
			return AtWarWith(thisKingdomGUID, thatKingdomGUID);
		}

		public static bool IsAFriend(string thisKingdomGUID, Entity target) {
			if (target is not EntityPlayer && target is not EntityArcher && target is not EntityKnight) {
				return false;
			}
			if (!target.WatchedAttributes.HasAttribute("loyalties")) {
				return false;
			}

			string thatKingdomGUID = target.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID");

			if (thisKingdomGUID == thatKingdomGUID) {
				return true;
			} else {
				return false;
			}
		}
		
		public static bool IsAFriend(Entity entity, Entity target) {
			if (target is not EntityPlayer && target is not EntityArcher && target is not EntityKnight) {
				return false;
			}
			if (!target.WatchedAttributes.HasAttribute("loyalties")) {
				return false;
			}

			string thisKingdomGUID = entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID");
			string thatKingdomGUID = target.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID");

			if (thisKingdomGUID == thatKingdomGUID) {
				return true;
			} else {
				return false;
			}
		}

		public static bool InKingdom(IServerPlayer player) {
			foreach (Kingdom kingdom in kingdomList) {
				if (kingdom.PlayerUIDs.Contains(player.PlayerUID)) {
					return true;
				}
			}
			return false;
		}

		public static bool AtWarWith(string thisKingdomGUID, string thatKingdomGUID) {
			if (kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thisKingdomGUID).AtWarsUIDs.Contains(thatKingdomGUID)) {
				return true;
			}
			if (kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thatKingdomGUID).AtWarsUIDs.Contains(thisKingdomGUID)) {
				return true;
			}
			return false;
		}

		public static IServerPlayer GetLeaders(string KingdomName, string KingdomGUID) {
			if (KingdomName is not null) {
				return VSKingdom.serverAPI?.World.PlayerByUid(kingdomList.Find(kingdomMatch => kingdomMatch.KingdomName == KingdomName)?.LeadersGUID) as IServerPlayer;
			}
			if (KingdomGUID is not null) {
				return VSKingdom.serverAPI?.World.PlayerByUid(kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == KingdomGUID)?.LeadersGUID) as IServerPlayer;
			}
			return null;
		}

		public static IServerPlayer GetAPlayer(string PlayerUID) {
			return VSKingdom.serverAPI.World.AllPlayers.ToList<IPlayer>().Find(playerMatch => playerMatch.PlayerUID == PlayerUID) as IServerPlayer ?? null;
		}
		
		public static Kingdom GetKingdom(string KingdomGUID, string KingdomName) {
			if (KingdomGUID is not null) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == KingdomGUID);
			}
			if (KingdomName is not null) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomName == KingdomName);
			}
			return null;
		}

		public static Culture GetCulture(string CultureGUID, string CultureName) {
			if (CultureGUID is not null) {
				return cultureList.Find(cultureMatch => cultureMatch.CultureGUID == CultureGUID);
			}
			if (CultureName is not null) {
				return cultureList.Find(cultureMatch => cultureMatch.CultureName == CultureName);
			}
			return null;
		}

		public static BlockEntityPost GetOutpost(BlockPos outpostPOS) {
			if (VSKingdom.serverAPI.World.BlockAccessor.GetBlockEntity(outpostPOS) is BlockEntityPost block) {
				return block;
			}
			return null;
		}
	}
}
