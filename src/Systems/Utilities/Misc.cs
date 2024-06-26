﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Util;

namespace VSKingdom {
	internal static class GuidUtility {
		public static string RandomizeGUID(string GUID, int SIZE = 8, string[] LIST = null) {
			if (GUID != null && GUID.Length == SIZE) {
				return GUID;
			}
			bool repeating = true;
			Random rnd = new Random();
			StringBuilder strBuilder = new StringBuilder();
			while (repeating) {
				strBuilder.Clear();
				Enumerable
					.Range(65, 26).Select(e => ((char)e).ToString())
					.Concat(Enumerable.Range(97, 26).Select(e => ((char)e).ToString()))
					.Concat(Enumerable.Range(0, 7).Select(e => e.ToString()))
					.OrderBy(e => Guid.NewGuid()).Take(SIZE)
					.ToList().ForEach(e => strBuilder.Append(e));
				if (LIST != null) {
					repeating = LIST.Contains(strBuilder.ToString());
				} else {
					repeating = false;
				}
			}
			return strBuilder.ToString();
		}
	}

	internal static class KingUtility {
		private static byte[] kingdomData { get => VSKingdom.serverAPI.WorldManager.SaveGame.GetData("kingdomData"); }
		private static List<Kingdom> kingdomList => kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);

		public static string CorrectedTYPE(string kingdomNAME) {
			string[] monTypes = { "kingdom", "monarchy", "dynasty", "commonwealth", "empire", "imperium", "sultanate", "fiefdom", "tribal", "tribe" };
			string[] dicTypes = { "dictatorship", "administration", "state", "authority", "people's" };
			string[] repTypes = { "republic", "united", "republik", "república", "repubblica", "anarchal", "commune" };
			for (int i = 0; i < monTypes.Length; i++) {
				if (kingdomNAME.IndexOf(monTypes[i], StringComparison.OrdinalIgnoreCase) >= 0) {
					return "MONARCHY";
				}
			}
			for (int i = 0; i < dicTypes.Length; i++) {
				if (kingdomNAME.IndexOf(dicTypes[i], StringComparison.OrdinalIgnoreCase) >= 0) {
					return "DICTATOR";
				}
			}
			for (int i = 0; i < repTypes.Length; i++) {
				if (kingdomNAME.IndexOf(repTypes[i], StringComparison.OrdinalIgnoreCase) >= 0) {
					return "REPUBLIC";
				}
			}
			return "MONARCHY";
		}

		public static string CorrectedNAME(string kingdomTYPE, string kingdomVARS, bool getName = false, bool getLong = false) {
			if (getName) {
				if (kingdomVARS.ToLower().StartsWith("the ")) {
					kingdomVARS = kingdomVARS.Remove(0, 3);
				}
				return kingdomVARS.Replace("_", " ").UcFirst();
			}
			if (getLong) {
				switch (kingdomTYPE) {
					case "MONARCHY": return "the " + kingdomVARS + " Kingdom";
					case "DICTATOR": return "the " + kingdomVARS + " Federation";
					case "REPUBLIC": return "the " + kingdomVARS + " Republic";
					default: return kingdomVARS ?? null;
				}
			}
			return null;
		}

		public static string CorrectedLEAD(string kingdomTYPE, bool getName = false, bool getLong = false, bool getDesc = false) {
			if (getName) {
				switch (kingdomTYPE) {
					case "MONARCHY": return "King";
					case "DICTATOR": return "Chairman";
					case "REPUBLIC": return "Senator";
					default: return null;
				}
			}
			if (getLong) {
				switch (kingdomTYPE) {
					case "MONARCHY": return "His Excellency";
					case "DICTATOR": return "Supreme Leader";
					case "REPUBLIC": return "Prime Minister";
					default: return null;
				}
			}
			if (getDesc) {
				switch (kingdomTYPE) {
					case "MONARCHY": return "Sole monarch of the kingdom, their powers are absolute and chosen by divine right or inheritence.";
					case "DICTATOR": return "Despotic ruler given near total power to oversee almost every aspect of society, from war to peace.";
					case "REPUBLIC": return "Elected official given limited powers by the people, their position is fluid and often in terms.";
					default: return null;
				}
			}
			return null;
		}

		public static string CorrectedROLE(string kingdomTYPE) {
			switch (kingdomTYPE) {
				case "MONARCHY": return "Peasant/F/F/F/F/F/F:Soldier/T/T/T/F/T/T:Lordship/T/T/T/T/T/T";
				case "DICTATOR": return "Subject/T/T/F/F/F/F:Trooper/T/T/F/F/T/T:Oligarch/T/T/T/T/T/T";
				case "REPUBLIC": return "Citizen/T/T/T/T/F/T:Officer/T/T/T/F/T/T:Official/T/T/T/F/T/T";
				default: return "Peasant/F/F/F/F/F/F:Citizen/T/T/T/F/F/T:Soldier/T/T/T/F/T/T:Royalty/T/T/T/T/T/T";
			}
		}

		public static string GetMemberINFO(HashSet<string> playersINFO, string playersGUID) {
			foreach (string player in playersINFO) {
				if (player.Split(':')[0] == playersGUID) {
					return player;
				}
			}
			return null;
		}

		public static string GetMemberROLE(HashSet<string> playersINFO, string playersGUID) {
			foreach (string player in playersINFO) {
				string[] playerCard = player.Split(':');
				if (playerCard[0] == playersGUID) {
					return playerCard[1].Replace("/T", "").Replace("/F", "");
				}
			}
			return null;
		}

		public static string GetLeaderROLE(string membersROLE) {
			string[] allRoles = membersROLE.Replace("/T", "").Replace("/F", "").Split(':');
			return allRoles[allRoles.Length - 1];
		}

		public static string[] GetRolesNAME(string membersROLE) {
			string[] allRoles = membersROLE.Replace("/T", "").Replace("/F", "").Split(':');
			return allRoles;
		}

		public static bool[] GetRolesPRIV(string membersROLE, string role) {
			if (membersROLE == null || membersROLE == "" || role == null || role == "") {
				return new bool[6] { false, false, false, false, false, false };
			}
			string[] curRoles = new string[] { };
			bool[] gotPrivs = new bool[] { };
			if (!membersROLE.Contains(':')) {
				curRoles = membersROLE.Split('/');
			} else {
				curRoles = membersROLE.Split(':')[membersROLE.Replace("/T", "").Replace("/F", "").Split(':').IndexOf(role)].Split('/');
			}
			gotPrivs = new bool[curRoles.Length - 1];
			for (int i = 1; i < curRoles.Length; i++) {
				switch (curRoles[i]) {
					case "T": gotPrivs[i - 1] = true; continue;
					case "F": gotPrivs[i - 1] = false; continue;
					default: gotPrivs[i - 1] = false; continue;
				}
			}
			return gotPrivs;
		}

		public static string MostSeniority(string kingdomGUID) {
			Kingdom thisKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID);
			string[] playerInfo = thisKingdom.PlayersINFO.ToArray();
			string oldestPlayerGuid = playerInfo[0].Split(':')[0];
			int oldestPlayerMnth = Int32.Parse(playerInfo[0].Split(':')[3].Split('/')[0]);
			int oldestPlayerDays = Int32.Parse(playerInfo[0].Split(':')[3].Split('/')[1]);
			int oldestPlayerYear = Int32.Parse(playerInfo[0].Split(':')[3].Split('/')[2]);
			foreach (string player in playerInfo) {
				string[] fullDate = player.Split(':')[3].Split('/');
				int m = Int32.Parse(fullDate[0]);
				int d = Int32.Parse(fullDate[1]);
				int y = Int32.Parse(fullDate[2]);
				if (y < oldestPlayerYear || (y == oldestPlayerYear && m < oldestPlayerMnth) || (y == oldestPlayerYear && m == oldestPlayerMnth && d < oldestPlayerDays)) {
					oldestPlayerMnth = m;
					oldestPlayerDays = d;
					oldestPlayerYear = y;
					oldestPlayerGuid = player.Split(':')[0];
					continue;
				}
			}
			return oldestPlayerGuid; 
		}

		public static string PlayerDetails(string playersGUID, string membersROLE = null, string specifcROLE = null) {
			string[] allRoles = GetRolesNAME(membersROLE);
			string joinedRole = membersROLE.Split(':')[allRoles.IndexOf(specifcROLE)];
			if (specifcROLE == null || !allRoles.Contains(specifcROLE)) {
				joinedRole = membersROLE.Split(':')[0].Split('/')[0];
			}
			return playersGUID + ":" + joinedRole + ":" + DateTime.Now.ToShortDateString();
		}

		public static string ListedAllData(string kingdomGUID) {
			Kingdom kingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID);
			string basicInfo = "FULL KINGDOM INFO\nGuid: " + kingdom.KingdomGUID + "\nName: " + kingdom.KingdomNAME + "\nLong: " + kingdom.KingdomLONG + "\nDesc: " + kingdom.KingdomDESC;
			string intricate = "\nLeaders Name: " + VSKingdom.serverAPI.World.PlayerByUid(kingdom.LeadersGUID).PlayerName + "\nDate Founded: " + kingdom.FoundedDATE + " (" + kingdom.FoundedMETA + ")";
			string[] nameList = new string[] { };
			foreach (string guid in kingdom.PlayersGUID) {
				try { nameList.Append(VSKingdom.serverAPI.World.PlayerByUid(guid).PlayerName); } catch { }
			}
			return basicInfo + intricate + "\nPLAYER LIST" + LangUtility.Msg(nameList);
		}
	}

	internal static class CultUtility {
		public static string CorrectedNAME(string cultureNAME) {
			string oldName = cultureNAME.Replace(" ", "_");
			string newName = "";
			string[] fullName = oldName.Split('_');
			for (int i = 0; i < fullName.Length; i++) {
				if (i == 0 && (fullName[i].IndexOf("the", StringComparison.OrdinalIgnoreCase) >= 0)) {
					continue;
				}
				newName += fullName[i] + "_";
			}
			return newName.Remove(newName.Length - 1);
		}
		
		public static string CorrectedLONG(string cultureNAME) {
			string oldName = cultureNAME.Replace(" ", "_");
			foreach (var addCombo in FixedLiterature.CulturalAdditionSuffixes) {
				if (oldName.EndsWith(addCombo.Key)) {
					return oldName + addCombo.Value;
				}
			}
			foreach (var repCombo in FixedLiterature.CulturalReplacedSuffixes) {
				if (oldName.EndsWith(repCombo.Key)) {
					return oldName.Remove((oldName.Length - 1) - (repCombo.Key.Length - 1)) + repCombo.Value;
				}
			}
			return oldName;
		}

		public static string ListedAllData(string cultureGUID) {
			byte[] cultureData = VSKingdom.serverAPI.WorldManager.SaveGame.GetData("cultureData");
			List<Culture> cultureList = cultureData is null ? new List<Culture>() : SerializerUtil.Deserialize<List<Culture>>(cultureData);
			Culture culture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == cultureGUID);
			string basicInfo = "FULL CULTURE INFO\nGuid: " + culture.CultureGUID + "\nName: " + culture.CultureNAME + "\nLong: " + culture.CultureLONG + "\nDesc: " + culture.CultureDESC;
			string intricate = "\nFounder Name: " + VSKingdom.serverAPI.World.PlayerByUid(culture.FounderGUID).PlayerName + "\nDate Founded: " + culture.FoundedDATE + " (" + culture.FoundedMETA + ")";
			return basicInfo + intricate;
		}
	}
}