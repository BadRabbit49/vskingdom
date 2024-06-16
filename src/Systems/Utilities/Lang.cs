using System.Linq;
using Vintagestory.API.Config;

internal static class LangUtility {
	public static string Get(string langKeys, params object[] args) {
		return Lang.Get("vskingdom:" + langKeys, args);
	}

	public static string Set(string langKeys, string entryKeys, params object[] args) {
		return Lang.Get("vskingdom:" + langKeys, args).Replace("[ENTRY]", entryKeys);
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

	public static string[] Open(string stringList) {
		return stringList.Split(", ");
	}

	public static string[] Fuse(string[] array1, string[] array2) {
		return array1.Concat(array2).ToArray();
	}
}