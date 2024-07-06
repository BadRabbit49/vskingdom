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
		private static ICoreServerAPI serverAPI { get => VSKingdom.serverAPI; }
		private static byte[] kingdomData { get => VSKingdom.serverAPI.WorldManager.SaveGame.GetData("kingdomData"); }
		private static byte[] cultureData { get => VSKingdom.serverAPI.WorldManager.SaveGame.GetData("cultureData"); }
		private static List<Kingdom> kingdomList => kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);
		private static List<Culture> cultureList => cultureData is null ? new List<Culture>() : SerializerUtil.Deserialize<List<Culture>>(cultureData);
		
		public static string GetKingdomNAME(string kingdomGUID) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID)?.KingdomNAME;
		}

		public static string GetKingdomGUID(string kingdomNAME) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomNAME.ToLowerInvariant() == kingdomNAME.ToLowerInvariant())?.KingdomGUID;
		}

		public static string GetKingdomGUID(IServerPlayer Player) {
			return Player.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") ?? null;
		}

		public static string GetLeadersName(string LeadersUID) {
			return serverAPI.PlayerData.GetPlayerDataByUid(LeadersUID)?.LastKnownPlayername;
		}

		public static string GetLeadersGUID(string kingdomGUID) {
			if (kingdomGUID != null) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID)?.LeadersGUID;
			}
			return null;
		}

		public static string GetLeadersGUID(string kingdomGUID, string PlayersUID = null) {
			if (kingdomGUID != null) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID)?.LeadersGUID;
			}
			if (PlayersUID != null) {
				foreach (Kingdom kingdom in kingdomList) {
					if (kingdom.PlayersGUID.Contains(PlayersUID)) {
						return kingdom.LeadersGUID;
					}
				}
			}
			return null;
		}
		
		public static string[] GetKingdomGUIDs() {
			string[] kingdomGUIDs = Array.Empty<string>();
			foreach (var kingdom in kingdomList) {
				kingdomGUIDs.AddItem(kingdom.KingdomGUID);
			}
			return kingdomGUIDs;
		}

		public static string[] GetCultureGUIDs() {
			string[] cultureGUIDs = Array.Empty<string>();
			foreach (var culture in cultureList) {
				cultureGUIDs.AddItem(culture.CultureGUID);
			}
			return cultureGUIDs;
		}

		public static string[] GetPlayersGUIDs(string kingdomGUID = null, string cultureGUID = null) {
			string[] playersGUID = Array.Empty<string>();
			foreach (var player in serverAPI.World.AllPlayers) {
				if (kingdomGUID == null && cultureGUID == null) {
					playersGUID.AddItem(player.PlayerUID);
					continue;
				}
				if (kingdomGUID != null && player.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") == kingdomGUID) {
					playersGUID.AddItem(player.PlayerUID);
				}
				if (cultureGUID != null && player.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("culture_guid") == cultureGUID) {
					playersGUID.AddItem(player.PlayerUID);
				}
			}
			return playersGUID;
		}

		public static string[] GetEnemiesGUIDs(string kingdomGUID) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID)?.EnemiesGUID.ToArray<string>();
		}

		public static string[] GetOnlinesGUIDs(string kingdomGUID = null, string cultureGUID = null) {
			string[] allOnlines = Array.Empty<string>();
			if (kingdomGUID != null) {
				foreach (var player in serverAPI.World.AllOnlinePlayers) {
					if (player.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") == kingdomGUID) {
						allOnlines.AddItem(player.PlayerUID);
					}
				}
			}
			if (cultureGUID != null) {
				foreach (var player in serverAPI.World.AllOnlinePlayers) {
					if (player.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("culture_guid") == cultureGUID) {
						allOnlines.AddItem(player.PlayerUID);
					}
				}
			}
			return allOnlines;
		}

		public static string[] GetOfflineGUIDs(string kingdomGUID) {
			string[] AllMembers = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID)?.PlayersGUID.ToArray<string>();
			IServerPlayer[] AllPlayers = VSKingdom.serverAPI.World.AllPlayers as IServerPlayer[];
			string[] AllOffline = Array.Empty<string>();
			foreach (var player in AllPlayers) {
				if (!AllMembers.Contains(player.PlayerUID)) {
					AllOffline.AddItem(player.PlayerUID);
				}
			}
			return AllOffline;
		}

		public static long[] GetEntityGUIDs(string kingdomGUID) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID)?.EntitiesALL.ToArray<long>();
		}
		
		public static bool KingdomExists(string kingdomGUID = null, string kingdomNAME = null) {
			bool lookingForGUID = kingdomGUID != null;
			bool lookingForNAME = kingdomNAME != null;
			foreach (var kingdom in kingdomList) {
				if (lookingForGUID && kingdom.KingdomGUID == kingdomGUID) {
					return true;
				}
				if (lookingForNAME && kingdom.KingdomNAME == kingdomNAME) {
					return true;
				}
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
			if (target is not EntityPlayer && target is not EntitySentry) {
				return true;
			}
			if (thisKingdomGUID is null) {
				return false;
			}
			if (!target.WatchedAttributes.HasAttribute("loyalties")) {
				return true;
			}

			string thatKingdomGUID = target.WatchedAttributes.GetTreeAttribute("loyalties").GetString("kingdom_guid");

			if (thisKingdomGUID == thatKingdomGUID) {
				return false;
			}
			if (target is EntityPlayer) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thisKingdomGUID).EnemiesGUID.Contains((target as EntityPlayer).PlayerUID);
			}
			if (kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thisKingdomGUID).CurrentWars.Contains(thatKingdomGUID)) {
				return true;
			}
			if (kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thatKingdomGUID).CurrentWars.Contains(thisKingdomGUID)) {
				return true;
			}
			return false;
		}

		public static bool IsAnEnemy(Entity entity, Entity target) {
			if (target is not EntityPlayer && target is not EntitySentry) {
				return true;
			}
			if (!target.WatchedAttributes.HasAttribute("loyalties")) {
				return true;
			}

			string thisKingdomGUID = entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid");
			string thatKingdomGUID = target.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid");

			if (thisKingdomGUID == thatKingdomGUID) {
				return false;
			}
			if (target is EntityPlayer playerTarget) {
				if (entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("leaders_guid") == playerTarget.PlayerUID) {
					return false;
				}
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thisKingdomGUID).EnemiesGUID.Contains(playerTarget.PlayerUID);
			}
			return AtWarWith(thisKingdomGUID, thatKingdomGUID);
		}

		public static bool IsAFriend(string thisKingdomGUID, Entity target) {
			if (target is not EntityPlayer && target is not EntitySentry) {
				return false;
			}
			if (!target.WatchedAttributes.HasAttribute("loyalties")) {
				return false;
			}

			string thatKingdomGUID = target.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid");

			if (thisKingdomGUID == thatKingdomGUID) {
				return true;
			} else {
				return false;
			}
		}
		
		public static bool IsAFriend(Entity entity, Entity target) {
			if (target is not EntityPlayer && target is not EntitySentry) {
				return false;
			}
			if (!target.WatchedAttributes.HasAttribute("loyalties")) {
				return false;
			}

			string thisKingdomGUID = entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid");
			string thatKingdomGUID = target.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid");

			if (thisKingdomGUID == thatKingdomGUID) {
				return true;
			} else {
				return false;
			}
		}

		public static bool IsKingdom(string kingdomGUID, string kingdomNAME = null) {
			foreach (Kingdom kingdom in kingdomList) {
				if (kingdomGUID != null && kingdom.KingdomGUID == kingdomGUID) {
					return true;
				}
				if (kingdomNAME != null && kingdom.KingdomNAME.ToLowerInvariant() == kingdomNAME.ToLowerInvariant()) {
					return true;
				}
			}
			return false;
		}

		public static bool InKingdom(string playerUID) {
			foreach (Kingdom kingdom in kingdomList) {
				if (kingdom.PlayersGUID.Contains(playerUID)) {
					return true;
				}
			}
			return false;
		}
		
		public static bool InKingdom(IServerPlayer player) {
			foreach (Kingdom kingdom in kingdomList) {
				if (kingdom.PlayersGUID.Contains(player.PlayerUID)) {
					return true;
				}
			}
			return false;
		}

		public static bool NameTaken(string kingdomNAME) {
			foreach (Kingdom kingdom in kingdomList) {
					if (kingdom.KingdomNAME.ToLowerInvariant() == kingdomNAME.ToLowerInvariant()) {
						return true;
					}
				}
			return false;
		}

		public static bool AtWarWith(string thisKingdomGUID, string thatKingdomGUID) {
			if (kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thisKingdomGUID).CurrentWars.Contains(thatKingdomGUID)) {
				return true;
			}
			if (kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thatKingdomGUID).CurrentWars.Contains(thisKingdomGUID)) {
				return true;
			}
			return false;
		}

		public static IServerPlayer GetLeaders(string kingdomNAME, string kingdomGUID = null) {
			if (kingdomNAME != null) {
				return VSKingdom.serverAPI?.World.PlayerByUid(kingdomList.Find(kingdomMatch => kingdomMatch.KingdomNAME == kingdomNAME)?.LeadersGUID) as IServerPlayer;
			}
			if (kingdomGUID != null) {
				return VSKingdom.serverAPI?.World.PlayerByUid(kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID)?.LeadersGUID) as IServerPlayer;
			}
			return null;
		}
		
		public static Kingdom GetKingdom(string kingdomGUID, string kingdomNAME = null) {
			if (kingdomGUID != null) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID);
			}
			if (kingdomNAME != null) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomNAME == kingdomNAME);
			}
			return null;
		}

		public static Culture GetCulture(string cultureGUID, string cultureNAME = null) {
			if (cultureGUID != null) {
				return cultureList.Find(cultureMatch => cultureMatch.CultureGUID == cultureGUID);
			}
			if (cultureNAME != null) {
				return cultureList.Find(cultureMatch => cultureMatch.CultureNAME == cultureNAME);
			}
			return null;
		}

		public static BlockEntityPost GetOutpost(BlockPos outpostPOS) {
			if (serverAPI.World.BlockAccessor.GetBlockEntity(outpostPOS) is BlockEntityPost block) {
				return block;
			}
			return null;
		}

		public static IServerPlayer GetAPlayer(string playersNAME) {
			return serverAPI.World.AllPlayers.ToList<IPlayer>().Find(playerMatch => playerMatch.PlayerName == playersNAME) as IServerPlayer ?? null;
		}
	}
}
