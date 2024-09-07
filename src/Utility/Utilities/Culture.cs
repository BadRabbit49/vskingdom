using System;
using System.Collections.Generic;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using VSKingdom.Constants;

namespace VSKingdom.Utilities {
	internal static class CultureUtil {
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
			foreach (var addCombo in GlobalDicts.suffixAddition) {
				if (oldName.EndsWith(addCombo.Key)) {
					return oldName + addCombo.Value;
				}
			}
			foreach (var repCombo in GlobalDicts.suffixReplaced) {
				if (oldName.EndsWith(repCombo.Key)) {
					return oldName.Remove((oldName.Length - 1) - (repCombo.Key.Length - 1)) + repCombo.Value;
				}
			}
			return oldName;
		}

		public static string ListedCultures(ICoreServerAPI sapi, string cultureGUID) {
			byte[] cultureData = sapi.WorldManager.SaveGame.GetData("cultureData");
			List<Culture> cultureList = cultureData is null ? new List<Culture>() : SerializerUtil.Deserialize<List<Culture>>(cultureData);
			Culture culture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == cultureGUID);
			string basicInfo = $"FULL CULTURE INFO\nGuid: {culture.CultureGUID}\nName: {culture.CultureNAME}\nLong: {culture.CultureLONG}\nDesc: {culture.CultureDESC}";
			string intricate = $"\nFounder Name: {sapi.World.PlayerByUid(culture.FounderGUID)?.PlayerName ?? "unknown"}\nDate Founded: {culture.FoundedDATE} ({culture.FoundedMETA})";
			return basicInfo + intricate;
		}
	}
}