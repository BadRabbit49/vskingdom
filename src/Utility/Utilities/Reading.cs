using System;
using System.Linq;
using Vintagestory.API.Config;

namespace VSKingdom.Utilities {
	internal static class ReadingUtil {
		public static string Get(string langKeys) {
			return Lang.Get("vskingdom:" + langKeys);
		}

		public static string GetL(string langCode, string langKeys, params object[] entryKey) {
			return Lang.GetL(langCode, "vskingdom:" + langKeys, entryKey);
		}

		public static string GetL(string langCode, string langKeys, string langKey2, bool translate = true) {
			return Lang.GetL(langCode, "vskingdom:" + langKeys, translate ? Lang.GetL(langCode, "vskingdom:" + langKey2) : langKey2);
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