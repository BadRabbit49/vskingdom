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
				itemCode = null,
				idleAnim = "Idle1", idleTime = 1f,
				walkAnim = "Walk", walkTime = 1.3f,
				moveAnim = "Sprint", moveTime = 0.6f,
				drawAnim = "BowDraw", drawTime = 1f,
				fireAnim = "BowFire", fireTime = 1f,
				loadAnim = "BowLoad", loadTime = 1f,
				bashAnim = "Hit", bashTime = 1f,
				stabAnim = null, stabTime = 1f,
				hit1Anim = null, hit1Time = 1f,
				hit2Anim = null, hit2Time = 1f
			},
			// Vintage Story
			new WeaponAnims() {
				itemCode = new AssetLocation("game:bow-crude"),
				idleAnim = "BowIdle", idleTime = 1f,
				walkAnim = "BowWalk", walkTime = 1.3f,
				moveAnim = "BowMove", moveTime = 0.6f,
				drawAnim = "BowDraw", drawTime = 1f,
				fireAnim = "BowFire", fireTime = 1f,
				loadAnim = "BowLoad", loadTime = 1f,
				bashAnim = null, bashTime = 1f,
				stabAnim = null, stabTime = 1f,
				hit1Anim = null, hit1Time = 1f,
				hit2Anim = null, hit2Time = 1f
			},
			new WeaponAnims() {
				itemCode = new AssetLocation("game:bow-simple"),
				idleAnim = "BowIdle", idleTime = 1f,
				walkAnim = "BowWalk", walkTime = 1.3f,
				moveAnim = "BowMove", moveTime = 0.6f,
				drawAnim = "BowDraw", drawTime = 1f,
				fireAnim = "BowFire", fireTime = 1f,
				loadAnim = "BowLoad", loadTime = 1f,
				bashAnim = null, bashTime = 1f,
				stabAnim = null, stabTime = 1f,
				hit1Anim = null, hit1Time = 1f,
				hit2Anim = null, hit2Time = 1f
			},
			new WeaponAnims() {
				itemCode = new AssetLocation("game:bow-long"),
				idleAnim = "BowIdle", idleTime = 1f,
				walkAnim = "BowWalk", walkTime = 1.3f,
				moveAnim = "BowMove", moveTime = 0.6f,
				drawAnim = "BowDraw", drawTime = 1f,
				fireAnim = "BowFire", fireTime = 1f,
				loadAnim = "BowLoad", loadTime = 1f,
				bashAnim = null, bashTime = 1f,
				stabAnim = null, stabTime = 1f,
				hit1Anim = null, hit1Time = 1f,
				hit2Anim = null, hit2Time = 1f
			},
			new WeaponAnims() {
				itemCode = new AssetLocation("game:bow-recurve"),
				idleAnim = "BowIdle", idleTime = 1f,
				walkAnim = "BowWalk", walkTime = 1.3f,
				moveAnim = "BowMove", moveTime = 0.6f,
				drawAnim = "BowDraw", drawTime = 1f,
				fireAnim = "BowFire", fireTime = 1f,
				loadAnim = "BowLoad", loadTime = 1f,
				bashAnim = null, bashTime = 1f,
				stabAnim = null, stabTime = 1f,
				hit1Anim = null, hit1Time = 1f,
				hit2Anim = null, hit2Time = 1f
			},
			// Maltiez Firearms
			new WeaponAnims() {
				itemCode = new AssetLocation("maltiezfirearms:pistol-plain"), idleTime = 1f,
				walkAnim = "GunWalkPistol", walkTime = 1.3f,
				moveAnim = "GunMovePistol", moveTime = 0.6f,
				drawAnim = "GunDrawPistol", drawTime = 1f,
				fireAnim = "GunFirePistol", fireTime = 1f,
				loadAnim = "Hit", loadTime = 1f,
				bashAnim = "Hit", bashTime = 1f,
				stabAnim = null, stabTime = 1f,
				hit1Anim = null, hit1Time = 1f,
				hit2Anim = null, hit2Time = 1f
			},
			new WeaponAnims() {
				itemCode = new AssetLocation("maltiezfirearms:arquebus-plain"),
				idleAnim = "GunIdleArquebus", idleTime = 1f,
				walkAnim = "GunWalkArquebus", walkTime = 1.3f,
				moveAnim = "GunMoveArquebus", moveTime = 0.6f,
				drawAnim = "GunDrawArquebus", drawTime = 1f,
				fireAnim = "GunFireArquebus", fireTime = 1f,
				loadAnim = "GunLoadArquebus", loadTime = 1f,
				bashAnim = "GunBashArquebus", bashTime = 1f,
				stabAnim = null, stabTime = 1f,
				hit1Anim = null, hit1Time = 1f,
				hit2Anim = null, hit2Time = 1f
			},
			new WeaponAnims() {
				itemCode = new AssetLocation("maltiezfirearms:musket"),
				idleAnim = "GunIdleMusket", idleTime = 1f,
				walkAnim = "GunWalkMusket", walkTime = 1.3f,
				moveAnim = "GunMoveMusket", moveTime = 0.6f,
				drawAnim = "GunDrawMusket", drawTime = 1f,
				fireAnim = "GunFireMusket", fireTime = 1f,
				loadAnim = "GunLoadMusket", loadTime = 1f,
				bashAnim = "GunBashMusket", bashTime = 1f,
				stabAnim = "GunStabMusket", stabTime = 1f,
				hit1Anim = null, hit1Time = 1f,
				hit2Anim = null, hit2Time = 1f
			},
			new WeaponAnims() {
				itemCode = new AssetLocation("maltiezfirearms:carbine"),
				idleAnim = "GunIdleArquebus", idleTime = 1f,
				walkAnim = "GunWalkArquebus", walkTime = 1.3f,
				moveAnim = "GunMoveArquebus", moveTime = 0.6f,
				drawAnim = "GunDrawArquebus", drawTime = 1f,
				fireAnim = "GunFireArquebus", fireTime = 1f,
				loadAnim = "GunLoadArquebus", loadTime = 1f,
				bashAnim = "GunBashArquebus", bashTime = 1f,
				stabAnim = null, stabTime = 1f,
				hit1Anim = null, hit1Time = 1f,
				hit2Anim = null, hit2Time = 1f
			}
		});
		
		public static Dictionary<AssetLocation, AssetLocation> wepnAimAudio = new Dictionary<AssetLocation, AssetLocation> {
			// Vintage Story
			{ new AssetLocation("game:bow-crude"), new AssetLocation("game:sounds/bow-draw") },
			{ new AssetLocation("game:bow-simple"), new AssetLocation("game:sounds/bow-draw") },
			{ new AssetLocation("game:bow-long"), new AssetLocation("game:sounds/bow-draw") },
			{ new AssetLocation("game:bow-recurve"), new AssetLocation("game:sounds/bow-draw") },
			{ new AssetLocation("game:sling"), new AssetLocation("game:sounds/bow-draw") },
			{ new AssetLocation("game:spear-chert"), null },
			{ new AssetLocation("game:spear-granite"), null },
			{ new AssetLocation("game:spear-andesite"), null },
			{ new AssetLocation("game:spear-peridotite"), null },
			{ new AssetLocation("game:spear-basalt"), null },
			{ new AssetLocation("game:spear-flint"), null },
			{ new AssetLocation("game:spear-obsidian"), null },
			{ new AssetLocation("game:spear-scrap"), null },
			{ new AssetLocation("game:spear-copper"), null },
			{ new AssetLocation("game:spear-bismuthbronze"), null },
			{ new AssetLocation("game:spear-tinbronze"), null },
			{ new AssetLocation("game:spear-blackbronze"), null },
			{ new AssetLocation("game:spear-ruined"), null },
			{ new AssetLocation("game:spear-hacking"), null },
			{ new AssetLocation("game:spear-ornategold"), null },
			{ new AssetLocation("game:spear-ornatesilver"), null },
			// Maltiez Firearms & Crossbows
			{ new AssetLocation("maltiezfirearms:pistol"), new AssetLocation("maltiezfirearms:sounds/pistol/flint-raise") },
			{ new AssetLocation("maltiezfirearms:arquebus"), new AssetLocation("maltiezfirearms:sounds/arquebus/powder-prime") },
			{ new AssetLocation("maltiezfirearms:musket"), new AssetLocation("maltiezfirearms:sounds/musket/musket-cock") },
			{ new AssetLocation("maltiezfirearms:carbine"), new AssetLocation("maltiezfirearms:sounds/musket/musket-cock") },
			{ new AssetLocation("maltiezcrossbows:crossbow-simple"), new AssetLocation("maltiezcrossbows:sounds/loading/wooden-click") },
			{ new AssetLocation("maltiezcrossbows:crossbow-stirrup"), new AssetLocation("maltiezcrossbows:sounds/loading/wooden-click") },
			{ new AssetLocation("maltiezcrossbows:crossbow-goatsfoot"), new AssetLocation("maltiezcrossbows:sounds/loading/metal-click") },
			{ new AssetLocation("maltiezcrossbows:crossbow-windlass"), new AssetLocation("maltiezcrossbows:sounds/loading/metal-click") }
		};

		// Audios to play while firing.
		public static Dictionary<AssetLocation, List<AssetLocation>> wepnHitAudio = new Dictionary<AssetLocation, List<AssetLocation>> {
			// Vintage Story
			{ new AssetLocation("game:bow-crude"), new List<AssetLocation> { new AssetLocation("game:sounds/bow-release") } },
			{ new AssetLocation("game:bow-simple"), new List<AssetLocation> { new AssetLocation("game:sounds/bow-release") } },
			{ new AssetLocation("game:bow-long"), new List<AssetLocation> { new AssetLocation("game:sounds/bow-release") } },
			{ new AssetLocation("game:bow-recurve"), new List<AssetLocation> { new AssetLocation("game:sounds/bow-release") } },
			{ new AssetLocation("game:sling"), new List<AssetLocation> { new AssetLocation("game:sounds/tool/sling1") } },
			{ new AssetLocation("game:spear"), new List<AssetLocation> { new AssetLocation("game:sounds/bow-release") } },
			// Maltiez Firearms & Crossbows
			{ new AssetLocation("maltiezfirearms:pistol"), new List<AssetLocation> { new AssetLocation("maltiezfirearms:sounds/pistol/pistol-fire-1"), new AssetLocation("maltiezfirearms:sounds/pistol/pistol-fire-2"), new AssetLocation("maltiezfirearms:sounds/pistol/pistol-fire-3"), new AssetLocation("maltiezfirearms:sounds/pistol/pistol-fire-4"), } },
			{ new AssetLocation("maltiezfirearms:arquebus"), new List<AssetLocation> { new AssetLocation("maltiezfirearms:sounds/arquebus/arquebus-fire-1"), new AssetLocation("maltiezfirearms:sounds/arquebus/arquebus-fire-2"), new AssetLocation("maltiezfirearms:sounds/arquebus/arquebus-fire-3") } },
			{ new AssetLocation("maltiezfirearms:musket"), new List<AssetLocation> { new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-1"), new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-2"), new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-3"), new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-4") } },
			{ new AssetLocation("maltiezfirearms:carbine"), new List<AssetLocation> { new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-1"), new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-2"), new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-3"), new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-4") } },
			{ new AssetLocation("maltiezcrossbows:crossbow-simple"), new List<AssetLocation> { new AssetLocation( "maltiezcrossbows:sounds/release/simple-0"), new AssetLocation("maltiezcrossbows:sounds/release/simple-1"), new AssetLocation("maltiezcrossbows:sounds/release/simple-2"), new AssetLocation("maltiezcrossbows:sounds/release/simple-3") } },
			{ new AssetLocation("maltiezcrossbows:crossbow-stirrup"), new List<AssetLocation> { new AssetLocation("maltiezcrossbows:sounds/release/stirrup-0"), new AssetLocation("maltiezcrossbows:sounds/release/stirrup-1"), new AssetLocation("maltiezcrossbows:sounds/release/stirrup-2"), new AssetLocation("maltiezcrossbows:sounds/release/stirrup-3"), new AssetLocation("maltiezcrossbows:sounds/release/stirrup-4"), new AssetLocation("maltiezcrossbows:sounds/release/stirrup-5"), new AssetLocation("maltiezcrossbows:sounds/release/stirrup-6") } },
			{ new AssetLocation("maltiezcrossbows:crossbow-goatsfoot"), new List<AssetLocation> { new AssetLocation("maltiezcrossbows:sounds/release/goatsfoot-0"), new AssetLocation("maltiezcrossbows:sounds/release/goatsfoot-1"), new AssetLocation("maltiezcrossbows:sounds/release/goatsfoot-2"), new AssetLocation("maltiezcrossbows:sounds/release/goatsfoot-3"), new AssetLocation("maltiezcrossbows:sounds/release/goatsfoot-4"), new AssetLocation("maltiezcrossbows:sounds/release/goatsfoot-5"), new AssetLocation("maltiezcrossbows:sounds/release/goatsfoot-6"), new AssetLocation("maltiezcrossbows:sounds/release/goatsfoot-7"), new AssetLocation("maltiezcrossbows:sounds/release/goatsfoot-8"), new AssetLocation("maltiezcrossbows:sounds/release/goatsfoot-9"), new AssetLocation("maltiezcrossbows:sounds/release/goatsfoot-10"), new AssetLocation("maltiezcrossbows:sounds/release/goatsfoot-11") } },
			{ new AssetLocation("maltiezcrossbows:crossbow-windlass"), new List<AssetLocation> { new AssetLocation("maltiezcrossbows:sounds/release/windlass-0"), new AssetLocation("maltiezcrossbows:sounds/release/windlass-1"), new AssetLocation("maltiezcrossbows:sounds/release/windlass-2"), new AssetLocation("maltiezcrossbows:sounds/release/windlass-3"), new AssetLocation("maltiezcrossbows:sounds/release/windlass-4"), new AssetLocation("maltiezcrossbows:sounds/release/windlass-5"), new AssetLocation("maltiezcrossbows:sounds/release/windlass-6"), new AssetLocation("maltiezcrossbows:sounds/release/windlass-7"), new AssetLocation("maltiezcrossbows:sounds/release/windlass-8"), new AssetLocation("maltiezcrossbows:sounds/release/windlass-9"), new AssetLocation("maltiezcrossbows:sounds/release/windlass-10"), new AssetLocation("maltiezcrossbows:sounds/release/windlass-11") } }
		};
	}
}