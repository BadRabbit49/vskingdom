using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class EntityBehaviorLoyalties : EntityBehavior {
		public EntityBehaviorLoyalties(Entity entity) : base(entity) { }

		public RequestedDialog RequestedDialog { get; set; }

		public ITreeAttribute loyalties {
			get {
				if (entity.WatchedAttributes.GetTreeAttribute("loyalties") is null) {
					entity.WatchedAttributes.SetAttribute("loyalties", new TreeAttribute());
				}
				return entity.WatchedAttributes.GetTreeAttribute("loyalties");
			}
			set {
				entity.WatchedAttributes.SetAttribute("loyalties", value);
				entity.WatchedAttributes.MarkPathDirty("loyalties");
			}
		}

		public CurrentCommand currentCommand {
			get {
				CurrentCommand level;
				if (Enum.TryParse<CurrentCommand>(loyalties.GetString("currentCommand"), out level)) {
					return level;
				} else {
					return CurrentCommand.WANDER;
				}
			}
			set {
				loyalties.SetString("currentCommand", value.ToString());
				entity.WatchedAttributes.MarkPathDirty("loyalties");
			}
		}

		public EnlistedStatus enlistedStatus {
			get {
				EnlistedStatus level;
				if (Enum.TryParse<EnlistedStatus>(loyalties.GetString("enlistedStatus"), out level)) {
					return level;
				} else {
					return EnlistedStatus.CIVILIAN;
				}
			}
			set {
				loyalties.SetString("enlistedStatus", value.ToString());
				entity.WatchedAttributes.MarkPathDirty("loyalties");
			}
		}

		public Specialization specialization {
			get {
				Specialization types;
				if (Enum.TryParse<Specialization>(loyalties.GetString("specialization"), out types)) {
					return types;
				} else {
					return Specialization.EMPTY;
				}
			}
			set {
				loyalties.SetString("specialization", value.ToString());
				entity.WatchedAttributes.MarkPathDirty("loyalties");
			}
		}

		public string kingdomUID {
			get => loyalties.GetString("kingdomUID");
			set {
				loyalties.SetString("kingdomUID", value);
				entity.WatchedAttributes.MarkPathDirty("loyalties");
			}
		}

		public string leadersUID {
			get => loyalties.GetString("leadersUID");
			set {
				loyalties.SetString("leadersUID", value);
				entity.WatchedAttributes.MarkPathDirty("loyalties");
			}
		}

		public BlockPos outpostPOS {
			get => loyalties.GetBlockPos("outpostPOS");
			set {
				loyalties.SetBlockPos("outpostPOS", value);
				entity.WatchedAttributes.MarkPathDirty("loyalties");
			}
		}

		public Kingdom cachedKingdom {
			/**get {
				if (_cachedKingdom?.KingdomUID == kingdomUID) {
					return _cachedKingdom;
				}
				if (_cachedKingdom?.KingdomUID is null) {
					return null;
				}
				// Join the leader's group.
				if (_cachedKingdom != null) {
					if (_cachedLeaders.Entity.HasBehavior<EntityBehaviorLoyalties>()) {
						_cachedKingdom = _cachedLeaders.Entity.GetBehavior<EntityBehaviorLoyalties>().cachedKingdom;
					}
				}
				return _cachedKingdom;
			}**/
			get => DataUtility.GetKingdom(null, kingdomUID);
		}

		public IPlayer cachedLeaders {
			/**get {
				if (_cachedLeaders?.PlayerUID == leadersUID) {
					return _cachedLeaders;
				}
				if (String.IsNullOrEmpty(leadersUID)) {
					return null;
				}
				_cachedLeaders = entity.World.PlayerByUid(leadersUID);
				return _cachedLeaders;
			}**/
			get => entity.World.PlayerByUid(leadersUID);
		}

		public BlockEntityPost cachedOutpost {
			/**get {
				if (_cachedOutpost is null) {
					return null;
				}
				return _cachedOutpost;
			}**/
			get => DataUtility.GetOutpost(outpostPOS);
		}

		public override string PropertyName() {
			return "KingdomLoyalties";
		}

		public override void OnEntitySpawn() {
			base.OnEntitySpawn();
			try {
				if (kingdomUID is null) {
					kingdomUID = "00000000";
					leadersUID = null;
					outpostPOS = entity.ServerPos.AsBlockPos;
					cachedKingdom.AddNewMember(entity);
				}
			} catch (NullReferenceException) { }
		}
		
		public override void OnEntityDespawn(EntityDespawnData despawn) {
			try {
				if (kingdomUID is not null) {
					cachedKingdom.RemoveMember(entity);
				}
			} catch (NullReferenceException) { }
			base.OnEntityDespawn(despawn);
		}

		public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled) {
			// Other players won't be interactable for now.
			if (entity is EntityPlayer) {
				base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
				return;
			}
			if (byEntity is EntityPlayer player && mode == EnumInteractMode.Interact) {
				// While a STRANGER has something in their ACTIVE SLOT try commands.
				if (enlistedStatus == EnlistedStatus.CIVILIAN && entity.Alive && cachedLeaders is null && itemslot.Itemstack != null && !player.Controls.Sneak) {
					TryRecruiting(itemslot, player.Player);
					return;
				}
				// Try to revive if the entity is dead but not a carcass.
				if (!entity.Alive && itemslot.Itemstack != null && player.Controls.Sneak) {
					TryReviveWith(itemslot);
					return;
				}
				// While the OWNER or a GROUPMEMBER has something in their ACTIVE SLOT try commands.
				if (!entity.Alive && itemslot.Itemstack != null && (leadersUID == player.PlayerUID || player.GetBehavior<EntityBehaviorLoyalties>().kingdomUID == kingdomUID)) {
					TryOrderRally(itemslot, player.Player);
					return;
				}
				// While the OWNER is SNEAKING with an EMPTY SLOT open inventory dialogbox.
				if (leadersUID == player.PlayerUID && player.Controls.Sneak && itemslot.Empty) {
					(entity as EntityArcher).ToggleInventoryDialog(player.Player);
				}
			} else {
				base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
			}
		}

		public override void GetInfoText(StringBuilder infotext) {
			// Place Kingdom (if any), Leader (if any) and Guardpost (if any), here.
			if (kingdomUID is not null) {
				infotext.AppendLine(string.Concat(LangUtility.Get("gui-kingdom-name"), DataUtility.GetKingdomName(kingdomUID)));
			}
			if (leadersUID is not null) {
				infotext.AppendLine(string.Concat(LangUtility.Get("gui-leaders-name"), DataUtility.GetLeadersName(leadersUID)));
			}
			if (leadersUID is null && kingdomUID is not null) {
				infotext.AppendLine(string.Concat(LangUtility.Get("gui-leaders-name"), DataUtility.GetLeaders(null, kingdomUID).PlayerName));
			}
			if (outpostPOS is not null) {
				infotext.AppendLine(string.Concat(LangUtility.Get("gui-outpost-xyzd"), outpostPOS.ToString()));
			}
			base.GetInfoText(infotext);
		}

		public virtual void GetRequestedDialog(IServerPlayer sender, string kingdomUID, string message) {
			if (entity.Api.Side != EnumAppSide.Client) {
				return;
			}
			var capi = (ICoreClientAPI)entity.Api;
			if (RequestedDialog is null) {
				RequestedDialog = new RequestedDialog((entity as EntityPlayer).Player as IServerPlayer, sender, kingdomUID, message, capi);
				RequestedDialog.OnClosed += OnRequestedDialogClosed;
			}
			if (RequestedDialog.IsOpened()) {
				RequestedDialog.TryClose();
			} else {
				RequestedDialog.TryOpen();
			}
		}

		public virtual void OnRequestedDialogClosed() {
			var capi = (ICoreClientAPI)entity.Api;
			capi.Network.SendEntityPacket(entity.EntityId, 1508);
			RequestedDialog?.Dispose();
			RequestedDialog = null;
		}

		public virtual void SetKingdom(string kingdomUID) {
			if (kingdomUID is null) {
				return;
			}
			cachedKingdom.RemoveMember(entity);
			this.kingdomUID = kingdomUID;
			cachedKingdom.AddNewMember(entity);
		}
		
		public virtual void SetLeaders(string leadersUID) {
			if (leadersUID is null) {
				return;
			}
			this.leadersUID = leadersUID;
			if (cachedLeaders.Entity.HasBehavior<EntityBehaviorLoyalties>()) {
				this.kingdomUID = cachedLeaders.Entity.GetBehavior<EntityBehaviorLoyalties>()?.kingdomUID;
			}
		}

		public virtual void SetOutpost(BlockPos outpostPOS) {
			if (outpostPOS is null) {
				return;
			}
			if (entity.World.BlockAccessor.GetBlockEntity(outpostPOS) is BlockEntityPost block) {
				if (block is null) {
					return;
				}
				if (block.IsCapacity(entity.EntityId)) {
					this.outpostPOS = outpostPOS;
					entity.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager?.GetTask<AiTaskSoldierReturningTo>()?.UpdatePostEnt(block);
				}
			}
		}

		public virtual void ClearBlockPOS() {
			// Can keep the outpostPOS so soldiers can still return home even if it is destroyed.
			outpostPOS = null;
			entity.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager?.GetTask<AiTaskSoldierReturningTo>()?.UpdatePostEnt(null);
		}

		public virtual void SendAllData() {
			var message = new KingdomProfileMsg();
			message.entityUID = entity.EntityId;
			message.kingdomUID = this.kingdomUID;
			message.leadersUID = this.leadersUID;
			message.outpostPOS = this.outpostPOS;
			(entity.Api as ICoreServerAPI)?.Network.GetChannel("kingdomNetwork")
				.SendPacket<KingdomProfileMsg>(message, this?.cachedLeaders as IServerPlayer);
		}

		public void SetUnitOrders(CurrentCommand orders) {
			currentCommand = orders;
			loyalties.SetString("currentCommand", orders.ToString());
			entity.WatchedAttributes.MarkPathDirty("loyalties");
		}
		
		private void TryRecruiting(ItemSlot itemslot, IPlayer player) {
			// If the entity isn't already owned, giving it some kind of currency will hire it on to join.
			if (itemslot.Itemstack.ItemAttributes["currency"].Exists) {
				itemslot.TakeOut(1);
				itemslot.MarkDirty();
				// Set the owner to this player, and set the enlistment to ENLISTED.
				enlistedStatus = EnlistedStatus.ENLISTED;
				// If the owner also is in a group then go ahead and join that too.
				kingdomUID = player.Entity.GetBehavior<EntityBehaviorLoyalties>()?.kingdomUID;
			}
		}

		private void TryReviveWith(ItemSlot itemslot) {
			try {
				if (itemslot.Itemstack.Collectible.Attributes["health"].Exists && entity.GetBehavior<EntityBehaviorHarvestable>()?.IsHarvested != true) {
					entity.Revive();
					if (entity.HasBehavior<EntityBehaviorHealth>()) {
						entity.GetBehavior<EntityBehaviorHealth>().Health = itemslot.Itemstack.Collectible.Attributes["health"].AsFloat();
					}
					itemslot.TakeOut(1);
					itemslot.MarkDirty();
				}
			} catch {
				entity.Api.World.Logger.Error("Caught error here! Item path was: " + itemslot?.Itemstack?.Collectible?.Code?.ToString());
			}
		}

		private void TryOrderRally(ItemSlot itemslot, IPlayer player) {
			if (itemslot.Itemstack.Item is ItemBanner) {
				EntityPlayer playerEnt = player.Entity;
				// Activate orders for all surrounding soldiers of this player's faction to follow them!
				foreach (Entity soldier in entity.World.GetEntitiesAround(entity.ServerPos.XYZ, 15, 4, entity => (SoldierUtility.CanFollowThis(entity, playerEnt)))) {
					var taskManager = soldier.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
					taskManager.ExecuteTask<AiTaskFollowEntityLeader>();
				}
			}
		}
	}
}