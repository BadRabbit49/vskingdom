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
			return VSKingdom.serverAPI.PlayerData.GetPlayerDataByUid(LeadersUID)?.LastKnownPlayername;
		}

		public static string GetLeadersGUID(string kingdomGUID) {
			if (kingdomGUID != null) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID)?.LeadersGUID;
			}
			return null;
		}

		public static string GetLeadersGUID(string kingdomGUID, string PlayersUID) {
			if (kingdomGUID != null) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID)?.LeadersGUID;
			}
			if (PlayersUID is not null) {
				foreach (Kingdom kingdom in kingdomList) {
					if (kingdom.PlayersGUID.Contains(PlayersUID)) {
						return kingdom.LeadersGUID;
					}
				}
			}
			return null;
		}

		public static string[] GetPlayerGUIDs(string kingdomGUID) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID)?.PlayersGUID.ToArray<string>();
		}

		public static string[] GetEnemieGUIDs(string kingdomGUID) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID)?.EnemiesGUID.ToArray<string>();
		}

		public static string[] GetOnlineGUIDs(string kingdomGUID) {
			string[] AllMembers = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID)?.PlayersGUID.ToArray<string>();
			IPlayer[] AllPlayers = VSKingdom.serverAPI.World.AllOnlinePlayers;
			string[] AllOnlines = Array.Empty<string>();
			foreach (var player in AllPlayers) {
				if (AllMembers.Contains(player.PlayerUID)) {
					AllOnlines.AddItem(player.PlayerUID);
				}
			}
			return AllOnlines;
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
			if (VSKingdom.serverAPI.World.BlockAccessor.GetBlockEntity(outpostPOS) is BlockEntityPost block) {
				return block;
			}
			return null;
		}

		public static IServerPlayer GetAPlayer(string playersNAME) {
			return VSKingdom.serverAPI.World.AllPlayers.ToList<IPlayer>().Find(playerMatch => playerMatch.PlayerName == playersNAME) as IServerPlayer ?? null;
		}
	}
}
