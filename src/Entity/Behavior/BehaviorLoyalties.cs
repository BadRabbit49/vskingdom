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

		public bool commandATTACK {
			get => loyalties.GetBool("command_attack");
			set => loyalties.SetBool("command_attack", value);
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
		
		public Kingdom cachedKingdom { get => DataUtility.GetKingdom(kingdomGUID, null); }

		public Culture cachedCulture { get => DataUtility.GetCulture(cultureGUID, null); }

		public IPlayer cachedLeaders { get => entity.World.PlayerByUid(leadersGUID); }

		public BlockEntityPost cachedOutpost { get => DataUtility.GetOutpost(outpostXYZD); }

		public override string PropertyName() {
			return "KingdomLoyalties";
		}

		public override void OnEntitySpawn() {
			base.OnEntitySpawn();
			try {
				if (kingdomGUID is null) {
					kingdomGUID = "00000000";
					cultureGUID = "00000000";
					outpostXYZD = entity.ServerPos.AsBlockPos;
				}
				if (entity is not EntityPlayer) {
					commandWANDER = true;
					commandFOLLOW = false;
					commandATTACK = true;
					commandPURSUE = true;
					commandSHIFTS = false;
					commandNIGHTS = false;
					commandRETURN = false;
					return;
				}
				loyalties.RemoveAttribute("leaders_name");
				loyalties.RemoveAttribute("leaders_guid");
				cachedKingdom.EntityUIDs.Add(entity.EntityId);
				cachedOutpost.EntityUIDs.Add(entity.EntityId);
			} catch { }
		}

		public override void OnEntityDespawn(EntityDespawnData despawn) {
			try {
				cachedKingdom.EntityUIDs.Remove(entity.EntityId);
				cachedOutpost.EntityUIDs.Remove(entity.EntityId);
			} catch { }
			base.OnEntityDespawn(despawn);
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
				if (entity.Alive && itemslot.Itemstack != null && (DataUtility.IsAFriend(kingdomGUID, byEntity))) {
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
				infotext.AppendLine(string.Concat(LangUtility.Get("gui-kingdom-guid"), kingdomGUID));
			}
			if (leadersGUID is not null) {
				//infotext.AppendLine(string.Concat(LangUtility.Get("gui-leaders-name"), DataUtility.GetAPlayer(leadersGUID).PlayerName));
				infotext.AppendLine(string.Concat(LangUtility.Get("gui-leaders-guid"), leadersGUID));
			}
			if (outpostXYZD is not null) {
				infotext.AppendLine(string.Concat(LangUtility.Get("gui-outpost-xyzd"), outpostXYZD.ToString()));
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
			capi.Network.SendEntityPacket(entity.EntityId, 1510);
			RequestedDialog?.Dispose();
			RequestedDialog = null;
		}

		public virtual void SetKingdom(string kingdomGUID) {
			if (kingdomGUID is null || kingdomGUID == "") {
				return;
			}
			KingdomCommand command = new KingdomCommand();
			command.entityID = entity.EntityId;
			command.commands = "switch_kingdom";
			command.oldGUIDs = this.kingdomGUID;
			command.newGUIDs = kingdomGUID;
			if (entity is EntityPlayer) {
				(entity.Api as ICoreServerAPI)?.Network.GetChannel("kingdomnetwork").SendPacket<KingdomCommand>(command, (entity as EntityPlayer).Player as IServerPlayer);
			} else {
				(entity.Api as ICoreServerAPI)?.Network.GetChannel("kingdomnetwork").SendPacket<KingdomCommand>(command, entity.GetBehavior<EntityBehaviorLoyalties>()?.cachedLeaders as IServerPlayer);
			}
			// Why did you make me do this?!
			(entity.Api as ICoreServerAPI)?.World.GetEntityById(entity.EntityId).WatchedAttributes.GetTreeAttribute("loyalties").SetString("kingdom_guid", kingdomGUID);
			(entity.Api as ICoreServerAPI)?.World.GetEntityById(entity.EntityId).WatchedAttributes.MarkPathDirty("loyalties");
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
				VSKingdom.serverAPI.World.GetEntityById(entity.EntityId).WatchedAttributes.GetTreeAttribute("health").SetFloat("currenthealth", healAmount);
				itemslot.TakeOut(1);
				itemslot.MarkDirty();
			}
		}

		private void TryOrderRally(ItemSlot itemslot, IPlayer player) {
			if (itemslot.Itemstack.Item is ItemBanner) {
				EntityPlayer playerEnt = player.Entity;
				// Activate orders for all surrounding soldiers of this player's faction to follow them!
				foreach (Entity soldier in entity.World.GetEntitiesAround(entity.ServerPos.XYZ, 15, 4, entity => (DataUtility.IsAFriend(entity, playerEnt)))) {
					var taskManager = soldier.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
					taskManager.ExecuteTask<AiTaskSentryFollow>();
				}
			}
		}
	}
}