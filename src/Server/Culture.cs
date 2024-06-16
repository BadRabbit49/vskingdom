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
		public HashSet<string> MascFNames { get; set; } = new HashSet<string>();
		public HashSet<string> FemmFNames { get; set; } = new HashSet<string>();
		public HashSet<string> CommLNames { get; set; } = new HashSet<string>();
		public HashSet<string> SkinColors { get; set; } = new HashSet<string>();
		public HashSet<string> HairColors { get; set; } = new HashSet<string>();
		public HashSet<string> EyesColors { get; set; } = new HashSet<string>();
		public HashSet<string> HairStyles { get; set; } = new HashSet<string>();
		public HashSet<string> HairExtras { get; set; } = new HashSet<string>();
		public HashSet<string> FaceStyles { get; set; } = new HashSet<string>();
		public HashSet<string> FaceBeards { get; set; } = new HashSet<string>();
		public HashSet<string> WoodBlocks { get; set; } = new HashSet<string>();
		public HashSet<string> RockBlocks { get; set; } = new HashSet<string>();
	}
}