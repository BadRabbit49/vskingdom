using System;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VSKingdom.Extension {
	internal static class CultureListExtension {
		public static bool CultureExists(this List<Culture> cultureList, string cultureGUID) {
			if (cultureList.Count == 0) {
				return false;
			} else if (cultureGUID != null && cultureList.Count > 0) {
				for (int k = 0; k < cultureList.Count; k++) {
					if (cultureList[k].CultureGUID == cultureGUID) {
						return true;
					}
				}
			}
			return false;
		}

		public static bool PartOfCulture(this List<Culture> cultureList, IPlayer player) {
			string cultureGUID = player.Entity.WatchedAttributes.GetString("cultureGUID", CommonerID);
			if (cultureGUID == null || cultureGUID == CommonerID || cultureGUID == BanditryID || cultureList.Count == 0) {
				return false;
			}
			for (int k = 0; k < cultureList.Count; k++) {
				if (cultureList[k].PlayersGUID.Contains(player.PlayerUID)) {
					return true;
				}
			}
			return false;
		}

		public static bool NameAvailable(this List<Culture> cultureList, string cultureNAME) {
			char[] badchars = { ' ', ';', ':', ',', '"', '\'', '/', '|', '\\', '-', '~', '`', '(', ')', '[', ']', '<', '>', '{', '}', '!', '@', '#', '$', '%', '^', '&', '*', '=', '+' };
			string proposed = new string(cultureNAME.ToLowerInvariant().RemoveDiacritics().Replace(badchars, '_'));
			if (cultureList.Count != 0) {
				for (int k = 0; (k < cultureList.Count); k++) {
					if (cultureList[k].CultureNAME.ToLowerInvariant().RemoveDiacritics().Replace(badchars, '_').TooClose('_', 3, 5, proposed)) {
						return false;
					}
				}
			}
			return cultureNAME != null;
		}

		public static string GetCultureNAME(this List<Culture> cultureList, string cultureGUID) {
			return cultureList.Find(cultureMatch => cultureMatch.CultureGUID == cultureGUID)?.CultureNAME;
		}

		public static string GetCultureLONG(this List<Culture> cultureList, string cultureGUID) {
			return cultureList.Find(cultureMatch => cultureMatch.CultureGUID == cultureGUID)?.CultureLONG;
		}

		public static string GetFoundersGUID(this List<Culture> cultureList, string cultureGUID) {
			if (cultureGUID != null) {
				return cultureList.Find(cultureMatch => cultureMatch.CultureGUID == cultureGUID)?.FounderGUID;
			}
			return null;
		}

		public static string[] GetCultureGUIDs(this List<Culture> cultureList) {
			string[] cultureGUIDs = Array.Empty<string>();
			for (int k = 0; (k < cultureList.Count); k++) {
				cultureGUIDs.AddItem(cultureList[k].CultureGUID);
			}
			return cultureGUIDs;
		}

		public static string[] GetOnlinesGUIDs(this List<Culture> cultureList, string cultureGUID, ICoreServerAPI sapi) {
			string[] allOnlines = Array.Empty<string>();
			if (cultureGUID == null) {
				return Array.Empty<string>();
			}
			foreach (var player in sapi.World.AllOnlinePlayers) {
				if (player.Entity.WatchedAttributes.GetString("cultureGUID") == cultureGUID) {
					allOnlines.AddItem(player.PlayerUID);
				}
			}
			return allOnlines;
		}

		public static Culture GetCulture(this List<Culture> cultureList, string cultureNAME) {
			if (cultureNAME != null) {
				return cultureList.Find(cultureMatch => cultureMatch.CultureNAME.ToLowerInvariant().RemoveDiacritics() == cultureNAME.ToLowerInvariant().RemoveDiacritics());
			}
			return null;
		}

		public static Culture Copy(this Culture culture) {
			return new Culture() {
				CultureGUID = culture.CultureGUID,
				CultureNAME = culture.CultureNAME,
				CultureLONG = culture.CultureLONG,
				CultureDESC = culture.CultureDESC,
				FounderGUID = culture.FounderGUID,
				FoundedMETA = culture.FoundedMETA,
				FoundedDATE = culture.FoundedDATE,
				FoundedHOUR = culture.FoundedHOUR,
				Predecessor = culture.Predecessor,
				PlayersGUID = culture.PlayersGUID,
				InvitesGUID = culture.InvitesGUID,
				MFirstNames = culture.MFirstNames,
				FFirstNames = culture.FFirstNames,
				FamilyNames = culture.FamilyNames,
				SkinColours = culture.SkinColours,
				HairColours = culture.HairColours,
				EyesColours = culture.EyesColours,
				HairsStyles = culture.HairsStyles,
				HairsExtras = culture.HairsExtras,
				FacesStyles = culture.FacesStyles,
				FacesBeards = culture.FacesBeards,
				WoodsBlocks = culture.WoodsBlocks,
				StoneBlocks = culture.StoneBlocks
			};
		}
	}
}
