﻿using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VSKingdom.Constants {
	internal static class GlobalProps {
		public static Dictionary<string, float> WeaponMultipliers = new Dictionary<string, float> {
			{ "", 1f },
			{ "maltiezfirearms:pistol-plain", 1f },
			{ "maltiezfirearms:pistol-tarnished", 1f },
			{ "maltiezfirearms:arquebus-plain", 1.375f },
			{ "maltiezfirearms:arquebus-rusted", 1.375f },
			{ "maltiezfirearms:musket", 1.63f },
			{ "maltiezfirearms:carbine", 1f },
			{ "maltiezcrossbows:crossbow-simple-plain", 1f },
			{ "maltiezcrossbows:crossbow-stirrup-plain", 2f },
			{ "maltiezcrossbows:crossbow-latch-plain", 1.5f },
			{ "maltiezcrossbows:crossbow-goatsfoot-plain", 2.5f },
			{ "maltiezcrossbows:crossbow-windlass-plain", 4.3f },
			{ "maltiezcrossbows:crossbow-repeating-plain", 4.3f },
		};

		public static Dictionary<string, float> AmmunitionDamages = new Dictionary<string, float> {
			{ "", 0f },
			{ "maltiezfirearms:bullet-lead", 16f },
			{ "maltiezfirearms:bullet-copper", 8f },
			{ "maltiezfirearms:bullet-steel", 10f },
			{ "maltiezfirearms:slug-lead", 38f },
			{ "maltiezfirearms:slug-copper", 20f },
			{ "maltiezfirearms:slug-steel", 40f },
			{ "maltiezcrossbows:bolt-crude", 5f },
			{ "maltiezcrossbows:bolt-copper", 6f },
			{ "maltiezcrossbows:bolt-tinbronze",  7f },
			{ "maltiezcrossbows:bolt-bismuthbronze", 7f },
			{ "maltiezcrossbows:bolt-blackbronze", 7f },
			{ "maltiezcrossbows:bolt-iron",  7f },
			{ "maltiezcrossbows:bolt-meteoriciron", 7f },
			{ "maltiezcrossbows:bolt-steel", 9f }
		};

		public static Dictionary<string,WeaponProps> WeaponProperties = new Dictionary<string, WeaponProps> {
			// Vintage Story //
			{
				"", new WeaponProps {
					ammoCodes = null,
					idleAnims = "idle",
					walkAnims = "walk",
					moveAnims = "move",
					duckAnims = "duck",
					swimAnims = "swim",
					jumpAnims = "jump",
					drawAnims = "draw",
					fireAnims = "fire",
					loadAnims = "load",
					bashAnims = "bash",
					stabAnims = "bash",
					drawAudio = new AssetLocation[] { new AssetLocation("game:sounds/bow-draw") },
					fireAudio = new AssetLocation[] { new AssetLocation("game:sounds/bow-release") },
				}
			},
			{
				"game:bow", new WeaponProps {
					ammoCodes = "game:arrow",
					idleAnims = "bowidle",
					walkAnims = "bowwalk",
					moveAnims = "bowmove",
					duckAnims = "duck",
					swimAnims = "swim",
					jumpAnims = "jump",
					drawAnims = "bowdraw",
					fireAnims = "bowfire",
					loadAnims = "bowload",
					bashAnims = "bash",
					stabAnims = "bash",
					drawAudio = new AssetLocation[] { new AssetLocation("game:sounds/bow-draw") },
					fireAudio = new AssetLocation[] { new AssetLocation("game:sounds/bow-release") },
				}
			},
			{
				"game:sling", new WeaponProps {
					ammoCodes = "game:thrownstone",
					idleAnims = "idle",
					walkAnims = "walk",
					moveAnims = "move",
					duckAnims = "duck",
					swimAnims = "swim",
					jumpAnims = "jump",
					drawAnims = "slingdraw2",
					fireAnims = "slingfire2",
					loadAnims = "load",
					bashAnims = "bash",
					stabAnims = "bash",
					drawAudio = new AssetLocation[] { new AssetLocation("game:sounds/bow-draw") },
					fireAudio = new AssetLocation[] { new AssetLocation("game:sounds/tool/sling1") },
				}
			},
			{
				"game:spear", new WeaponProps {
					ammoCodes = "game:spear",
					idleAnims = "idle",
					walkAnims = "walk",
					moveAnims = "move",
					duckAnims = "duck",
					swimAnims = "swim",
					jumpAnims = "jump",
					drawAnims = "slingdraw2",
					fireAnims = "slingfire2",
					loadAnims = "load",
					bashAnims = "bash",
					stabAnims = "bash",
					drawAudio = new AssetLocation[] { new AssetLocation("game:sounds/bow-release") },
					fireAudio = new AssetLocation[] { new AssetLocation("game:sounds/bow-release") },
				}
			},
			// Maltiez Firearms //
			{
				"maltiezfirearms:pistol", new WeaponProps {
					ammoCodes = "maltiezfirearms:bullet",
					idleAnims = "pistolidle",
					walkAnims = "pistolwalk",
					moveAnims = "pistolmove",
					duckAnims = "duck",
					swimAnims = "swim",
					jumpAnims = "jump",
					drawAnims = "pistoldraw",
					fireAnims = "pistolfire",
					loadAnims = "pistolload",
					bashAnims = "pistolbash",
					stabAnims = "pistolbash",
					drawAudio = new AssetLocation[] { new AssetLocation("maltiezfirearms:sounds/pistol/flint-raise") },
					fireAudio = new AssetLocation[] { new AssetLocation("maltiezfirearms:sounds/pistol/pistol-fire-1"), new AssetLocation("maltiezfirearms:sounds/pistol/pistol-fire-2"), new AssetLocation("maltiezfirearms:sounds/pistol/pistol-fire-3"), new AssetLocation("maltiezfirearms:sounds/pistol/pistol-fire-4") },
				}
			},
			{
				"maltiezfirearms:arquebus", new WeaponProps {
					ammoSpeed = 4.0,
					ammoCodes = "maltiezfirearms:bullet",
					idleAnims = "arquebusidle",
					walkAnims = "arquebuswalk",
					moveAnims = "arquebusmove",
					duckAnims = "duck",
					swimAnims = "swim",
					jumpAnims = "jump",
					drawAnims = "arquebusdraw",
					fireAnims = "arquebusfire",
					loadAnims = "arquebusload",
					bashAnims = "arquebusbash",
					stabAnims = "arquebusbash",
					drawAudio = new AssetLocation[] { new AssetLocation("maltiezfirearms:sounds/arquebus/powder-prime") },
					fireAudio = new AssetLocation[] { new AssetLocation("maltiezfirearms:sounds/arquebus/arquebus-fire-1"), new AssetLocation("maltiezfirearms:sounds/arquebus/arquebus-fire-2"), new AssetLocation("maltiezfirearms:sounds/arquebus/arquebus-fire-3") },
				}
			},
			{
				"maltiezfirearms:musket", new WeaponProps {
					ammoSpeed = 4.0,
					ammoCodes = "maltiezfirearms:slug",
					idleAnims = "musketidle",
					walkAnims = "musketwalk",
					moveAnims = "musketmove",
					duckAnims = "duck",
					swimAnims = "swim",
					jumpAnims = "jump",
					drawAnims = "musketdraw",
					fireAnims = "musketfire",
					loadAnims = "musketload",
					bashAnims = "musketbash",
					stabAnims = "musketstab",
					drawAudio = new AssetLocation[] { new AssetLocation("maltiezfirearms:sounds/musket/musket-cock") },
					fireAudio = new AssetLocation[] { new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-1"), new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-2"), new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-3"), new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-4") },
				}
			},
			{
				"maltiezfirearms:carbine", new WeaponProps {
					ammoCodes = "maltiezfirearms:slug",
					idleAnims = "arquebusidle",
					walkAnims = "arquebuswalk",
					moveAnims = "arquebusmove",
					duckAnims = "duck",
					swimAnims = "swim",
					jumpAnims = "jump",
					drawAnims = "arquebusdraw",
					fireAnims = "arquebusfire",
					loadAnims = "arquebusload",
					bashAnims = "arquebusbash",
					stabAnims = "arquebusbash",
					drawAudio = new AssetLocation[] { new AssetLocation("maltiezfirearms:sounds/musket/musket-cock") },
					fireAudio = new AssetLocation[] { new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-1"), new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-2"), new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-3"), new AssetLocation("maltiezfirearms:sounds/musket/musket-fire-4") },
				}
			},
			// Maltiez Crossbows //
			{
				"maltiezcrossbows:crossbow", new WeaponProps {
					ammoCodes = "maltiezcrossbows:bolt",
					idleAnims = "arquebusidle",
					walkAnims = "arquebuswalk",
					moveAnims = "arquebusmove",
					duckAnims = "duck",
					swimAnims = "swim",
					jumpAnims = "jump",
					drawAnims = "arquebusdraw",
					fireAnims = "arquebusfire",
					loadAnims = "arquebusload",
					bashAnims = "arquebusbash",
					stabAnims = "arquebusbash",
					drawAudio = new AssetLocation[] { new AssetLocation("maltiezcrossbows:sounds/loading/wooden-click") },
					fireAudio = new AssetLocation[] { new AssetLocation( "maltiezcrossbows:sounds/release/simple-0"), new AssetLocation("maltiezcrossbows:sounds/release/simple-1"), new AssetLocation("maltiezcrossbows:sounds/release/simple-2"), new AssetLocation("maltiezcrossbows:sounds/release/simple-3") },
				}
			}
		};
	}
}