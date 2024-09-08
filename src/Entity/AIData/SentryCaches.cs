using ProtoBuf;
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
		public string bashAnims { get; set; } = "bash";
		public string stabAnims { get; set; } = "stab";
		public string kingdomGUID { get; set; } = commonerGUID;
		public string cultureGUID { get; set; } = seraphimGUID;
		public string leadersGUID { get; set; } = null;
		public string recruitINFO { get; set; } = "CIVILIAN";
		public string[] enemiesLIST { get; set; } = new string[] { banditryGUID };
		public string[] friendsLIST { get; set; } = new string[] { commonerGUID };
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
			AssetLocation _weapCode = gearInv[16].Itemstack.Collectible.Code;
			AssetLocation _ammoCode = gearInv[18].Itemstack.Collectible.Code;
			weapCodes = (_weapCode?.Domain + ":" + _weapCode?.FirstCodePart()) ?? "";
			ammoCodes = (_ammoCode?.Domain + ":" + _ammoCode?.FirstCodePart()) ?? "";
			if (WeaponProperties.ContainsKey(weapCodes)) {
				var _weapClass = WeaponProperties[weapCodes];
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
				bashAnims = _weapAnims[9];
				stabAnims = _weapAnims[10];
			}
		}

		public void UpdateReloads(IInventory gearInv) {
			weapReady = !gearInv[16].Empty && !gearInv[18].Empty && WeaponProperties[weapCodes].ammoCodes == ammoCodes;
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
	}
}
