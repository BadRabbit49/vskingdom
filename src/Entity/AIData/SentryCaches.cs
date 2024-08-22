using ProtoBuf;
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
		public float walkSpeed { get; set; } = 0.015f;
		public float moveSpeed { get; set; } = 0.030f;
		public float weapRange { get; set; } = 1.500f;
		public float healRates { get; set; } = 1.000f;
		public float postRange { get; set; } = 6.000f;
		public Vec3d postBlock { get; set; } = new Vec3d();
		public string idleAnims { get; set; } = "idle";
		public string walkAnims { get; set; } = "walk";
		public string moveAnims { get; set; } = "move";
		public string duckAnims { get; set; } = "duck";
		public string swimAnims { get; set; } = "swim";
		public string jumpAnims { get; set; } = "jump";
		public string diesAnims { get; set; } = "dies";
		public string drawAnims { get; set; } = "draw";
		public string fireAnims { get; set; } = "fire";
		public string loadAnims { get; set; } = "load";
		public string bashAnims { get; set; } = "bash";
		public string stabAnims { get; set; } = "stab";
		public string kingdomGUID { get; set; } = GlobalCodes.commonerGUID;
		public string cultureGUID { get; set; } = GlobalCodes.seraphimGUID;
		public string leadersGUID { get; set; } = null;
		public string recruitINFO { get; set; } = "CIVILIAN";
		public string[] enemiesLIST { get; set; } = new string[] { GlobalCodes.banditryGUID };
		public string[] friendsLIST { get; set; } = new string[] { GlobalCodes.commonerGUID };
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

		public void UpdateAnimate(string[] codes) {
			idleAnims = codes[0];
			walkAnims = codes[1];
			moveAnims = codes[2];
			duckAnims = codes[3];
			swimAnims = codes[4];
			jumpAnims = codes[5];
			diesAnims = codes[6];
			drawAnims = codes[7];
			fireAnims = codes[8];
			loadAnims = codes[9];
			bashAnims = codes[10];
			stabAnims = codes[11];
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
