using System.IO;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class EntitySentry : EntityHumanoid {
		public EntitySentry() { }
		public InventorySentry gearInv { get; set; }
		public EntityTalkUtil talkUtil { get; set; }
		public InvSentryDialog InventoryDialog { get; set; }
		public virtual string inventoryId => "gear-" + EntityId;
		public virtual string weaponClass { get; set; }
		public override IInventory GearInventory => gearInv;
		public override ItemSlot LeftHandItemSlot => gearInv[15];
		public override ItemSlot RightHandItemSlot => gearInv[16];
		public virtual ItemSlot BackItemSlot => gearInv[17];
		public virtual ItemSlot AmmoItemSlot => gearInv[18];
		public virtual ItemSlot HealItemSlot => gearInv[19];

		public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d) {
			base.Initialize(properties, api, InChunkIndex3d);
			// Initialize gear slots if not done yet.
			if (gearInv is null) {
				gearInv = new InventorySentry(inventoryId, api);
				gearInv.SlotModified += GearInvSlotModified;
			} else {
				gearInv.LateInitialize(inventoryId, api);
			}
			// Register stuff for client-side api.
			if (api is ICoreClientAPI capi) {
				talkUtil = new EntityTalkUtil(capi, this);
			}
			// Register listeners if api is on server.
			if (api is ICoreServerAPI sapi) {
				WatchedAttributes.RegisterModifiedListener("inventory", ReadInventoryFromAttributes);
				GetBehavior<EntityBehaviorHealth>().onDamaged += (dmg, dmgSource) => HealthUtility.handleDamaged(World.Api, this, dmg, dmgSource);
			}
			// Set weapon class to distinguish behaviors.
			weaponClass = properties.Attributes["kingdomClass"].AsString().ToLowerInvariant();
			UpdateAllVariables();
			ReadInventoryFromAttributes();
		}

		public override void OnEntityDespawn(EntityDespawnData despawn) {
			InventoryDialog?.TryClose();
			base.OnEntityDespawn(despawn);
		}

		public override void OnTesselation(ref Shape entityShape, string shapePathForLogging) {
			base.OnTesselation(ref entityShape, shapePathForLogging);
			foreach (ItemSlot slot in GearInventory) {
				addGearToShape(slot, entityShape, shapePathForLogging);
			}
		}
		
		public override void OnReceivedClientPacket(IServerPlayer player, int packetid, byte[] data) {
			base.OnReceivedClientPacket(player, packetid, data);
			if (packetid == 1504) {
				player.InventoryManager.OpenInventory(GearInventory);
			}
			if (packetid == 1505) {
				player.InventoryManager.CloseInventory(GearInventory);
			}
		}

		public override void OnReceivedServerPacket(int packetid, byte[] data) {
			base.OnReceivedServerPacket(packetid, data);
			if (packetid == 1503) {
				TreeAttribute tree = new TreeAttribute();
				SerializerUtil.FromBytes(data, (r) => tree.FromBytes(r));
				gearInv.FromTreeAttributes(tree);
				foreach (var slot in gearInv) {
					slot.OnItemSlotModified(slot.Itemstack);
				}
			}
			if (packetid == 1505) {
				(World as IClientWorldAccessor).Player.InventoryManager.CloseInventory(GearInventory);
				InventoryDialog?.TryClose();
			}
		}

		public override bool ShouldReceiveDamage(DamageSource damageSource, float damage) {
			if (damageSource.CauseEntity is EntityHumanoid attacker) {
				if (damageSource.CauseEntity is EntityPlayer player && WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("leaders_guid") == player.PlayerUID) {
					return player.ServerControls.Sneak && base.ShouldReceiveDamage(damageSource, damage);
				}
				if (DataUtility.IsAnEnemy(WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid"), attacker)) {
					return base.ShouldReceiveDamage(damageSource, damage);
				}
				if (DataUtility.IsAFriend(WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid"), attacker)) {
					return Api.World.Config.GetAsBool("FriendlyFire") && base.ShouldReceiveDamage(damageSource, damage);
				}
			}
			return base.ShouldReceiveDamage(damageSource, damage);
		}

		public override void FromBytes(BinaryReader reader, bool forClient) {
			base.FromBytes(reader, forClient);
			if (gearInv is null) {
				gearInv = new InventorySentry(Code.Path, "gearInv-" + EntityId, null);
			}
			gearInv.FromTreeAttributes(GetInventoryTree());
		}

		public override void ToBytes(BinaryWriter writer, bool forClient) {
			// Save as much as possible, but ignore anything if it catches a null reference exception.
			try { gearInv.ToTreeAttributes(GetInventoryTree()); } catch (NullReferenceException) { }
			base.ToBytes(writer, forClient);
		}

		public override void StartAnimation(string code) {
			// Set the animation to sprint if walking but controls are set to sprint.
			if (code == "walk" && ServerControls.Sprint) {
				Api.Logger.Notification(GetName() + " is switching to sprinting mode.");
				AnimManager.StartAnimation("sprint");
			}
			if (code == "walk" && ServerControls.Sneak) {
				Api.Logger.Notification(GetName() + " is switching to sneaking mode.");
				AnimManager.StartAnimation("sneak");
			}
			base.StartAnimation(code);
		}

		public virtual ITreeAttribute GetInventoryTree() {
			if (!WatchedAttributes.HasAttribute("inventory")) {
				ITreeAttribute tree = new TreeAttribute();
				gearInv.ToTreeAttributes(tree);
				WatchedAttributes.SetAttribute("inventory", tree);
			}
			return WatchedAttributes.GetTreeAttribute("inventory");
		}

		public virtual void GearInvSlotModified(int slotId) {
			ITreeAttribute tree = new TreeAttribute();
			WatchedAttributes["inventory"] = tree;
			gearInv.ToTreeAttributes(tree);
			WatchedAttributes.MarkPathDirty("inventory");
			// If on server-side, not client, send the packetid on the channel.
			(Api as ICoreServerAPI)?.Network.BroadcastEntityPacket(EntityId, 1503, SerializerUtil.ToBytes((w) => tree.ToBytes(w)));
			UpdateAllVariables();
		}

		public virtual void ToggleInventoryDialog(IPlayer player) {
			if (Api.Side != EnumAppSide.Client) {
				return;
			} else {
				var capi = (Api as ICoreClientAPI);
				if (InventoryDialog is null) {
					InventoryDialog = new InvSentryDialog(gearInv, this, capi);
					InventoryDialog.OnClosed += OnInventoryDialogClosed;
				}
				if (!InventoryDialog.TryOpen()) {
					return;
				}
				player.InventoryManager.OpenInventory(GearInventory);
				capi.Network.SendEntityPacket(EntityId, 1504);
			}
		}

		public virtual void OnInventoryDialogClosed() {
			var capi = (Api as ICoreClientAPI);
			capi.World.Player.InventoryManager.CloseInventory(GearInventory);
			capi.Network.SendEntityPacket(EntityId, 1505);
			InventoryDialog?.Dispose();
			InventoryDialog = null;
		}

		public virtual void ReadInventoryFromAttributes() {
			ITreeAttribute treeAttribute = WatchedAttributes["inventory"] as ITreeAttribute;
			if (gearInv != null && treeAttribute != null) {
				gearInv.FromTreeAttributes(treeAttribute);
			}
			(Properties.Client.Renderer as EntitySkinnableShapeRenderer)?.MarkShapeModified();
		}

		public virtual void UpdateAllVariables() {
			try {
				var loyalties = GetBehavior<EntityBehaviorLoyalties>();
				if (RightHandItemSlot.Empty && LeftHandItemSlot.Empty) {
					loyalties.enlistedStatus = EnlistedStatus.CIVILIAN;
				} else {
					loyalties.enlistedStatus = EnlistedStatus.ENLISTED;
				}
			} catch (NullReferenceException e) {
				World.Logger.Error(e.ToString());
			}
		}
	}
}