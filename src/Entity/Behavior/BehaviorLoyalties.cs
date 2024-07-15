using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Vintagestory.API.Config;
using System.Collections.Generic;
using Vintagestory.API.Util;

namespace VSKingdom {
	public class EntityBehaviorLoyalties : EntityBehavior {
		public EntityBehaviorLoyalties(Entity entity) : base(entity) { this.ServerAPI = entity.Api as ICoreServerAPI; }

		public ICoreServerAPI ServerAPI;

		public ITreeAttribute loyalties {
			get {
				return entity.WatchedAttributes.GetOrAddTreeAttribute("loyalties");
			}
			set {
				entity.WatchedAttributes.SetAttribute("loyalties", value);
				entity.WatchedAttributes.MarkPathDirty("loyalties");
			}
		}

		public EnlistedStatus enlistedStatus {
			get {
				EnlistedStatus level;
				if (Enum.TryParse<EnlistedStatus>(loyalties.GetString("enlistedStatus"), out level)) {
					return level;
				}
				return EnlistedStatus.CIVILIAN;
			}
			set {
				loyalties.SetString("enlistedStatus", value.ToString());
				entity.WatchedAttributes.MarkPathDirty("loyalties");
			}
		}

		public string kingdomGUID {
			get => loyalties.GetString("kingdom_guid");
			set => loyalties.SetString("kingdom_guid", value);
		}

		public string cultureGUID {
			get => loyalties.GetString("culture_guid");
			set => loyalties.SetString("culture_guid", value);
		}

		public string leadersGUID {
			get => loyalties.GetString("leaders_guid");
			set => loyalties.SetString("leaders_guid", value);
		}

		public double outpostSIZE {
			get => loyalties.GetDouble("outpost_size");
			set => loyalties.SetDouble("outpost_size", value);
		}

		public BlockPos outpostXYZD {
			get => loyalties.GetBlockPos("outpost_xyzd");
			set => loyalties.SetBlockPos("outpost_xyzd", value);
		}

		public bool commandWANDER {
			get => loyalties.GetBool("command_wander");
			set => loyalties.SetBool("command_wander", value);
		}

		public bool commandFOLLOW {
			get => loyalties.GetBool("command_follow");
			set => loyalties.SetBool("command_follow", value);
		}

		public bool commandFIRING {
			get => loyalties.GetBool("command_firing");
			set => loyalties.SetBool("command_firing", value);
		}

		public bool commandPURSUE {
			get => loyalties.GetBool("command_pursue");
			set => loyalties.SetBool("command_pursue", value);
		}

		public bool commandSHIFTS {
			get => loyalties.GetBool("command_shifts");
			set => loyalties.SetBool("command_shifts", value);
		}

		public bool commandNIGHTS {
			get => loyalties.GetBool("command_nights");
			set => loyalties.SetBool("command_nights", value);
		}

		public bool commandRETURN {
			get => loyalties.GetBool("command_return");
			set => loyalties.SetBool("command_return", value);
		}
		
		public Kingdom cachedKingdom { get => kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID); }

		public Culture cachedCulture { get => cultureList.Find(cultureMatch => cultureMatch.CultureGUID == cultureGUID); }

		public IPlayer cachedLeaders { get => entity.World.PlayerByUid(leadersGUID); }

		public BlockEntityPost cachedOutpost { get => (ServerAPI.World.BlockAccessor.GetBlockEntity(outpostXYZD) as BlockEntityPost) ?? null; }

		public override string PropertyName() {
			return "KingdomLoyalties";
		}
		public override void AfterInitialized(bool onFirstSpawn) {
			base.AfterInitialized(onFirstSpawn);
			if (entity is EntitySentry sentry) {
				if (kingdomGUID is null) {
					kingdomGUID = sentry.baseGroup;
				}
				if (cultureGUID is null) {
					cultureGUID = "00000000";
				}
				if (outpostXYZD is null) {
					outpostXYZD = entity.ServerPos.AsBlockPos;
				}
				commandWANDER = true;
				commandFOLLOW = false;
				commandFIRING = true;
				commandPURSUE = true;
				commandSHIFTS = false;
				commandNIGHTS = false;
				commandRETURN = false;
			}
		}

		public override void OnEntitySpawn() {
			base.OnEntitySpawn();
			if (entity is EntityPlayer) {
				if (kingdomGUID is null) {
					kingdomGUID = "00000000";
				}
				if (cultureGUID is null) {
					cultureGUID = "00000000";
				}
				loyalties.RemoveAttribute("leaders_name");
				loyalties.RemoveAttribute("leaders_guid");
			}
		}

		public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled) {
			// Other players won't be interactable for now.
			if (byEntity is EntityPlayer player && mode == EnumInteractMode.Interact && entity is not EntityPlayer) {
				// Remind them to join their leaders kingdom if they aren't already in it.
				if (leadersGUID == player.PlayerUID && kingdomGUID != player.GetBehavior<EntityBehaviorLoyalties>()?.kingdomGUID) {
					SetKingdom(byEntity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") ?? kingdomGUID);
				}
				// While a STRANGER has something in their ACTIVE SLOT try commands.
				if (enlistedStatus == EnlistedStatus.CIVILIAN && entity.Alive && cachedLeaders is null && itemslot.Itemstack != null && player.Controls.Sneak) {
					TryRecruiting(itemslot, player.Player);
					return;
				}
				// Try to revive if the entity is dead but not a carcass.
				if (!entity.Alive && itemslot.Itemstack != null && byEntity.Controls.Sneak) {
					TryReviveWith(itemslot);
					return;
				}
				// While the OWNER or a GROUPMEMBER has something in their ACTIVE SLOT try commands.
				if (entity.Alive && itemslot.Itemstack != null && byEntity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") == kingdomGUID) {
					TryOrderRally(itemslot, player.Player);
					return;
				}
				// While the OWNER is SNEAKING with an EMPTY SLOT open inventory dialogbox.
				if (leadersGUID == player.PlayerUID && player.Controls.Sneak && itemslot.Empty) {
					(entity as EntitySentry).ToggleInventoryDialog(player.Player);
				}
			} else {
				base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
			}
		}

		public override void GetInfoText(StringBuilder infotext) {
			// Place Kingdom (if any), Leader (if any) and Guardpost (if any), here.
			if (kingdomGUID is not null) {
				//infotext.AppendLine(string.Concat(LangUtility.Get("gui-kingdom-name"), DataUtility.GetKingdomName(kingdomGUID)));
				infotext.AppendLine(string.Concat(Lang.Get("vskingdom:gui-kingdom-guid"), kingdomGUID));
			}
			if (leadersGUID is not null) {
				//infotext.AppendLine(string.Concat(LangUtility.Get("gui-leaders-name"), DataUtility.GetAPlayer(leadersGUID).PlayerName));
				infotext.AppendLine(string.Concat(Lang.Get("vskingdom:gui-leaders-guid"), leadersGUID));
			}
			if (outpostXYZD is not null) {
				infotext.AppendLine(string.Concat(Lang.Get("vskingdom:gui-outpost-xyzd"), outpostXYZD.ToString()));
			}
			base.GetInfoText(infotext);
		}

		public virtual void SetKingdom(string kingdomGUID) {
			if (kingdomGUID is null || kingdomGUID == "") {
				return;
			}
			// Why did you make me do this?!
			(entity.Api as ICoreServerAPI)?.World.GetEntityById(entity.EntityId).WatchedAttributes.GetTreeAttribute("loyalties").SetString("kingdom_guid", kingdomGUID);
			(entity.Api as ICoreServerAPI)?.World.GetEntityById(entity.EntityId).WatchedAttributes.MarkPathDirty("loyalties");
		}

		public virtual void SetCulture(string cultureGUID) {
			if (cultureGUID is null || cultureGUID == "") {
				return;
			}
			// Why did you make me do this?!
			ServerAPI.World.GetEntityById(entity.EntityId).WatchedAttributes.GetTreeAttribute("loyalties").SetString("culture_guid", cultureGUID);
			ServerAPI.World.GetEntityById(entity.EntityId).WatchedAttributes.MarkPathDirty("loyalties");
		}

		public virtual void SetLeaders(string playerUID) {
			if (playerUID is not null && entity.World.PlayerByUid(playerUID) is not null) {
				leadersGUID = playerUID;
			}
			if (cachedLeaders.Entity.HasBehavior<EntityBehaviorLoyalties>()) {
				SetKingdom(cachedLeaders.Entity.GetBehavior<EntityBehaviorLoyalties>()?.kingdomGUID);
			}
		}
		
		public virtual void SetOutpost(BlockPos blockPos) {
			if (blockPos.X == 0 && blockPos.Y == 0 && blockPos.Z == 0) {
				(entity.Api as ICoreServerAPI)?.World.GetEntityById(entity.EntityId).WatchedAttributes.GetTreeAttribute("loyalties").SetDouble("outpost_size", 5.0);
				(entity.Api as ICoreServerAPI)?.World.GetEntityById(entity.EntityId).WatchedAttributes.MarkPathDirty("loyalties");
				return;
			}
			if (entity.World.BlockAccessor.GetBlockEntity(blockPos) is BlockEntityPost outpost) {
				if (outpost is not null && outpost.IsCapacity(entity.EntityId)) {
					(entity.Api as ICoreServerAPI)?.World.GetEntityById(entity.EntityId).WatchedAttributes.GetTreeAttribute("loyalties").SetDouble("outpost_size", outpost.areasize);
					(entity.Api as ICoreServerAPI)?.World.GetEntityById(entity.EntityId).WatchedAttributes.GetTreeAttribute("loyalties").SetBlockPos("outpost_xyzd", blockPos);
					(entity.Api as ICoreServerAPI)?.World.GetEntityById(entity.EntityId).WatchedAttributes.MarkPathDirty("loyalties");
				}
			}
		}

		public virtual void SetCommand(string commands, bool value) {
			// This is a very brute-force and unsophisticated way of doing this. In the future it should be changed.
			(entity.Api as ICoreServerAPI)?.World.GetEntityById(entity.EntityId).WatchedAttributes.GetTreeAttribute("loyalties").SetBool(commands, value);
			(entity.Api as ICoreServerAPI)?.World.GetEntityById(entity.EntityId).WatchedAttributes.MarkPathDirty("loyalties");
		}

		private void TryRecruiting(ItemSlot itemslot, IPlayer player) {
			// If the entity isn't already owned, giving it some kind of currency will hire it on to join.
			if (itemslot.Itemstack.ItemAttributes["currency"].Exists) {
				itemslot.TakeOut(1);
				itemslot.MarkDirty();
				// Set the owner to this player, and set the enlistment to ENLISTED.
				enlistedStatus = EnlistedStatus.ENLISTED;
				// If the owner also is in a group then go ahead and join that too.
				string newKingdomGUID = player.Entity.GetBehavior<EntityBehaviorLoyalties>()?.kingdomGUID ?? loyalties.GetString("kingdom_guid");
				(entity.Api as ICoreServerAPI)?.World.GetEntityById(entity.EntityId).WatchedAttributes.GetTreeAttribute("loyalties").SetString("kingdom_guid", newKingdomGUID);
				(entity.Api as ICoreServerAPI)?.World.GetEntityById(entity.EntityId).WatchedAttributes.MarkPathDirty("loyalties");
			}
		}

		private void TryReviveWith(ItemSlot itemslot) {
			if (itemslot.Itemstack.Item is ItemPoultice) {
				float healAmount = itemslot.Itemstack.ItemAttributes["health"].AsFloat();
				entity.Revive();
				(entity.Api as ICoreServerAPI)?.World.GetEntityById(entity.EntityId).WatchedAttributes.GetTreeAttribute("health").SetFloat("currenthealth", healAmount);
				itemslot.TakeOut(1);
				itemslot.MarkDirty();
			}
		}

		private void TryOrderRally(ItemSlot itemslot, IPlayer player) {
			if (itemslot.Itemstack.Item is ItemBanner) {
				EntityPlayer playerEnt = player.Entity;
				string playerKingdomID = playerEnt.WatchedAttributes.GetTreeAttribute("loyalties").GetString("kingdom_guid");
				// Activate orders for all surrounding soldiers of this player's faction to follow them!
				foreach (Entity soldier in entity.World.GetEntitiesAround(entity.ServerPos.XYZ, 15, 4, entity => (entity is EntitySentry sentry && sentry.kingdomID == playerKingdomID))) {
					var taskManager = soldier.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
					taskManager.ExecuteTask<AiTaskSentryFollow>();
				}
			}
		}

		private byte[] kingdomData { get => (entity.Api as ICoreServerAPI)?.WorldManager.SaveGame.GetData("kingdomData"); }
		private byte[] cultureData { get => (entity.Api as ICoreServerAPI)?.WorldManager.SaveGame.GetData("cultureData"); }
		private List<Kingdom> kingdomList => kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);
		private List<Culture> cultureList => cultureData is null ? new List<Culture>() : SerializerUtil.Deserialize<List<Culture>>(cultureData);
	}
}