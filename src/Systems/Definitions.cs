using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VSKingdom {
	public static class RegisteredItems {
		public static List<AssetLocation> AcceptedRange = new List<AssetLocation>() {
			// Vintage Story
			new AssetLocation("game:bow-crude"),
			new AssetLocation("game:bow-simple"),
			new AssetLocation("game:bow-long"),
			new AssetLocation("game:bow-recurve"),
			new AssetLocation("game:sling"),
			// Maltiez Firearms & Crossbows
			new AssetLocation("maltiezfirearms:pistol-plain"),
			new AssetLocation("maltiezfirearms:pistol-rusted"),
			new AssetLocation("maltiezfirearms:arquebus-plain"),
			new AssetLocation("maltiezfirearms:arquebus-rusted"),
			new AssetLocation("maltiezfirearms:musket"),
			new AssetLocation("maltiezfirearms:carbine"),
			new AssetLocation("maltiezcrossbows:crossbow-simple"),
			new AssetLocation("maltiezcrossbows:crossbow-stirrup"),
			new AssetLocation("maltiezcrossbows:crossbow-goatsfoot"),
			new AssetLocation("maltiezcrossbows:crossbow-windlass")
		};

		public static List<AssetLocation> AcceptedAmmos = new List<AssetLocation>() {
			// Vintage Story
			new AssetLocation("game:arrow-crude"),
			new AssetLocation("game:arrow-flint"),
			new AssetLocation("game:arrow-copper"),
			new AssetLocation("game:arrow-tinbronze"),
			new AssetLocation("game:arrow-gold"),
			new AssetLocation("game:arrow-silver"),
			new AssetLocation("game:arrow-bismuthbronze"),
			new AssetLocation("game:arrow-blackbronze"),
			new AssetLocation("game:arrow-iron"),
			new AssetLocation("game:arrow-meteoriciron"),
			new AssetLocation("game:arrow-steel"),
			new AssetLocation("game:spear-chert"),
			new AssetLocation("game:spear-granite"),
			new AssetLocation("game:spear-andesite"),
			new AssetLocation("game:spear-peridotite"),
			new AssetLocation("game:spear-basalt"),
			new AssetLocation("game:spear-flint"),
			new AssetLocation("game:spear-obsidian"),
			new AssetLocation("game:spear-scrap"),
			new AssetLocation("game:spear-copper"),
			new AssetLocation("game:spear-bismuthbronze"),
			new AssetLocation("game:spear-tinbronze"),
			new AssetLocation("game:spear-blackbronze"),
			new AssetLocation("game:spear-ruined"),
			new AssetLocation("game:spear-hacking"),
			new AssetLocation("game:spear-ornategold"),
			new AssetLocation("game:spear-ornatesilver"),
			new AssetLocation("game:bullets-lead"),
			new AssetLocation("game:stone"),
			new AssetLocation("game:beenade"),
			// Maltiez Firearms & Crossbows
			new AssetLocation("maltiezfirearms:bullet-lead"),
			new AssetLocation("maltiezfirearms:bullet-copper"),
			new AssetLocation("maltiezfirearms:bullet-steel"),
			new AssetLocation("maltiezfirearms:slug-lead"),
			new AssetLocation("maltiezfirearms:slug-copper"),
			new AssetLocation("maltiezfirearms:slug-steel"),
			new AssetLocation("maltiezcrossbows:bolt-crude"),
			new AssetLocation("maltiezcrossbows:bolt-copper"),
			new AssetLocation("maltiezcrossbows:bolt-tinbronze"),
			new AssetLocation("maltiezcrossbows:bolt-bismuthbronze"),
			new AssetLocation("maltiezcrossbows:bolt-blackbronze"),
			new AssetLocation("maltiezcrossbows:bolt-iron"),
			new AssetLocation("maltiezcrossbows:bolt-meteoriciron"),
			new AssetLocation("maltiezcrossbows:bolt-steel"),
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

	public static class ItemsStatistics {
		public static List<WeaponStats> WeaponStatistics { get; set; } = new List<WeaponStats>(new WeaponStats[] {
			// Defaults
			new WeaponStats() {
				itemCode = null,
				rangeDmg = 0f, meleeDmg = 0f, minRange = 0f, maxRange = 0f,
				canMelee = false, canThrow = false, skirmish = false, useSmoke = false
			},
			// Vintage Story
			new WeaponStats() {
				itemCode = new AssetLocation("game:bow-crude"),
				rangeDmg = 3f, meleeDmg = 1f, velocity = 1f, minRange = 7f, maxRange = 13f,
				canMelee = false, canThrow = false, skirmish = false, useSmoke = false
			},
			new WeaponStats() {
				itemCode = new AssetLocation("game:bow-simple"),
				rangeDmg = 3.25f, meleeDmg = 1f, velocity = 1f, minRange = 7f, maxRange = 15f,
				canMelee = false, canThrow = false, skirmish = false, useSmoke = false
			},
			new WeaponStats() {
				itemCode = new AssetLocation("game:bow-long"),
				rangeDmg = 3.75f, meleeDmg = 1f, velocity = 1f, minRange = 10f, maxRange = 18f,
				canMelee = false, canThrow = false, skirmish = false, useSmoke = false
			},
			new WeaponStats() {
				itemCode = new AssetLocation("game:bow-recurve"),
				rangeDmg = 4f, meleeDmg = 1f, velocity = 1f, minRange = 8f, maxRange = 17f,
				canMelee = false, canThrow = false, skirmish = false, useSmoke = false
			},
			new WeaponStats() {
				itemCode = new AssetLocation("game:sling"),
				rangeDmg = 2.5f, meleeDmg = 1f, velocity = 1f, minRange = 6f, maxRange = 10f,
				canMelee = false, canThrow = false, skirmish = true, useSmoke = false
			},
			// Maltiez Firearms
			new WeaponStats() {
				itemCode = new AssetLocation("maltiezfirearms:pistol-plain"),
				rangeDmg = 0f, meleeDmg = 2f, velocity = 3f, minRange = 7f, maxRange = 10f,
				canMelee = false, canThrow = false, skirmish = false, useSmoke = true
			},
			new WeaponStats() {
				itemCode = new AssetLocation("maltiezfirearms:pistol-tarnished"),
				rangeDmg = 0f, meleeDmg = 2f, velocity = 3f, minRange = 7f, maxRange = 10f,
				canMelee = false, canThrow = false, skirmish = false, useSmoke = true
			},
			new WeaponStats() {
				itemCode = new AssetLocation("maltiezfirearms:arquebus-plain"),
				rangeDmg = 0f, meleeDmg = 2f, velocity = 4f, minRange = 7f, maxRange = 15f,
				canMelee = false, canThrow = false, skirmish = false, useSmoke = true
			},
			new WeaponStats() {
				itemCode = new AssetLocation("maltiezfirearms:arquebus-rusted"),
				rangeDmg = 0f, meleeDmg = 2f, velocity = 4f, minRange = 7f, maxRange = 15f,
				canMelee = false, canThrow = false, skirmish = false, useSmoke = true
			},
			new WeaponStats() {
				itemCode = new AssetLocation("maltiezfirearms:musket"),
				rangeDmg = 0f, meleeDmg = 2f, velocity = 4f, minRange = 8f, maxRange = 17f,
				canMelee = true, canThrow = false, skirmish = false, useSmoke = true
			},
			new WeaponStats() {
				itemCode = new AssetLocation("maltiezfirearms:carbine"),
				rangeDmg = 0f, meleeDmg = 2f, velocity = 3f, minRange = 6f, maxRange = 15f,
				canMelee = true, canThrow = false, skirmish = false, useSmoke = true
			}
		});

		public static List<ArrowsStats> ProjectilesStats { get; set; } = new List<ArrowsStats>(new ArrowsStats[] {
			// Defaults
			new ArrowsStats() {
				itemCode = null,
				basicDmg = 0f, piercing = 0f, knocking = 0f
			},
			// Vintage Story
			new ArrowsStats() {
				itemCode = new AssetLocation("game:arrow-crude"),
				basicDmg = -0.75f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:arrow-flint"),
				basicDmg = -0.50f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:arrow-copper"),
				basicDmg = 0f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:arrow-tinbronze"),
				basicDmg = 1f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:arrow-gold"),
				basicDmg = 1f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:arrow-silver"),
				basicDmg = 1f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:arrow-bismuthbronze"),
				basicDmg = 1f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:arrow-blackbronze"),
				basicDmg = 1.5f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:arrow-iron"),
				basicDmg = 2f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:arrow-meteoriciron"),
				basicDmg = 2.25f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:arrow-steel"),
				basicDmg = 2.5f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:spear-chert"),
				basicDmg = 4f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:spear-granite"),
				basicDmg = 4f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:spear-andesite"),
				basicDmg = 4f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:spear-peridotite"),
				basicDmg = 4f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:spear-basalt"),
				basicDmg = 4f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:spear-flint"),
				basicDmg = 5f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:spear-obsidian"),
				basicDmg = 5.25f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:spear-scrap"),
				basicDmg = 5.75f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:spear-copper"),
				basicDmg = 5.75f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:spear-bismuthbronze"),
				basicDmg = 6.5f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:spear-tinbronze"),
				basicDmg = 7.5f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:spear-blackbronze"),
				basicDmg = 8f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:spear-ruined"),
				basicDmg = 8f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:spear-hacking"),
				basicDmg = 7f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:spear-ornategold"),
				basicDmg = 8.25f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("game:spear-ornatesilver"),
				basicDmg = 8.25f, piercing = 0f, knocking = 0f
			},
			// Maltiez Firearms & Crossbows
			new ArrowsStats() {
				itemCode = new AssetLocation("maltiezfirearms:bullet-lead"),
				basicDmg = 16f, piercing = 0f, knocking = 0.3f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("maltiezfirearms:bullet-copper"),
				basicDmg = 8f, piercing = 4f, knocking = 0.1f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("maltiezfirearms:bullet-steel"),
				basicDmg = 10f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("maltiezfirearms:slug-lead"),
				basicDmg = 38f, piercing = 0f, knocking = 0.5f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("maltiezfirearms:slug-copper"),
				basicDmg = 20f, piercing = 10f, knocking = 0.2f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("maltiezfirearms:slug-steel"),
				basicDmg = 0f, piercing = 25f, knocking = 0.1f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("maltiezcrossbows:bolt-crude"),
				basicDmg = 5f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("maltiezcrossbows:bolt-copper"),
				basicDmg = 6f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("maltiezcrossbows:bolt-tinbronze"),
				basicDmg = 7f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("maltiezcrossbows:bolt-bismuthbronze"),
				basicDmg = 7f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("maltiezcrossbows:bolt-blackbronze"),
				basicDmg = 7f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("maltiezcrossbows:bolt-iron"),
				basicDmg = 7f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("maltiezcrossbows:bolt-meteoriciron"),
				basicDmg = 7f, piercing = 0f, knocking = 0f
			},
			new ArrowsStats() {
				itemCode = new AssetLocation("maltiezcrossbows:bolt-steel"),
				basicDmg = 9f, piercing = 0f, knocking = 0f
			}
		});
	}
}