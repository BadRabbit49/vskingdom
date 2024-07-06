using ProtoBuf;
using System.Collections.Generic;

namespace VSKingdom {
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class Kingdom {
		public string KingdomGUID { get; set; }
		public string KingdomNAME { get; set; }
		public string KingdomLONG { get; set; }
		public string KingdomDESC { get; set; }
		public string KingdomTYPE { get; set; }
		public string LeadersGUID { get; set; }
		public string LeadersNAME { get; set; }
		public string LeadersLONG { get; set; }
		public string LeadersDESC { get; set; }
		public string FoundedDATE { get; set; }
		public string FoundedMETA { get; set; }
		public double FoundedHOUR { get; set; }
		public string MembersROLE { get; set; }
		public string CurrentVOTE { get; set; }
		public HashSet<string> PlayersGUID { get; set; } = new HashSet<string>();
		public HashSet<string> PlayersINFO { get; set; } = new HashSet<string>();
		public HashSet<string> EnemiesGUID { get; set; } = new HashSet<string>();
		public HashSet<string> CurrentWars { get; set; } = new HashSet<string>();
		public HashSet<string> InvitesGUID { get; set; } = new HashSet<string>();
		public HashSet<string> RequestGUID { get; set; } = new HashSet<string>();
		public HashSet<long> EntitiesALL { get; set; } = new HashSet<long>();
	}
}