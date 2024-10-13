using ProtoBuf;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace VSKingdom {
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class ClientDataCache {
		public ClientDataCache() { }
		public string kingdomNAME { get; set; }
		public string cultureNAME { get; set; }
		public string leadersNAME { get; set; }
		public string coloursHEXA { get; set; } = "#ffffff";
		public string coloursHEXB { get; set; } = "#ffffff";
		public string coloursHEXC { get; set; } = "#ffffff";

		public void UpdateLoyalty(Entity entity) {
			kingdomNAME = entity.WatchedAttributes.GetString("kingdomNAME");
			cultureNAME = entity.WatchedAttributes.GetString("cultureNAME");
			leadersNAME = entity.WatchedAttributes.GetString("leadersNAME");
		}

		public void UpdateLoyalty(string kingdom, string culture, string leaders) {
			if (kingdom != null) { kingdomNAME = kingdom; }
			if (culture != null) { cultureNAME = culture; }
			if (leaders != null) { leadersNAME = leaders; }
		}

		public void UpdateColours(string colourA, string colourB, string colourC) {
			if (colourA != null) { coloursHEXA = colourA; }
			if (colourB != null) { coloursHEXB = colourB; }
			if (colourC != null) { coloursHEXC = colourC; }
		}
	}
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class SentryDataCache {
		public SentryDataCache() { }
		public bool usesMelee { get; set; } = true;
		public bool usesRange { get; set; } = false;
		public bool weapReady { get; set; } = false;
		public float walkSpeed { get; set; } = 0.020f;
		public float moveSpeed { get; set; } = 0.040f;
		public float weapRange { get; set; } = 1.500f;
		public float healRates { get; set; } = 1.000f;
		public float postRange { get; set; } = 6.000f;
		public Vec3d postBlock { get; set; } = new Vec3d();
		public string weapCodes { get; set; } = null;
		public string ammoCodes { get; set; } = null;
		public string idleAnims { get; set; } = "idle";
		public string walkAnims { get; set; } = "walk";
		public string moveAnims { get; set; } = "move";
		public string duckAnims { get; set; } = "duck";
		public string swimAnims { get; set; } = "swim";
		public string jumpAnims { get; set; } = "jump";
		public string drawAnims { get; set; } = "draw";
		public string fireAnims { get; set; } = "fire";
		public string loadAnims { get; set; } = "load";
		public string stabAnims { get; set; } = "bash";
		public string kingdomGUID { get; set; } = CommonersID;
		public string cultureGUID { get; set; } = SeraphimsID;
		public string leadersGUID { get; set; } = null;
		public string recruitINFO { get; set; } = "CIVILIAN";
		public string[] enemiesLIST { get; set; } = new string[] { BanditrysID };
		public string[] friendsLIST { get; set; } = new string[] { CommonersID };
		public string[] outlawsLIST { get; set; } = new string[] { null };

		public void UpdateLoyalty(Entity entity) {
			kingdomGUID = entity.WatchedAttributes.GetString("kingdomGUID");
			cultureGUID = entity.WatchedAttributes.GetString("cultureGUID");
			leadersGUID = entity.WatchedAttributes.GetString("leadersGUID");
		}

		public void UpdateLoyalty(string kingdom, string culture, string leaders) {
			if (kingdom != null) { kingdomGUID = kingdom; }
			if (culture != null) { cultureGUID = culture; }
			if (leaders != null) { leadersGUID = leaders; }
		}
		
		public void UpdateWeapons(IInventory gearInv) {
			if (!gearInv[16].Empty) {
				AssetLocation _weapCode = gearInv[16]?.Itemstack.Collectible.Code;
				weapCodes = (_weapCode?.Domain + ":" + _weapCode?.FirstCodePart()) ?? "";
			}
			if (!gearInv[18].Empty) {
				AssetLocation _ammoCode = gearInv[18]?.Itemstack.Collectible.Code;
				ammoCodes = (_ammoCode?.Domain + ":" + _ammoCode?.FirstCodePart()) ?? "";
			}
			string _searchFor = (weapCodes != null && Constants.GlobalProps.WeaponProperties.ContainsKey(weapCodes)) ? weapCodes : "";
			var _weapClass = Constants.GlobalProps.WeaponProperties[_searchFor];
			string[] _weapAnims = _weapClass.allCodes;
			idleAnims = _weapAnims[0];
			walkAnims = _weapAnims[1];
			moveAnims = _weapAnims[2];
			duckAnims = _weapAnims[3];
			swimAnims = _weapAnims[4];
			jumpAnims = _weapAnims[5];
			drawAnims = _weapAnims[6];
			fireAnims = _weapAnims[7];
			loadAnims = _weapAnims[8];
			stabAnims = _weapAnims[9];
		}

		public void UpdateReloads(IInventory gearInv) {
			weapReady = usesMelee || (!gearInv[16].Empty && !gearInv[18].Empty && Constants.GlobalProps.WeaponProperties[weapCodes].ammoCodes == ammoCodes);
		}

		public void UpdateEnemies(string[] codes) {
			enemiesLIST = codes;
		}

		public void UpdateFriends(string[] codes) {
			friendsLIST = codes;
		}

		public void UpdateOutlaws(string[] codes) {
			outlawsLIST = codes;
		}

		public void UpdateOutpost(Vec3d block, float range) {
			postBlock = block;
			postRange = range;
		}

		public string[] GetAnimations() {
			return new string[] { idleAnims, walkAnims, moveAnims, duckAnims, swimAnims, jumpAnims, drawAnims, fireAnims, loadAnims, stabAnims };
		}
	}
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class AnimationsCache {
		public string weapCodes { get; set; } = null;
		public string ammoCodes { get; set; } = null;
		public string idleAnims { get; set; } = "idle";
		public string walkAnims { get; set; } = "walk";
		public string moveAnims { get; set; } = "move";
		public string duckAnims { get; set; } = "duck";
		public string swimAnims { get; set; } = "swim";
		public string jumpAnims { get; set; } = "jump";
		public string drawAnims { get; set; } = "draw";
		public string fireAnims { get; set; } = "fire";
		public string loadAnims { get; set; } = "load";
		public string stabAnims { get; set; } = "bash";

		public void UpdateWeapons(IInventory gearInv) {
			if (!gearInv[16].Empty) {
				AssetLocation _weapCode = gearInv[16]?.Itemstack.Collectible.Code;
				weapCodes = (_weapCode?.Domain + ":" + _weapCode?.FirstCodePart()) ?? "";
			}
			if (!gearInv[18].Empty) {
				AssetLocation _ammoCode = gearInv[18]?.Itemstack.Collectible.Code;
				ammoCodes = (_ammoCode?.Domain + ":" + _ammoCode?.FirstCodePart()) ?? "";
			}
			string _searchFor = (weapCodes != null && Constants.GlobalProps.WeaponProperties.ContainsKey(weapCodes)) ? weapCodes : "";
			var _weapClass = Constants.GlobalProps.WeaponProperties[_searchFor];
			string[] _weapAnims = _weapClass.allCodes;
			idleAnims = _weapAnims[0];
			walkAnims = _weapAnims[1];
			moveAnims = _weapAnims[2];
			duckAnims = _weapAnims[3];
			swimAnims = _weapAnims[4];
			jumpAnims = _weapAnims[5];
			drawAnims = _weapAnims[6];
			fireAnims = _weapAnims[7];
			loadAnims = _weapAnims[8];
			stabAnims = _weapAnims[9];
		}

		public AnimationMetaData[] GetAnims() {
			return new AnimationMetaData[] {
				new AnimationMetaData {
					Code = idleAnims,
					Animation = idleAnims,
					EaseOutSpeed = 10000f,
					EaseInSpeed = 10000f,
					BlendMode = EnumAnimationBlendMode.Average,
					Weight = 10,
					TriggeredBy = new AnimationTrigger() { OnControls = new EnumEntityActivity[] { EnumEntityActivity.Idle }, DefaultAnim = true }
				},
				new AnimationMetaData {
					Code = walkAnims,
					Animation = walkAnims,
					MulWithWalkSpeed = true,
					BlendMode = EnumAnimationBlendMode.Average,
					ElementWeight = new Dictionary<string, float> { { "UpperFootR", 2f }, { "UpperFootL", 2f }, { "LowerFootR", 2f }, { "LowerFootL", 2f }, { "UpperArmR", 2f }, { "LowerArmR", 2f }, { "UpperArmL", 2f }, { "LowerArmL", 2f } },
					TriggeredBy = new AnimationTrigger() { OnControls = new EnumEntityActivity[] { EnumEntityActivity.Move }, MatchExact = true }
				},
				new AnimationMetaData {
					Code = moveAnims,
					Animation = moveAnims,
					MulWithWalkSpeed = true,
					BlendMode = EnumAnimationBlendMode.Average,
					ElementWeight = new Dictionary<string, float> { { "UpperFootR", 2f }, { "UpperFootL", 2f }, { "LowerFootR", 2f }, { "LowerFootL", 2f }, { "UpperArmR", 2f }, { "LowerArmR", 2f }, { "UpperArmL", 2f }, { "LowerArmL", 2f } },
					TriggeredBy = new AnimationTrigger() { OnControls = new EnumEntityActivity[] { EnumEntityActivity.Move, EnumEntityActivity.SprintMode }, MatchExact = true }
				},
				new AnimationMetaData {
					Code = duckAnims,
					Animation = duckAnims,
					MulWithWalkSpeed = true,
					BlendMode = EnumAnimationBlendMode.Average,
					AnimationSpeed = 3f,
					TriggeredBy = new AnimationTrigger() { OnControls = new EnumEntityActivity[] { EnumEntityActivity.Move, EnumEntityActivity.SneakMode }, MatchExact = true }
				},
				new AnimationMetaData {
					Code = "duckidle",
					Animation = "duckidle",
					MulWithWalkSpeed = true,
					BlendMode = EnumAnimationBlendMode.Average,
					AnimationSpeed = 3f,
					TriggeredBy = new AnimationTrigger() { OnControls = new EnumEntityActivity[] { EnumEntityActivity.Idle, EnumEntityActivity.SneakMode }, MatchExact = true }
				},
				new AnimationMetaData {
					Code = swimAnims,
					Animation = swimAnims,
					MulWithWalkSpeed = true,
					BlendMode = EnumAnimationBlendMode.Average,
					TriggeredBy = new AnimationTrigger() { OnControls = new EnumEntityActivity[] { EnumEntityActivity.Move, EnumEntityActivity.Swim }, MatchExact = true }
				},
				new AnimationMetaData {
					Code = "swimidle",
					Animation = "swimidle",
					MulWithWalkSpeed = true,
					BlendMode = EnumAnimationBlendMode.Average,
					TriggeredBy = new AnimationTrigger() { OnControls = new EnumEntityActivity[] { EnumEntityActivity.Idle, EnumEntityActivity.Swim }, MatchExact = true }
				},
				new AnimationMetaData {
					Code = jumpAnims,
					Animation = jumpAnims,
					BlendMode = EnumAnimationBlendMode.Average,
					TriggeredBy = new AnimationTrigger() { OnControls = new EnumEntityActivity[] { EnumEntityActivity.Move, EnumEntityActivity.Jump }, MatchExact = true }
				},
				new AnimationMetaData {
					Code = "jump",
					Animation = "jump",
					BlendMode = EnumAnimationBlendMode.Average,
					TriggeredBy = new AnimationTrigger() { OnControls = new EnumEntityActivity[] { EnumEntityActivity.Move, EnumEntityActivity.Jump }, MatchExact = true }
				},
				new AnimationMetaData {
					Code = "laddergoup",
					Animation = "laddergoup",
					BlendMode = EnumAnimationBlendMode.Average,
					TriggeredBy = new AnimationTrigger() { OnControls = new EnumEntityActivity[] { EnumEntityActivity.Move, EnumEntityActivity.Climb }, MatchExact = true }
				},
				new AnimationMetaData {
					Code = "ladderidle",
					Animation = "ladderidle",
					BlendMode = EnumAnimationBlendMode.Average,
					TriggeredBy = new AnimationTrigger() { OnControls = new EnumEntityActivity[] { EnumEntityActivity.Idle, EnumEntityActivity.Climb }, MatchExact = true }
				},
				new AnimationMetaData {
					Code = drawAnims,
					Animation = drawAnims,
					BlendMode = EnumAnimationBlendMode.Add,
					ElementBlendMode = new Dictionary<string, EnumAnimationBlendMode> { { "UpperArmR", EnumAnimationBlendMode.AddAverage }, { "LowerArmR", EnumAnimationBlendMode.AddAverage }, { "UpperArmL", EnumAnimationBlendMode.AddAverage }, { "LowerArmL", EnumAnimationBlendMode.AddAverage } },
					ElementWeight = new Dictionary<string, float> { { "UpperArmR", 20f }, { "LowerArmR", 20f }, { "UpperArmL", 20f }, { "LowerArmL", 20f } }
				},
				new AnimationMetaData {
					Code = fireAnims,
					Animation = fireAnims,
					BlendMode = EnumAnimationBlendMode.Add,
					ElementBlendMode = new Dictionary<string, EnumAnimationBlendMode> { { "UpperArmR", EnumAnimationBlendMode.AddAverage }, { "LowerArmR", EnumAnimationBlendMode.AddAverage }, { "UpperArmL", EnumAnimationBlendMode.AddAverage }, { "LowerArmL", EnumAnimationBlendMode.AddAverage } },
					ElementWeight = new Dictionary<string, float> { { "UpperArmR", 20f }, { "LowerArmR", 20f }, { "UpperArmL", 20f }, { "LowerArmL", 20f } }
				},
				new AnimationMetaData {
					Code = loadAnims,
					Animation = loadAnims,
					BlendMode = EnumAnimationBlendMode.Add,
					ElementBlendMode = new Dictionary<string, EnumAnimationBlendMode> { { "UpperArmR", EnumAnimationBlendMode.AddAverage }, { "LowerArmR", EnumAnimationBlendMode.AddAverage }, { "UpperArmL", EnumAnimationBlendMode.AddAverage }, { "LowerArmL", EnumAnimationBlendMode.AddAverage } },
					ElementWeight = new Dictionary<string, float> { { "UpperArmR", 20f }, { "LowerArmR", 20f }, { "UpperArmL", 20f }, { "LowerArmL", 20f } }
				},
				new AnimationMetaData {
					Code = stabAnims,
					Animation = stabAnims,
					BlendMode = EnumAnimationBlendMode.Add,
					Weight = 6f
				},
				new AnimationMetaData {
					Code = "dies",
					Animation = "dies",
					BlendMode = EnumAnimationBlendMode.AddAverage,
					AnimationSpeed = 1.75f,
					Weight = 10f,
					TriggeredBy = new AnimationTrigger() { OnControls = new EnumEntityActivity[] { EnumEntityActivity.Dead } }
				},
				new AnimationMetaData {
					Code = "hurt",
					Animation = "hurt",
					BlendMode = EnumAnimationBlendMode.Add
				}
			};
		}
	}
}
