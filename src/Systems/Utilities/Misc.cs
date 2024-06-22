using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace VSKingdom {
	internal static class GuidUtility {
		public static string RandomizeGUID(string GUID) {
			if (GUID == "00000000") {
				return "00000000";
			}
			Random rnd = new Random();
			StringBuilder strBuilder = new StringBuilder();
			Enumerable
				.Range(65, 26)
				.Select(e => ((char)e).ToString())
				.Concat(Enumerable.Range(97, 26).Select(e => ((char)e).ToString()))
				.Concat(Enumerable.Range(0, 7).Select(e => e.ToString()))
				.OrderBy(e => Guid.NewGuid())
				.Take(8)
				.ToList().ForEach(e => strBuilder.Append(e));
			return strBuilder.ToString();
		}
	}

	internal static class KingUtility {
		private static byte[] kingdomData { get => VSKingdom.serverAPI.WorldManager.SaveGame.GetData("kingdomData"); }
		private static List<Kingdom> kingdomList => kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);

		public static string CorrectedNAME(string kingdomNAME) {
			if (kingdomNAME.ToLower().StartsWith("the_")) {
				kingdomNAME = kingdomNAME.Remove(0, 3);
			}
			return kingdomNAME.Replace("_", " ").UcFirst();
		}

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

		public static string CorrectedLONG(string kingdomTYPE) {
			switch (kingdomTYPE) {
				case "MONARCHY": return "His Excellency";
				case "DICTATOR": return "Supreme Leader";
				case "REPUBLIC": return "Prime Minister";
				default: return "";
			}
		}

		public static string GetMemberRole(string kingdomGUID, string playersGUID) {
			Kingdom thisKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID);
			foreach(string player in thisKingdom.PlayersINFO) {
				string[] playerCard = player.Split(':');
				if (playerCard[0] == playersGUID) {
					return playerCard[2];
				}
			}
			return null;
		}

		public static string[] GetRoleNames(string membersROLE) {
			string[] allRoles = membersROLE.Split(':');
			string[] newRoles = new string[allRoles.Length - 1];
			for (int i = 0; i < allRoles.Length - 1; i++) {
				newRoles[i] = allRoles[i].Replace("/T", "").Replace("/F", "");
			}
			return newRoles;
		}

		public static bool[] GetRolePrivs(string membersROLE, string role) {
			if (membersROLE == null || membersROLE == "" || role == null || role == "") {
				return new bool[6] { false, false, false, false, false, false };
			} else if (!membersROLE.Contains(':')) {
				string[] oneRoles = membersROLE.Split('/');
				bool[] onePrivs = new bool[oneRoles.Length + 1];
				for (int i = 1; i < oneRoles.Length; i++) {
					switch (oneRoles[i]) {
						case "T": onePrivs[i - 1] = true; continue;
						case "F": onePrivs[i - 1] = false; continue;
						default: onePrivs[i - 1] = false; continue;
					}
				}
				return onePrivs;
			} else {
				string[] allRoles = membersROLE.Split(':');
				string[] curRoles = allRoles[allRoles.IndexOf(role)].Split('/');
				bool[] gotPrivs = new bool[curRoles.Length + 1];
				for (int i = 1; i < curRoles.Length; i++) {
					switch (curRoles[i]) {
						case "T": gotPrivs[i - 1] = true; continue;
						case "F": gotPrivs[i - 1] = false; continue;
						default: gotPrivs[i - 1] = false; continue;
					}
				}
				return gotPrivs;
			}
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

		public static string PlayerDetails(string kingdomGUID, string playersGUID, string membersROLE = null) {
			Kingdom thisKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID);
			IPlayer thisPlayer = VSKingdom.serverAPI.World.PlayerByUid(playersGUID);
			string joinedRole = thisKingdom.MembersROLE.Split(':')[0].Split('/')[0];
			string[] roles = GetRoleNames(thisKingdom.MembersROLE);
			if (membersROLE != null && roles.Contains(membersROLE)) {
				joinedRole = roles[roles.IndexOf(membersROLE)];
			}
			return playersGUID + ":" + thisPlayer.PlayerName + ":" + joinedRole + ":" + DateTime.Now.ToShortDateString();
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