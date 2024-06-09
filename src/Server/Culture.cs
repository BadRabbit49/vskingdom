using ProtoBuf;

namespace VSKingdom {
	[ProtoContract]
	public class Culture {
		[ProtoMember(1)]
		public string CultureGUID { get; set; }
		[ProtoMember(2)]
		public string CultureNAME { get; set; }
		[ProtoMember(3)]
		public string CultureLONG { get; set; }
		[ProtoMember(4)]
		public string CultureDESC { get; set; }
		[ProtoMember(5)]
		public string FoundedMETA { get; set; }
		[ProtoMember(6)]
		public string FoundedDATE { get; set; }
		[ProtoMember(7)]
		public double FoundedHOUR { get; set; }
		[ProtoMember(8)]
		public string Predecessor { get; set; }
		[ProtoMember(9)]
		public string[] AllTenants { get; set; }
		[ProtoMember(10)]
		public string[] MascFNames { get; set; }
		[ProtoMember(11)]
		public string[] FemmFNames { get; set; }
		[ProtoMember(12)]
		public string[] CommLNames { get; set; }
		[ProtoMember(13)]
		public string[] SkinColors { get; set; }
		[ProtoMember(14)]
		public string[] HairColors { get; set; }
		[ProtoMember(15)]
		public string[] EyesColors { get; set; }
		[ProtoMember(16)]
		public string[] HairStyles { get; set; }
		[ProtoMember(17)]
		public string[] HairExtras { get; set; }
		[ProtoMember(18)]
		public string[] FaceStyles { get; set; }
		[ProtoMember(19)]
		public string[] FaceBeards { get; set; }
		[ProtoMember(20)]
		public string[] WoodBlocks { get; set; }
		[ProtoMember(21)]
		public string[] RockBlocks { get; set; }
	}
}