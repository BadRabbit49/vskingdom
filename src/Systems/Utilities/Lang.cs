using System;
using System.Linq;
using Vintagestory.API.Config;

internal static class LangUtility {
	public static string Get(string langKeys) {
		return Lang.Get("vskingdom:" + langKeys);
	}

	public static string Set(string langKeys, string entryKey) {
		return Lang.Get("vskingdom:" + langKeys).Replace("[ENTRY]", entryKey);
	}

	public static string Set(string langKeys, string entryKey1, string entryKey2 = null, string entryKey3 = null, string entryKey4 = null) {
		return Lang.Get("vskingdom:" + langKeys).Replace("[ENTRY1]", entryKey1).Replace("[ENTRY2]", entryKey2).Replace("[ENTRY3]", entryKey3).Replace("[ENTRY4]", entryKey4);
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

	public static string[] Open(string stringList) {
		return stringList.Split(", ");
	}

	public static string[] Fuse(string[] array1, string[] array2) {
		return array1.Concat(array2).ToArray();
	}
}