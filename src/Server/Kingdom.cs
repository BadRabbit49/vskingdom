using System.Collections.Generic;
using Vintagestory.API.Common.Entities;
using ProtoBuf;
using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.API.Server;

namespace VSKingdom {
	[ProtoContract]
	public class Kingdom {
		[ProtoMember(1)]
		public string KingdomName { get; set; }
		[ProtoMember(2)]
		public string KingdomLong { get; set; }
		[ProtoMember(3)]
		public string KingdomUID { get; set; }
		[ProtoMember(4)]
		public string LeadersName { get; set; }
		[ProtoMember(5)]
		public string LeadersUID { get; set; }
		[ProtoMember(6)]
		public string FoundedIRL { get; set; }
		[ProtoMember(7)]
		public string FoundedIGT { get; set; }
		/** PROBLEM IS PHOTOMEMBER CAN'T SEEM TO BINARIZE OR SET/GET LISTS **/
		[ProtoMember(8)]
		public HashSet<string> PlayerUIDs { get; set; } = new HashSet<string>();
		[ProtoMember(9)]
		public HashSet<string> EnemieUIDs { get; set; } = new HashSet<string>();
		[ProtoMember(10)]
		public HashSet<string> AtWarsUIDs { get; set; } = new HashSet<string>();
		[ProtoMember(11)]
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

		public Governance governance;
		public Priviliges commonrole;
		public Priviliges rulingrole;
		public List<ClassProperties> classspecs = new List<ClassProperties>();
		public Dictionary<Priviliges, ClassPrivileges> classright = new Dictionary<Priviliges, ClassPrivileges>();
		public Dictionary<string, Priviliges> PlayerRoles = new Dictionary<string, Priviliges>();

		public void SwitchMember(Entity oldEnt, Entity newEnt, List<Kingdom> kingdomList) {
			SwitchMember(oldEnt.EntityId, newEnt.EntityId, kingdomList);
		}

		public void SwitchMember(long oldEntUID, long newEntUID, List<Kingdom> kingdomList) {
			EntityUIDs.Remove(oldEntUID);
			EntityUIDs.Append(newEntUID);
			List<string> UIDList = new List<string>();
			foreach (Kingdom kingdom in kingdomList) {
				UIDList.Add(kingdom.KingdomUID);
			}
			Kingdom EnemyKingdom = null;
			foreach (string EnemyKingdomUID in AtWarsUIDs) {
				if (!UIDList.Contains(EnemyKingdomUID)) {
					// Looks like that kingdom is gone, remove it from the list.
					AtWarsUIDs.Remove(EnemyKingdomUID);
					continue;
				}
				EnemyKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomUID == EnemyKingdomUID);
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
				UIDList.Add(kingdom.KingdomUID);
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
				EnemyKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomUID == EnemyKingdomUID);
				if (isPlayer && !PlayerUIDs.Contains((entity as EntityPlayer)?.PlayerUID)) {
					EnemyKingdom.EnemieUIDs.Append((entity as EntityPlayer)?.PlayerUID);
				}
			}
		}
		
		/**public void RemoveMember(Entity entity) {
			if (EntityUIDs.Contains(entity.EntityId)) {
				EntityUIDs.Remove(entity.EntityId);
			}
			bool isPlayer = entity is EntityPlayer;
			if (isPlayer && !PlayerUIDs.Contains((entity as EntityPlayer)?.PlayerUID)) {
				PlayerUIDs.Remove((entity as EntityPlayer)?.PlayerUID);
			}
			byte[] kingdomData = VSKingdom.serverAPI?.WorldManager.SaveGame.GetData("kingdomData");
			List<Kingdom> kingdomList = kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);
			List<string> UIDList = new List<string>();
			foreach (Kingdom kingdom in kingdomList) {
				UIDList.Add(kingdom.KingdomUID);
			}
			Kingdom EnemyKingdom = null;
			foreach (string EnemyKingdomUID in AtWarsUIDs) {
				if (!UIDList.Contains(EnemyKingdomUID)) {
					// Looks like that kingdom is gone, remove it from the list.
					AtWarsUIDs.Remove(EnemyKingdomUID);
					continue;
				}
				EnemyKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomUID == EnemyKingdomUID);
				if (isPlayer) {
					EnemyKingdom.EnemieUIDs.Remove((entity as EntityPlayer)?.PlayerUID);
				}
			}
		}**/

		public void RemoveMember(Entity entity) {
			EntityUIDs.Remove(entity.EntityId);
			if (entity.HasBehavior<EntityBehaviorLoyalties>()) {
				entity.GetBehavior<EntityBehaviorLoyalties>().kingdomUID = null;
			}
		}

		public void RemoveMember(IServerPlayer player) {
			PlayerUIDs.Remove(player.PlayerUID);
			EntityUIDs.Remove(player.Entity.EntityId);
			if (LeadersUID == player.PlayerUID) {
				LeadersName = DataUtility.GetAPlayer(PlayerUIDs.First()).PlayerName;
				LeadersName = PlayerUIDs.First();
			}
		}

		public void DeclareWarOn(string targetUID) {
			if (!AtWarsUIDs.Contains(targetUID)) {
				AtWarsUIDs.Append(targetUID);
			}
		}

		public void SaveKingdoms(List<Kingdom> kingdomList) {
			VSKingdom.serverAPI?.WorldManager.SaveGame.StoreData("kingdomData", SerializerUtil.Serialize(kingdomList));
		}
	}
}