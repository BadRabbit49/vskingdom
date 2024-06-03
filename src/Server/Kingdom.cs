using ProtoBuf;
using System;
using System.Linq;
using System.Collections.Generic;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Util;

namespace VSKingdom {
	[ProtoContract]
	public class Kingdom {
		[ProtoMember(1)]
		public string KingdomGUID { get; set; }
		[ProtoMember(2)]
		public string KingdomNAME { get; set; }
		[ProtoMember(3)]
		public string KingdomLONG { get; set; }
		[ProtoMember(4)]
		public string KingdomDESC { get; set; }
		[ProtoMember(5)]
		public string KingdomTYPE { get; set; }
		[ProtoMember(6)]
		public string LeadersGUID { get; set; }
		[ProtoMember(7)]
		public string LeadersNAME { get; set; }
		[ProtoMember(8)]
		public string LeadersLONG { get; set; }
		[ProtoMember(9)]
		public string LeadersDESC { get; set; }
		[ProtoMember(10)]
		public string FoundingIRL { get; set; }
		[ProtoMember(11)]
		public string FoundingIGT { get; set; }
		[ProtoMember(12)]
		public HashSet<string> PlayerUIDs { get; set; } = new HashSet<string>();
		[ProtoMember(13)]
		public HashSet<string> EnemieUIDs { get; set; } = new HashSet<string>();
		[ProtoMember(14)]
		public HashSet<string> AtWarsUIDs { get; set; } = new HashSet<string>();
		[ProtoMember(15)]
		public HashSet<long> EntityUIDs { get; set; } = new HashSet<long>();

		public Entity[] EntityList {
			get {
				Entity[] list = Array.Empty<Entity>();
				foreach (var ent in EntityUIDs) {
					list.Append(VSKingdom.serverAPI.World.GetEntityById(ent));
				}
				return list;
			}
		}
	}
}