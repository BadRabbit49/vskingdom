using Vintagestory.API.Config;

internal static class LangUtility {
	public static string Get(string langKeys, params object[] args) {
		return Lang.Get("vskingdom:" + langKeys, args);
	}

	public static string Low(string fakeName) {
		// "Longstead of Mireland" -> "Longstead_of_Mireland"
		return fakeName.Replace(" ", "_");
	}

	public static string Fix(string realName) {
		// "Longstead_of_Mireland" -> "Longstead of Mireland"
		return realName.Replace("_", " ");
	}
}