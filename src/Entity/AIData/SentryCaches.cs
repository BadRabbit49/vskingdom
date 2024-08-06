using ProtoBuf;

namespace VSKingdom {
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class SentryDataCache {
		public SentryDataCache() { }
		public SentryDataCache(SentryDataCache dataCache) {
			walkSpeed = dataCache.walkSpeed;
			moveSpeed = dataCache.moveSpeed;
			postRange = dataCache.postRange;
			weapRange = dataCache.weapRange;
			idleAnims = dataCache.idleAnims;
			walkAnims = dataCache.walkAnims;
			moveAnims = dataCache.moveAnims;
			duckAnims = dataCache.duckAnims;
			swimAnims = dataCache.swimAnims;
			jumpAnims = dataCache.jumpAnims;
			diesAnims = dataCache.diesAnims;
			drawAnims = dataCache.drawAnims;
			fireAnims = dataCache.fireAnims;
			loadAnims = dataCache.loadAnims;
			bashAnims = dataCache.bashAnims;
			stabAnims = dataCache.stabAnims;
			kingdomGUID = dataCache.kingdomGUID;
			kingdomNAME = dataCache.kingdomNAME;
			cultureGUID = dataCache.cultureGUID;
			cultureNAME = dataCache.cultureNAME;
			leadersGUID = dataCache.leadersGUID;
			leadersNAME = dataCache.leadersNAME;
			recruitNAME = dataCache.recruitNAME;
			recruitINFO = dataCache.recruitINFO;
			defaultINFO = dataCache.defaultINFO;
			coloursLIST = dataCache.coloursLIST;
			enemiesLIST = dataCache.enemiesLIST;
			friendsLIST = dataCache.friendsLIST;
			outlawsLIST = dataCache.outlawsLIST;
		}

		public float walkSpeed { get; set; } = 0.015f;
		public float moveSpeed { get; set; } = 0.030f;
		public float postRange { get; set; } = 3.0f;
		public float weapRange { get; set; } = 1.5f;
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
		public string[] enemiesLIST { get; set; } = new string[] { null };
		public string[] friendsLIST { get; set; } = new string[] { null };
		public string[] outlawsLIST { get; set; } = new string[] { null };

		public SentryDataCache Copy() {
			return new SentryDataCache(this);
		}
	}
}
