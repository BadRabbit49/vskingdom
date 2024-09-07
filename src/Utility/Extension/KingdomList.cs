using System;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VSKingdom.Extension {
	internal static class KingdomListExtension {
		public static bool KingdomExists(this List<Kingdom> kingdomList, string kingdomGUID) {
			return kingdomList.Exists(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID);
		}

		public static bool PartOfKingdom(this List<Kingdom> kingdomList, IPlayer player) {
			string kingdomGUID = player.Entity.WatchedAttributes.GetString("kingdomGUID", commonerGUID);
			if (kingdomGUID == null || kingdomGUID == commonerGUID || kingdomGUID == banditryGUID || kingdomList.Count == 0) {
				return false;
			}
			for (int k = 0; k < kingdomList.Count; k++) {
				if (kingdomList[k].PlayersGUID.Contains(player.PlayerUID)) {
					return true;
				}
			}
			return false;
		}

		public static bool NameAvailable(this List<Kingdom> kingdomList, string kingdomNAME) {
			char[] badchars = { ' ', ';', ':', ',', '"', '\'', '/', '|', '\\', '-', '~', '`', '(', ')', '[', ']', '<', '>', '{', '}', '!', '@', '#', '$', '%', '^', '&', '*', '=', '+' };
			string proposed = new string(kingdomNAME.ToLowerInvariant().RemoveDiacritics().Replace(badchars, '_'));
			if (kingdomList.Count != 0) {
				for (int k = 0; (k < kingdomList.Count); k++) {
					if (kingdomList[k].KingdomNAME.ToLowerInvariant().RemoveDiacritics().Replace(badchars, '_').TooClose('_', 3, 5, proposed)) {
						return false;
					}
				}
			}
			return kingdomNAME != null;
		}

		public static string GetKingdomNAME(this List<Kingdom> kingdomList, string kingdomGUID) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID)?.KingdomNAME;
		}

		public static string GetKingdomLONG(this List<Kingdom> kingdomList, string kingdomGUID) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID)?.KingdomLONG;
		}

		public static string GetLeadersGUID(this List<Kingdom> kingdomList, string kingdomGUID) {
			if (kingdomGUID != null) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID)?.LeadersGUID;
			}
			return null;
		}

		public static string[] GetKingdomCOLR(this List<Kingdom> kingdomList, string kingdomGUID) {
			Kingdom kingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID);
			if (kingdom == null) {
				return Array.Empty<string>();
			}
			return new string[3] { kingdom.KingdomHEXA, kingdom.KingdomHEXB, kingdom.KingdomHEXC };
		}

		public static string[] GetKingdomGUIDs(this List<Kingdom> kingdomList) {
			string[] kingdomGUIDs = Array.Empty<string>();
			for (int k = 0; (k < kingdomList.Count); k++) {
				kingdomGUIDs.AddItem(kingdomList[k].KingdomGUID);
			}
			return kingdomGUIDs;
		}

		public static string[] GetOnlinesGUIDs(this List<Kingdom> kingdomList, string kingdomGUID, ICoreServerAPI sapi) {
			string[] allOnlines = Array.Empty<string>();
			if (kingdomGUID == null) {
				return Array.Empty<string>();
			}
			foreach (var player in sapi.World.AllOnlinePlayers) {
				if (player.Entity.WatchedAttributes.GetString("kingdomGUID") == kingdomGUID) {
					allOnlines.AddItem(player.PlayerUID);
				}
			}
			return allOnlines;
		}

		public static Kingdom GetKingdom(this List<Kingdom> kingdomList, string kingdomNAME) {
			if (kingdomNAME != null) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomNAME.ToLowerInvariant().RemoveDiacritics() == kingdomNAME.ToLowerInvariant().RemoveDiacritics());
			}
			return null;
		}
	}
}
