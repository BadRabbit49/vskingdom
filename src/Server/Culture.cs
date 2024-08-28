using ProtoBuf;
using System.Collections.Generic;

namespace VSKingdom {
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class Culture {
		public string CultureGUID { get; set; }
		public string CultureNAME { get; set; }
		public string CultureLONG { get; set; }
		public string CultureDESC { get; set; }
		public string FounderGUID { get; set; }
		public string FoundedMETA { get; set; }
		public string FoundedDATE { get; set; }
		public double FoundedHOUR { get; set; }
		public string Predecessor { get; set; }
		public HashSet<string> PlayersGUID { get; set; } = new HashSet<string>();
		public HashSet<string> InvitesGUID { get; set; } = new HashSet<string>();
		public HashSet<string> MFirstNames { get; set; } = new HashSet<string>();
		public HashSet<string> FFirstNames { get; set; } = new HashSet<string>();
		public HashSet<string> FamilyNames { get; set; } = new HashSet<string>();
		public HashSet<string> SkinColours { get; set; } = new HashSet<string>();
		public HashSet<string> HairColours { get; set; } = new HashSet<string>();
		public HashSet<string> EyesColours { get; set; } = new HashSet<string>();
		public HashSet<string> HairsStyles { get; set; } = new HashSet<string>();
		public HashSet<string> HairsExtras { get; set; } = new HashSet<string>();
		public HashSet<string> FacesStyles { get; set; } = new HashSet<string>();
		public HashSet<string> FacesBeards { get; set; } = new HashSet<string>();
		public HashSet<string> WoodsBlocks { get; set; } = new HashSet<string>();
		public HashSet<string> StoneBlocks { get; set; } = new HashSet<string>();
	}
}