using ProtoBuf;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VSKingdom {
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class SentryDataCache {
		public SentryDataCache() { }
		public float walkSpeed { get; set; } = 0.015f;
		public float moveSpeed { get; set; } = 0.030f;
		public float weapRange { get; set; } = 1.500f;
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
		public string kingdomNAME { get; set; } = "Commoner";
		public string cultureGUID { get; set; } = GlobalCodes.seraphimGUID;
		public string cultureNAME { get; set; } = "Seraphic";
		public string leadersGUID { get; set; } = null;
		public string leadersNAME { get; set; } = null;
		public string recruitNAME { get; set; } = null;
		public string[] recruitINFO { get; set; } = new string[] { "melee", "CIVILIAN" };
		public string[] defaultINFO { get; set; } = new string[] { "00000000", "00000000" };
		public string[] coloursLIST { get; set; } = new string[] { "#ffffff", "#ffffff", "#ffffff" };
		public string[] enemiesLIST { get; set; } = new string[] { GlobalCodes.banditryGUID };
		public string[] friendsLIST { get; set; } = new string[] { GlobalCodes.commonerGUID };
		public string[] outlawsLIST { get; set; } = new string[] { null };

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

		public void UpdateColours(string[] codes) {
			coloursLIST = codes;
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

		public void UpdateLoyalty(ITreeAttribute loyalty) {
			kingdomGUID = loyalty.GetString("kingdom_guid");
			kingdomNAME = loyalty.GetString("kingdom_name");
			cultureGUID = loyalty.GetString("culture_guid");
			cultureNAME = loyalty.GetString("culture_name");
			leadersGUID = loyalty.GetString("leaders_guid");
			leadersNAME = loyalty.GetString("leaders_name");
		}
		
		public void UpdateNametag(ITreeAttribute nametag) {
			recruitNAME = new string($"{nametag.GetString("name")} {nametag.GetString("last")}");
		}
	}
}
