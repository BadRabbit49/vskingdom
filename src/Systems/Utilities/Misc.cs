﻿using System;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Collections.Generic;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VSKingdom {
	internal static class KingdomUtility {
		public static string CorrectedType(string kingdomNAME) {
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

		public static string CorrectedName(string kingdomTYPE, string kingdomVARS, bool getName = false, bool getLong = false) {
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

		public static string CorrectedLead(string kingdomTYPE, bool getName = false, bool getLong = false, bool getDesc = false) {
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

		public static string CorrectedRole(string kingdomTYPE) {
			switch (kingdomTYPE) {
				case "MONARCHY": return "Peasant/F/F/F/F/F/F:Soldier/T/T/T/F/T/T:Lordship/T/T/T/T/T/T";
				case "DICTATOR": return "Subject/T/T/F/F/F/F:Trooper/T/T/F/F/T/T:Oligarch/T/T/T/T/T/T";
				case "REPUBLIC": return "Citizen/T/T/T/T/F/T:Officer/T/T/T/F/T/T:Official/T/T/T/F/T/T";
				default: return "Peasant/F/F/F/F/F/F:Citizen/T/T/T/F/F/T:Soldier/T/T/T/F/T/T:Royalty/T/T/T/T/T/T";
			}
		}

		public static string GetMemberInfo(HashSet<string> playersINFO, string playersGUID) {
			foreach (string player in playersINFO) {
				if (player.Split(':')[0] == playersGUID) {
					return player;
				}
			}
			return null;
		}

		public static string GetMemberRole(HashSet<string> playersINFO, string playersGUID) {
			foreach (string player in playersINFO) {
				string[] playerCard = player.Split(':');
				if (playerCard[0] == playersGUID) {
					return playerCard[1].Replace("/T", "").Replace("/F", "");
				}
			}
			return null;
		}

		public static string GetLeaderRole(string membersROLE) {
			string[] allRoles = membersROLE.Replace("/T", "").Replace("/F", "").Split(':');
			return allRoles[allRoles.Length - 1];
		}

		public static string[] GetRolesName(string membersROLE) {
			string[] allRoles = membersROLE.Replace("/T", "").Replace("/F", "").Split(':');
			return allRoles;
		}

		public static bool[] GetRolesPriv(string membersROLE, string role) {
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

		public static string MostSeniority(ICoreServerAPI sapi, string kingdomGUID) {
			byte[] kingdomData = sapi.WorldManager.SaveGame.GetData("kingdomData");
			List<Kingdom> kingdomList = kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);
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
			string[] allRoles = GetRolesName(membersROLE);
			string joinedRole = membersROLE.Split(':')[allRoles.IndexOf(specifcROLE)];
			if (specifcROLE == null || !allRoles.Contains(specifcROLE)) {
				joinedRole = membersROLE.Split(':')[0].Split('/')[0];
			}
			return playersGUID + ":" + joinedRole + ":" + DateTime.Now.ToShortDateString();
		}
		
		public static string ListedAllData(ICoreServerAPI sapi, string kingdomGUID) {
			byte[] kingdomData = sapi.WorldManager.SaveGame.GetData("kingdomData");
			List<Kingdom> kingdomList = kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);
			Kingdom kingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID);
			string basicInfo = "FULL KINGDOM INFO\nGuid: " + kingdom.KingdomGUID + "\nName: " + kingdom.KingdomNAME + "\nLong: " + kingdom.KingdomLONG + "\nDesc: " + kingdom.KingdomDESC;
			string intricate = "\nLeaders Name: " + sapi.World.PlayerByUid(kingdom.LeadersGUID).PlayerName + "\nDate Founded: " + kingdom.FoundedDATE + " (" + kingdom.FoundedMETA + ")";
			string[] nameList = new string[] { };
			foreach (string guid in kingdom.PlayersGUID) {
				try { nameList.Append(sapi.World.PlayerByUid(guid).PlayerName); } catch { }
			}
			return basicInfo + intricate + "\nPLAYER LIST" + LangUtility.Msg(nameList);
		}
	}

	internal static class CultureUtility {
		public static string CorrectedName(string cultureNAME) {
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
		
		public static string CorrectedLong(string cultureNAME) {
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

		public static string ListedAllData(ICoreServerAPI sapi, string cultureGUID) {
			byte[] kingdomData = sapi.WorldManager.SaveGame.GetData("kingdomData");
			List<Kingdom> kingdomList = kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);
			byte[] cultureData = sapi.WorldManager.SaveGame.GetData("cultureData");
			List<Culture> cultureList = cultureData is null ? new List<Culture>() : SerializerUtil.Deserialize<List<Culture>>(cultureData);
			Culture culture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == cultureGUID);
			string basicInfo = $"FULL CULTURE INFO\nGuid: {culture.CultureGUID}\nName: {culture.CultureNAME}\nLong: {culture.CultureLONG}\nDesc: {culture.CultureDESC}";
			string intricate = $"\nFounder Name: {sapi.World.PlayerByUid(culture.FounderGUID).PlayerName}\nDate Founded: {culture.FoundedDATE} ({culture.FoundedMETA})";
			return basicInfo + intricate;
		}
	}
	
	internal static class ColoursUtility {
		public static string RandomizeCode(int minHue = 0, int maxHue = 255) {
			if (minHue < 000) { minHue = 000; }
			if (maxHue > 255) { maxHue = 255; }
			Random rnd = new Random();
			int colorR = rnd.Next(minHue, maxHue);
			int colorG = rnd.Next(minHue, maxHue);
			int colorB = rnd.Next(minHue, maxHue);
			Color srgb = Color.FromArgb(colorR, colorG, colorB);
			string hex = srgb.R.ToString("X2") + srgb.G.ToString("X2") + srgb.B.ToString("X2");
			return "#" + hex;
		}

		public static string GetHexCode(string color) {
			if (color.StartsWith('#')) {
				char[] hex = color.Where(c => (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '-')).ToArray();
				if (hex.Length >= 6) {
					return new string(new char[] { '#', hex[0], hex[1], hex[2], hex[3], hex[4], hex[5] });
				}
			}
			string fixedCode = color.ToLowerInvariant().RemoveDiacritics().Replace("-", "").Replace("_", "");
			switch (fixedCode) {
				case "black": return "#0d0d0d";
				case "bloodred": return "#590000";
				case "blue": return "#115691";
				case "brightgreen": return "#7ee673";
				case "brightred": return "#ff3030";
				case "brown": return "#4f290d";
				case "darkblue": return "#05335c";
				case "darkbrown": return "#261307";
				case "darkgray": return "#454545";
				case "darkgreen": return "#0b2e12";
				case "darkgrey": return "#353535";
				case "darkpink": return "#964792";
				case "darkpurple": return "#6a007a";
				case "darkred": return "#630c06";
				case "darkyellow": return "#a69712";
				case "deepred": return "#290300";
				case "forestgreen": return "#26422c";
				case "gray": return "#707070";
				case "green": return "#36753c";
				case "grey": return "#606060";
				case "honey": return "#ffb300";
				case "jeanblue": return "#142636";
				case "lightblue": return "#3a9cf2";
				case "lightbrown": return "#735948";
				case "lightgray": return "#a8a8a8";
				case "lightgreen": return "#5a9967";
				case "lightgrey": return "#8f8f8f";
				case "lightpink": return "#ffa8fb";
				case "lightpurple": return "#a46aad";
				case "lightred": return "#fc5f53";
				case "lightyellow": return "#d9d18d";
				case "magenta": return "#eb0056";
				case "navyblue": return "#091b2b";
				case "orange": return "#d65611";
				case "pink": return "#ff69f7";
				case "purple": return "#a018b5";
				case "red": return "#f0190a";
				case "skyblue": return "#73b0e6";
				case "vanta": return "#000000";
				case "white": return "#ffffff";
				case "yellow": return "#f7e223";
				case "random": return string.Format("#{0:X6}", new Random().Next(0x1000000));
				default: return "#ffffff";
			}
		}
	}
}