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
		private static List<Kingdom> kingdomList => kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);

		public static string GetKingdomName(string KingdomUID) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomUID == KingdomUID)?.KingdomName;
		}

		public static string GetKingdomUID(string KingdomName) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomName.ToLowerInvariant() == KingdomName.ToLowerInvariant())?.KingdomUID;
		}

		public static string GetKingdomUID(IServerPlayer Player) {
			return Player.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID") ?? null;
		}

		public static string GetLeadersName(string LeadersUID) {
			return VSKingdom.serverAPI.PlayerData.GetPlayerDataByUid(LeadersUID)?.LastKnownPlayername;
		}

		public static string GetLeadersUID(string KingdomUID, string PlayersUID) {
			if (KingdomUID is not null) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomUID == KingdomUID)?.LeadersUID;
			}
			if (PlayersUID is not null) {
				foreach (Kingdom kingdom in kingdomList) {
					if (kingdom.PlayerUIDs.Contains(PlayersUID)) {
						return kingdom.LeadersUID;
					}
				}
			}
			return null;
		}

		public static string[] GetPlayerUIDs(string KingdomUID) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomUID == KingdomUID)?.PlayerUIDs.ToArray<string>();
		}

		public static string[] GetEnemieUIDs(string KingdomUID) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomUID == KingdomUID)?.EnemieUIDs.ToArray<string>();
		}

		public static string[] GetOnlineUIDs(string KingdomUID) {
			string[] AllMembers = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomUID == KingdomUID)?.PlayerUIDs.ToArray<string>();
			IPlayer[] AllPlayers = VSKingdom.serverAPI.World.AllOnlinePlayers;
			string[] AllOnlines = Array.Empty<string>();
			foreach (var player in AllPlayers) {
				if (AllMembers.Contains(player.PlayerUID)) {
					AllOnlines.AddItem(player.PlayerUID);
				}
			}
			return AllOnlines;
		}

		public static string[] GetOfflineUIDs(string KingdomUID) {
			string[] AllMembers = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomUID == KingdomUID)?.PlayerUIDs.ToArray<string>();
			IServerPlayer[] AllPlayers = VSKingdom.serverAPI.World.AllPlayers as IServerPlayer[];
			string[] AllOffline = Array.Empty<string>();
			foreach (var player in AllPlayers) {
				if (!AllMembers.Contains(player.PlayerUID)) {
					AllOffline.AddItem(player.PlayerUID);
				}
			}
			return AllOffline;
		}

		public static long[] GetEntityUIDs(string KingdomUID) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomUID == KingdomUID)?.EntityUIDs.ToArray<long>();
		}
		
		public static bool KingdomExists(string KingdomName, string KingdomUID) {
			if (KingdomName is not null) {
				return kingdomList.Contains(kingdomList.Find(kingdomMatch => kingdomMatch.KingdomName.ToLowerInvariant() == KingdomName.ToLowerInvariant()));
			}
			if (KingdomUID is not null) {
				return kingdomList.Contains(kingdomList.Find(kingdomMatch => kingdomMatch.KingdomUID == KingdomUID));
			}
			return false;
		}

		public static bool IsAnEnemy(string thisKingdomUID, string thatKingdomUID) {
			if (thisKingdomUID == thatKingdomUID) {
				return false;
			}
			return AtWarWith(thisKingdomUID, thatKingdomUID);
		}

		public static bool IsAnEnemy(string thisKingdomUID, Entity target) {
			if (target is not EntityPlayer && target is not EntityArcher) {
				return true;
			}
			if (thisKingdomUID is null) {
				return false;
			}
			if (!target.WatchedAttributes.HasAttribute("loyalties")) {
				return true;
			}
			if (kingdomList.Find(kingdomMatch => kingdomMatch.KingdomUID == thisKingdomUID).EntityUIDs.Contains(target.EntityId)) {
				return false;
			}

			string thatKingdomUID = target.WatchedAttributes.GetTreeAttribute("loyalties").GetString("kingdomUID");

			if (thisKingdomUID == thatKingdomUID) {
				return false;
			}
			if (target is EntityPlayer) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomUID == thisKingdomUID).EnemieUIDs.Contains((target as EntityPlayer).PlayerUID);
			}
			if (kingdomList.Find(kingdomMatch => kingdomMatch.KingdomUID == thisKingdomUID).AtWarsUIDs.Contains(thatKingdomUID)) {
				return true;
			}
			if (kingdomList.Find(kingdomMatch => kingdomMatch.KingdomUID == thatKingdomUID).AtWarsUIDs.Contains(thisKingdomUID)) {
				return true;
			}
			return false;
		}

		public static bool IsAnEnemy(Entity entity, Entity target) {
			if (target is not EntityPlayer && target is not EntityArcher) {
				return true;
			}
			if (!target.WatchedAttributes.HasAttribute("loyalties")) {
				return true;
			}

			string thisKingdomUID = entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID");
			string thatKingdomUID = target.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID");

			if (thisKingdomUID == thatKingdomUID) {
				return false;
			}
			if (target is EntityPlayer playerTarget) {
				if (entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("leadersUID") == playerTarget.PlayerUID) {
					return false;
				}
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomUID == thisKingdomUID).EnemieUIDs.Contains(playerTarget.PlayerUID);
			}
			return AtWarWith(thisKingdomUID, thatKingdomUID);
		}

		public static bool IsAFriend(string thisKingdomUID, Entity target) {
			if (target is not EntityPlayer && target is not EntityArcher) {
				return false;
			}
			if (!target.WatchedAttributes.HasAttribute("loyalties")) {
				return false;
			}

			string thatKingdomUID = target.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID");

			if (thisKingdomUID == thatKingdomUID) {
				return true;
			} else {
				return false;
			}
		}
		
		public static bool IsAFriend(Entity entity, Entity target) {
			if (target is not EntityPlayer && target is not EntityArcher) {
				return false;
			}
			if (!target.WatchedAttributes.HasAttribute("loyalties")) {
				return false;
			}

			string thisKingdomUID = entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID");
			string thatKingdomUID = target.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID");

			if (thisKingdomUID == thatKingdomUID) {
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

		public static bool AtWarWith(string thisKingdomUID, string thatKingdomUID) {
			if (kingdomList.Find(kingdomMatch => kingdomMatch.KingdomUID == thisKingdomUID).AtWarsUIDs.Contains(thatKingdomUID)) {
				return true;
			}
			if (kingdomList.Find(kingdomMatch => kingdomMatch.KingdomUID == thatKingdomUID).AtWarsUIDs.Contains(thisKingdomUID)) {
				return true;
			}
			return false;
		}

		public static IServerPlayer GetLeaders(string KingdomName, string KingdomUID) {
			if (KingdomName is not null) {
				return VSKingdom.serverAPI?.World.PlayerByUid(kingdomList.Find(kingdomMatch => kingdomMatch.KingdomName == KingdomName)?.LeadersUID) as IServerPlayer;
			}
			if (KingdomUID is not null) {
				return VSKingdom.serverAPI?.World.PlayerByUid(kingdomList.Find(kingdomMatch => kingdomMatch.KingdomUID == KingdomUID)?.LeadersUID) as IServerPlayer;
			}
			return null;
		}

		public static IServerPlayer GetAPlayer(string PlayerUID) {
			return VSKingdom.serverAPI.World.AllPlayers.ToList<IPlayer>().Find(playerMatch => playerMatch.PlayerUID == PlayerUID) as IServerPlayer ?? null;
		}
		
		public static Kingdom GetKingdom(string KingdomName, string KingdomUID) {
			if (KingdomName is not null) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomName == KingdomName);
			}
			if (KingdomUID is not null) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomUID == KingdomUID);
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
