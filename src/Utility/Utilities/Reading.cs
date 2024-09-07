using System;
using System.Linq;
using Vintagestory.API.Config;

namespace VSKingdom.Utilities {
	internal static class ReadingUtil {
		public static string Get(string langKeys) {
			return Lang.Get("vskingdom:" + langKeys);
		}

		public static string GetL(string langCode, string langKeys) {
			return Lang.GetL(langCode, "vskingdom:" + langKeys);
		}

		public static string Set(string langKeys, string entryKey) {
			return Lang.Get("vskingdom:" + langKeys).Replace("[ENTRY]", entryKey);
		}

		public static string SetL(string langCode, string langKeys, string entryKey) {
			return Lang.GetL(langCode, "vskingdom:" + langKeys).Replace("[ENTRY]", entryKey);
		}

		public static string Set(string langKeys, string entryKey1, string entryKey2 = null, string entryKey3 = null, string entryKey4 = null) {
			return Lang.Get("vskingdom:" + langKeys)
				.Replace("[ENTRY]", entryKey1)
				.Replace("[ENTRY1]", entryKey1)
				.Replace("[ENTRY2]", entryKey2)
				.Replace("[ENTRY3]", entryKey3)
				.Replace("[ENTRY4]", entryKey4);
		}

		public static string SetL(string langCode, string langKeys, string entryKey1, string entryKey2 = null, string entryKey3 = null, string entryKey4 = null) {
			return Lang.GetL(langCode, "vskingdom:" + langKeys)
				.Replace("[ENTRY]", entryKey1)
				.Replace("[ENTRY1]", entryKey1)
				.Replace("[ENTRY2]", entryKey2)
				.Replace("[ENTRY3]", entryKey3)
				.Replace("[ENTRY4]", entryKey4);
		}

		public static string Set(string langKeys, string[] entryKeys) {
			string msg = Lang.Get("vskingdom:" + langKeys);
			for (int i = 1; i < entryKeys.Length; i++) {
				msg.Replace($"[ENTRY{i}]", entryKeys[i - 1]);
			}
			return msg;
		}

		public static string Ref(string langKeys, string langEntry1, string langEntry2 = null, string langEntry3 = null, string langEntry4 = null) {
			return Lang.Get("vskingdom:" + langKeys)
				.Replace("[ENTRY]", Lang.Get(langEntry1))
				.Replace("[ENTRY1]", Lang.Get(langEntry1))
				.Replace("[ENTRY2]", Lang.Get(langEntry2))
				.Replace("[ENTRY3]", Lang.Get(langEntry3))
				.Replace("[ENTRY4]", Lang.Get(langEntry4));
		}

		public static string RefL(string langCode, string langKeys, string langEntry1, string langEntry2 = null, string langEntry3 = null, string langEntry4 = null) {
			return Lang.GetL(langCode, "vskingdom:" + langKeys)
				.Replace("[ENTRY]", Lang.GetL(langCode, langEntry1))
				.Replace("[ENTRY1]", Lang.GetL(langCode, langEntry1))
				.Replace("[ENTRY2]", Lang.GetL(langCode, langEntry2))
				.Replace("[ENTRY3]", Lang.GetL(langCode, langEntry3))
				.Replace("[ENTRY4]", Lang.GetL(langCode, langEntry4));
		}

		public static string Low(string fakeName) {
			// "Longstead of Mireland" -> "Longstead_of_Mireland"
			return fakeName.Replace(" ", "_");
		}

		public static string Fix(string realName) {
			// "Longstead_of_Mireland" -> "Longstead of Mireland"
			return realName.Replace("_", " ");
		}

		public static string Msg(string[] stringList) {
			string message = string.Empty;
			foreach (string str in stringList) {
				message += ("\n" + str);
			}
			return message;
		}

		public static string Mps(string sentence) {
			sentence = sentence.Replace("_", " ").Substring(0, 1).ToUpperInvariant() + sentence.Substring(1);
			if (!new char[] { '.', '!', '?', '。', '！', '？' }.Contains(sentence[sentence.Length - 1])) {
				if(new string[]{ "zh-cn", "zh-tw", "ja", "ko" }.Contains(Lang.CurrentLocale)) {
					sentence += '。';
				} else if (Lang.CurrentLocale != "ar") {
					sentence += '.';
				}
			}
			return sentence;
		}

		public static string[] Fuse(string[] array1, string[] array2) {
			return array1.Concat(array2).ToArray();
		}

		public static string[] Open(string stringList) {
			return stringList.Split(", ");
		}

		public static string Open(Int16[] array) {
			string stringList = string.Empty;
			for (int i = 0; i < array.Length; i++) {
				stringList += $"{array[i]}, ";
			}
			return stringList.Remove(stringList.Length - 2, 2);
		}

		public static string Open(Int32[] array) {
			string stringList = string.Empty;
			for (int i = 0; i < array.Length; i++) {
				stringList += $"{array[i]}, ";
			}
			return stringList.Remove(stringList.Length - 2, 2);
		}

		public static string Open(Int64[] array) {
			string stringList = string.Empty;
			for (int i = 0; i < array.Length; i++) {
				stringList += $"{array[i]}, ";
			}
			return stringList.Remove(stringList.Length - 2, 2);
		}

		public static string Open(Int128[] array) {
			string stringList = string.Empty;
			for (int i = 0; i < array.Length; i++) {
				stringList += $"{array[i]}, ";
			}
			return stringList.Remove(stringList.Length - 2, 2);
		}
	}
}