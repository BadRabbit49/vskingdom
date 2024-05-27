using System;
using System.Linq;
using System.Text;

namespace VSKingdom {
	internal static class KingUtility {
		public static string CorrectKingdomGUID(string kingdomGUID) {
			if (kingdomGUID == "00000000") {
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

		public static string CorrectKingdomName(string kingdomName) {
			string oldName = kingdomName.Replace(" ", "_");
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

		public static string DefaultKingdomDesc(string kingdomType) {
			switch (kingdomType) {
				case "MONARCHY": return "Humble kingdom with small roots and population.";
				case "DICTATOR": return "Dedicated state authority with great ambitions.";
				case "REPUBLIC": return "Democratic union with hopes to someday prosper.";
				default: return "";
			}
		}

		public static string DefaultKingdomType(string kingdomName) {
			string[] monTypes = { "kingdom", "monarchy", "dynasty", "commonwealth", "empire", "imperium", "sultanate", "fiefdom", "tribal", "tribe" };
			string[] dicTypes = { "dictatorship", "administration", "state", "authority", "people's" };
			string[] repTypes = { "republic", "united", "republik", "república", "repubblica", "anarchal", "commune" };
			for (int i = 0; i < monTypes.Length; i++) {
				if (kingdomName.IndexOf(monTypes[i], StringComparison.OrdinalIgnoreCase) >= 0) {
					return "MONARCHY";
				}
			}
			for (int i = 0; i < dicTypes.Length; i++) {
				if (kingdomName.IndexOf(dicTypes[i], StringComparison.OrdinalIgnoreCase) >= 0) {
					return "DICTATOR";
				}
			}
			for (int i = 0; i < repTypes.Length; i++) {
				if (kingdomName.IndexOf(repTypes[i], StringComparison.OrdinalIgnoreCase) >= 0) {
					return "REPUBLIC";
				}
			}
			if (kingdomName is null || kingdomName == "") {
				return "";
			} else {
				return "MONARCHY";
			}
		}

		public static string DefaultLeadersName(string kingdomType) {
			switch (kingdomType) {
				case "MONARCHY": return "King";
				case "DICTATOR": return "S.L.";
				case "REPUBLIC": return "P.M.";
				default: return "";
			}
		}

		public static string DefaultLeadersLong(string kingdomType) {
			switch (kingdomType) {
				case "MONARCHY": return "His Excellency";
				case "DICTATOR": return "Supreme Leader";
				case "REPUBLIC": return "Prime Minister";
				default: return "";
			}
		}

		public static string DefaultLeadersDesc(string kingdomType, string kingdomName) {
			switch (kingdomType) {
				case "MONARCHY": return "Head monarch and throning ruler of the " + LangUtility.Fix(kingdomName) + ".";
				case "DICTATOR": return "Lead authority and life-appointed ruler of the " + LangUtility.Fix(kingdomName) + ".";
				case "REPUBLIC": return "Elected represenative of the " + LangUtility.Fix(kingdomName) + ".";
				default: return "";
			}
		}
	}

	internal static class CultUtility {
		public static string CorrectCultureGUID(string cultureGUID) {
			if (cultureGUID == "00000000") {
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

		public static string CorrectCultureName(string cultureName) {
			string oldName = cultureName.Replace(" ", "_");
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

		public static string CorrectCultureLong(string cultureName) {
			string oldName = cultureName.Replace(" ", "_");
			if (oldName.EndsWith("an")) {
				return oldName + "ish";
			}
			if (oldName.EndsWith("ia")) {
				return oldName + "n";
			}
			if (oldName.EndsWith("ab")) {
				return oldName + "ic";
			}
			if (oldName.EndsWith("al")) {
				return oldName + "ese";
			}
			if (oldName.EndsWith("la")) {
				return oldName + "n";
			}
			if (oldName.EndsWith("e")) {
				return oldName.Remove(oldName.Length - 1) + "ian";
			}
			if (oldName.EndsWith("na")) {
				return oldName.Remove(oldName.Length - 1) + "ese";
			}
			if (oldName.EndsWith("y")) {
				return oldName.Remove(oldName.Length - 1) + "ian";
			}
			return oldName;
		}
	}
}