using System;
using System.Linq;
using Vintagestory.API.Config;

internal static class LangUtility {
	public static string Get(string langKeys, params object[] args) {
		return Lang.Get("vskingdom:" + langKeys, args);
	}

	public static string Set(string langKeys, string entryKey, params object[] args) {
		return Lang.Get("vskingdom:" + langKeys, args).Replace("[ENTRY]", entryKey);
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

	public static string Sets(string langKeys, string[] entryKeys, params object[] args) {
		string message = Lang.Get("vskingdom:" + langKeys, args);
		for (int i = 0; i < entryKeys.Length; i++) {
			string entryCode = "ENTRY" + (i + 1);
			message.Replace(entryCode, entryKeys[i]);
		}
		return message;
	}

	public static string[] Open(string stringList) {
		return stringList.Split(", ");
	}

	public static string[] Fuse(string[] array1, string[] array2) {
		return array1.Concat(array2).ToArray();
	}
}