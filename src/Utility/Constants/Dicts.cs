using System.Collections.Generic;

namespace VSKingdom.Constants {
	public static class GlobalDicts {
		public static Dictionary<string, string> suffixAddition = new Dictionary<string, string> {
			{ "an", "ish" },
			{ "ia", "n" },
			{ "ea", "n" },
			{ "ab", "ic" },
			{ "al", "ese" },
			{ "la", "n" },
			{ "ho", "n" },
			{ "wa", "n" },
			{ "am", "ian" }
		};

		public static Dictionary<string, string> suffixReplaced = new Dictionary<string, string> {
			{ "e", "ian" },
			{ "na", "ese" },
			{ "y", "ian" },
			{ "ish", "ish" }
		};

		public static Dictionary<string, int> metalTiers = new Dictionary<string, int> {
			{ "lead", 2 },
			{ "copper", 2 },
			{ "oxidizedcopper", 2 },
			{ "tinbronze", 3 },
			{ "bismuthbronze",  3 },
			{ "blackbronze",  3 },
			{ "brass", 4 },
			{ "silver", 4 },
			{ "gold", 4 },
			{ "iron", 5 },
			{ "rust", 5 },
			{ "meteoriciron", 6 },
			{ "steel", 7 },
			{ "stainlesssteel", 8 },
			{ "titanium", 9 },
			{ "electrum", 9 }
		};

		public static Dictionary<string, string[]> fuelsCodes { get; set; } = new Dictionary<string, string[]>() {
			{ "wood", new string[] { "bark", "wood" } },
			{ "firewood", new string[] { "bark", "wood" } },
			{ "agedfirewood", new string[] { "bark", "wood" } },
			{ "coal", new string[] { "ore", "coal" } },
			{ "coke", new string[] { "ore", "coal" } },
			{ "charcoal", new string[] { "ore", "coal" } },
			{ "ore-lignite", new string[] { "ore", "coal" } },
			{ "ore-bituminouscoal", new string[] { "ore", "coal" } },
			{ "ore-anthracite", new string[] { "ore", "coal" } },
			{ "gear", new string[] { "rusty-iron", "gear" } },
			{ "gear-rusty", new string[] { "rusty-iron", "gear" } },
			{ "gear-temporal", new string[] { "temporal", "gear" } }
		};
	}
}