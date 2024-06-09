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

		public static string GetKingdomNAME(string KingdomGUID) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == KingdomGUID)?.KingdomNAME;
		}

		public static string GetKingdomGUID(string KingdomNAME) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomNAME.ToLowerInvariant() == KingdomNAME.ToLowerInvariant())?.KingdomGUID;
		}

		public static string GetKingdomGUID(IServerPlayer Player) {
			return Player.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") ?? null;
		}

		public static string GetLeadersName(string LeadersUID) {
			return VSKingdom.serverAPI.PlayerData.GetPlayerDataByUid(LeadersUID)?.LastKnownPlayername;
		}

		public static string GetLeadersGUID(string KingdomGUID) {
			if (KingdomGUID != null) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == KingdomGUID)?.LeadersGUID;
			}
			return null;
		}

		public static string GetLeadersGUID(string KingdomGUID, string PlayersUID) {
			if (KingdomGUID != null) {
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

		public static string[] GetPlayerGUIDs(string KingdomGUID) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == KingdomGUID)?.PlayerUIDs.ToArray<string>();
		}

		public static string[] GetEnemieGUIDs(string KingdomGUID) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == KingdomGUID)?.EnemieUIDs.ToArray<string>();
		}

		public static string[] GetOnlineGUIDs(string KingdomGUID) {
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

		public static string[] GetOfflineGUIDs(string KingdomGUID) {
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

		public static long[] GetEntityGUIDs(string KingdomGUID) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == KingdomGUID)?.EntityUIDs.ToArray<long>();
		}
		
		public static bool KingdomExists(string KingdomGUID = null, string KingdomNAME = null) {
			bool lookingForGUID = KingdomGUID != null;
			bool lookingForNAME = KingdomNAME != null;
			foreach (var kingdom in kingdomList) {
				if (lookingForGUID && kingdom.KingdomGUID == KingdomGUID) {
					return true;
				}
				if (lookingForNAME && kingdom.KingdomNAME == KingdomNAME) {
					return true;
				}
			}
			return false;
		}

		public static bool CultureExists(string CultureGUID = null, string CultureNAME = null) {
			bool lookingForGUID = CultureGUID != null;
			bool lookingForNAME = CultureNAME != null;
			foreach (var culture in cultureList) {
				if (lookingForGUID && culture.CultureGUID == CultureGUID) {
					return true;
				}
				if (lookingForNAME && culture.CultureNAME == CultureNAME) {
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
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thisKingdomGUID).EnemieUIDs.Contains(playerTarget.PlayerUID);
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

		public static bool IsKingdom(string kingdomGUID, string kingdomNAME) {
			if (kingdomGUID is not null) {
				foreach (Kingdom kingdom in kingdomList) {
					if (kingdom.KingdomGUID.ToLowerInvariant() == kingdomGUID.ToLowerInvariant()) {
						return true;
					}
				}
			}
			if (kingdomNAME is not null) {
				foreach (Kingdom kingdom in kingdomList) {
					if (kingdom.KingdomNAME.ToLowerInvariant() == kingdomNAME.ToLowerInvariant()) {
						return true;
					}
				}
			}
			return false;
		}

		public static bool InKingdom(string playerUID) {
			foreach (Kingdom kingdom in kingdomList) {
				if (kingdom.PlayerUIDs.Contains(playerUID)) {
					return true;
				}
			}
			return false;
		}
		
		public static bool InKingdom(IServerPlayer player) {
			foreach (Kingdom kingdom in kingdomList) {
				if (kingdom.PlayerUIDs.Contains(player.PlayerUID)) {
					return true;
				}
			}
			return false;
		}

		public static bool NameTaken(string kingdomNAME, string cultureNAME = null) {
			if (kingdomNAME != null) {
				foreach (Kingdom kingdom in kingdomList) {
					if (kingdom.KingdomNAME.ToLowerInvariant() == kingdomNAME.ToLowerInvariant()) {
						return true;
					}
				}
			}
			if (cultureNAME != null) {
				foreach (Culture culture in cultureList) {
					if (culture.CultureNAME.ToLowerInvariant() == cultureNAME.ToLowerInvariant()) {
						return true;
					}
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

		public static IServerPlayer GetLeaders(string KingdomNAME, string KingdomGUID = null) {
			if (KingdomNAME != null) {
				return VSKingdom.serverAPI?.World.PlayerByUid(kingdomList.Find(kingdomMatch => kingdomMatch.KingdomNAME == KingdomNAME)?.LeadersGUID) as IServerPlayer;
			}
			if (KingdomGUID != null) {
				return VSKingdom.serverAPI?.World.PlayerByUid(kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == KingdomGUID)?.LeadersGUID) as IServerPlayer;
			}
			return null;
		}

		public static IServerPlayer GetAPlayer(string PlayerUID) {
			return VSKingdom.serverAPI.World.AllPlayers.ToList<IPlayer>().Find(playerMatch => playerMatch.PlayerUID == PlayerUID) as IServerPlayer ?? null;
		}
		
		public static Kingdom GetKingdom(string KingdomGUID, string KingdomNAME = null) {
			if (KingdomGUID != null) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == KingdomGUID);
			}
			if (KingdomNAME != null) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomNAME == KingdomNAME);
			}
			return null;
		}

		public static Culture GetCulture(string CultureGUID, string CultureNAME = null) {
			if (CultureGUID != null) {
				return cultureList.Find(cultureMatch => cultureMatch.CultureGUID == CultureGUID);
			}
			if (CultureNAME != null) {
				return cultureList.Find(cultureMatch => cultureMatch.CultureNAME == CultureNAME);
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
