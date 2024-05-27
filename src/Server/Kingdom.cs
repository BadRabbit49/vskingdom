using System.Collections.Generic;
using ProtoBuf;
using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Util;
using Vintagestory.API.Server;

namespace VSKingdom {
	[ProtoContract]
	public class Kingdom {
		[ProtoMember(1)]
		public string KingdomGUID { get; set; }
		[ProtoMember(2)]
		public string KingdomName { get; set; }
		[ProtoMember(3)]
		public string KingdomLong { get; set; }
		[ProtoMember(4)]
		public string KingdomDesc { get; set; }
		[ProtoMember(5)]
		public string KingdomType { get; set; }
		[ProtoMember(6)]
		public string LeadersGUID { get; set; }
		[ProtoMember(7)]
		public string LeadersName { get; set; }
		[ProtoMember(8)]
		public string LeadersLong { get; set; }
		[ProtoMember(9)]
		public string LeadersDesc { get; set; }
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

		public void AddNewMember(Entity entity) {
			if (!EntityUIDs.Contains(entity.EntityId)) {
				EntityUIDs.Append(entity.EntityId);
			}
			bool isPlayer = entity is EntityPlayer;
			if (isPlayer && !PlayerUIDs.Contains((entity as EntityPlayer)?.PlayerUID)) {
				PlayerUIDs.Append((entity as EntityPlayer)?.PlayerUID);
			}
			byte[] kingdomData = VSKingdom.serverAPI.WorldManager.SaveGame.GetData("kingdomData");
			List<Kingdom> kingdomList = kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);
			List<string> UIDList = new List<string>();
			foreach (Kingdom kingdom in kingdomList) {
				UIDList.Add(kingdom.KingdomGUID);
			}
			if (AtWarsUIDs is null || AtWarsUIDs.Count == 0) {
				return;
			}
			Kingdom EnemyKingdom = null;
			foreach (string EnemyKingdomUID in AtWarsUIDs) {
				if (!UIDList.Contains(EnemyKingdomUID)) {
					// Looks like that kingdom is gone, remove it from the list.
					AtWarsUIDs.Remove(EnemyKingdomUID);
					continue;
				}
				EnemyKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == EnemyKingdomUID);
				if (isPlayer && !PlayerUIDs.Contains((entity as EntityPlayer)?.PlayerUID)) {
					EnemyKingdom.EnemieUIDs.Append((entity as EntityPlayer)?.PlayerUID);
				}
			}
		}

		public void RemoveMember(Entity entity) {
			EntityUIDs.Remove(entity.EntityId);
			if (entity.HasBehavior<EntityBehaviorLoyalties>()) {
				entity.GetBehavior<EntityBehaviorLoyalties>().kingdomUID = null;
			}
		}
	}
}