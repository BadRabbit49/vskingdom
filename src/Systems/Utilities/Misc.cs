using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

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
		public static string CorrectedNAME(string kingdomNAME) {
			string oldName = kingdomNAME.Replace(" ", "_");
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

		public static string ListedAllData(string kingdomGUID) {
			byte[] kingdomData = VSKingdom.serverAPI.WorldManager.SaveGame.GetData("kingdomData");
			List<Kingdom> kingdomList = kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);
			Kingdom kingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID);
			string basicInfo = "FULL KINGDOM INFO\nGuid: " + kingdom.KingdomGUID + "\nName: " + kingdom.KingdomNAME + "\nLong: " + kingdom.KingdomLONG + "\nDesc: " + kingdom.KingdomDESC;
			string intricate = "\nLeaders Name: " + VSKingdom.serverAPI.World.PlayerByUid(kingdom.LeadersGUID).PlayerName + "\nDate Founded: " + kingdom.FoundedDATE + " (" + kingdom.FoundedMETA + ")";
			string[] nameList = new string[] { };
			foreach (string guid in kingdom.PlayerUIDs) {
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