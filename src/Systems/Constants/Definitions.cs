using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VSKingdom {
	public static class FixedLiterature {
		public static Dictionary<string, string> CulturalAdditionSuffixes = new Dictionary<string, string> {
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

		public static Dictionary<string, string> CulturalReplacedSuffixes = new Dictionary<string, string> {
			{ "e", "ian" },
			{ "na", "ese" },
			{ "y", "ian" },
			{ "ish", "ish" }
		};
	}
	public static class ItemsProperties {
		public static List<WeaponAnims> WeaponAnimations { get; set; } = new List<WeaponAnims>(new WeaponAnims[] {
			// Defaults
			new WeaponAnims() {
				itemCode = "",
				idleAnim = "idle",
				walkAnim = "walk",
				moveAnim = "move",
				duckAnim = "duck",
				swimAnim = "swim",
				jumpAnim = "jump",
				diesAnim = "dies",
				drawAnim = "draw",
				fireAnim = "fire",
				loadAnim = "load",
				bashAnim = "bash",
				stabAnim = "bash"
			},
			new WeaponAnims() {
				itemCode = "bow",
				idleAnim = "bowidle",
				walkAnim = "bowwalk",
				moveAnim = "bowmove",
				duckAnim = "duck",
				swimAnim = "swim",
				diesAnim = "dies",
				jumpAnim = "jump",
				drawAnim = "bowdraw",
				fireAnim = "bowfire",
				loadAnim = "bowload",
				bashAnim = "bash",
				stabAnim = "bash"
			},
			new WeaponAnims() {
				itemCode = "sling",
				idleAnim = "idle",
				walkAnim = "walk",
				moveAnim = "move",
				duckAnim = "duck",
				swimAnim = "swim",
				jumpAnim = "jump",
				diesAnim = "dies",
				drawAnim = "slingdraw2",
				fireAnim = "slingfire2",
				loadAnim = "load",
				bashAnim = "bash",
				stabAnim = "bash"
			},
			// Maltiez Firearms
			new WeaponAnims() {
				itemCode = "pistol",
				idleAnim = "pistolidle",
				walkAnim = "pistolwalk",
				moveAnim = "pistolmove",
				duckAnim = "duck",
				swimAnim = "swim",
				jumpAnim = "jump",
				diesAnim = "dies",
				drawAnim = "pistoldraw",
				fireAnim = "pistolfire",
				loadAnim = "pistolload",
				bashAnim = "pistolbash",
				stabAnim = "pistolbash"
			},
			new WeaponAnims() {
				itemCode = "arquebus",
				idleAnim = "arquebusidle",
				walkAnim = "arquebuswalk",
				moveAnim = "arquebusmove",
				duckAnim = "duck",
				swimAnim = "swim",
				jumpAnim = "jump",
				diesAnim = "dies",
				drawAnim = "arquebusdraw",
				fireAnim = "arquebusfire",
				loadAnim = "arquebusload",
				bashAnim = "arquebusbash",
				stabAnim = "arquebusbash"
			},
			new WeaponAnims() {
				itemCode = "musket",
				idleAnim = "musketidle",
				walkAnim = "musketwalk",
				moveAnim = "musketmove",
				duckAnim = "duck",
				swimAnim = "swim",
				jumpAnim = "jump",
				diesAnim = "dies",
				drawAnim = "musketdraw",
				fireAnim = "musketfire",
				loadAnim = "musketload",
				bashAnim = "musketbash",
				stabAnim = "musketstab"
			},
			new WeaponAnims() {
				itemCode = "carbine",
				idleAnim = "arquebusidle",
				walkAnim = "arquebuswalk",
				moveAnim = "arquebusmove",
				duckAnim = "duck",
				swimAnim = "swim",
				jumpAnim = "jump",
				diesAnim = "dies",
				drawAnim = "arquebusdraw",
				fireAnim = "arquebusfire",
				loadAnim = "arquebusload",
				bashAnim = "arquebusbash",
				stabAnim = "arquebusbash"
			}
		});
		
		public static Dictionary<string, AssetLocation> WeaponDrawAudios = new Dictionary<string, AssetLocation> {
			// Vintage Story
			{ "game:bow", new AssetLocation("game:sounds/bow-draw") },
			{ "game:sling", new AssetLocation("game:sounds/bow-draw") },
			// Maltiez Firearms & Crossbows
			{ "maltiezfirearms:pistol", new AssetLocation("maltiezfirearms:sounds/pistol/flint-raise") },
			{ "maltiezfirearms:arquebus", new AssetLocation("maltiezfirearms:sounds/arquebus/powder-prime") },
			{ "maltiezfirearms:musket", new AssetLocation("maltiezfirearms:sounds/musket/musket-cock") },
			{ "maltiezfirearms:carbine", new AssetLocation("maltiezfirearms:sounds/musket/musket-cock") },
			{ "maltiezcrossbows:crossbow", new AssetLocation("maltiezcrossbows:sounds/loading/wooden-click") }
		};

		// Audios to play while firing.
		public static Dictionary<string, List<AssetLocation>> WeaponFireAudios = new Dictionary<string, List<AssetLocation>> {
			// Vintage Story
			{ "game:bow", new List<AssetLocation> { new AssetLocation("game:sounds/bow-release") } },
			{ "game:sling", new List<AssetLocation> { new AssetLocation("game:sounds/tool/sling1") } },
			{ "game:spear", new List<AssetLocation> { new AssetLocation("game:sounds/bow-release") } },
			// Maltiez Firearms & Crossbows
			{ "maltiezfirearms:pistol", new List<AssetLocation> { new AssetLocation("maltiezfirearms:sounds/pistol/pistol-fire-1"), new AssetLocation("maltiezfirearms:sounds/pistol/pistol-fire-2"), new AssetLocation("maltiezfirearms:sounds/pistol/pistol-fire-3"), new AssetLocation("maltiezfirearms:sounds/pistol/pistol-fire-4"), } },
			{ "maltiezfirearms:arquebus", new List<AssetLocation> { new AssetLocation("maltiezfirearms:sounds/arquebus/arquebus-fire-1"), new AssetLocation("maltiezfirearms:sounds/arquebus/arquebus-fire-2"), new AssetLocation("maltiezfirearms:sounds/arquebus/arquebus-fire-3") } },
			{ "maltiezfirearms:musket", new List<AssetLocation> { new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-1"), new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-2"), new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-3"), new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-4") } },
			{ "maltiezfirearms:carbine", new List<AssetLocation> { new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-1"), new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-2"), new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-3"), new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-4") } },
			{ "maltiezcrossbows:crossbow", new List<AssetLocation> { new AssetLocation( "maltiezcrossbows:sounds/release/simple-0"), new AssetLocation("maltiezcrossbows:sounds/release/simple-1"), new AssetLocation("maltiezcrossbows:sounds/release/simple-2"), new AssetLocation("maltiezcrossbows:sounds/release/simple-3") } }
		};
	}
}