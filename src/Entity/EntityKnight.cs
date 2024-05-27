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
	public class EntityKnight : EntityHumanoid {
		public EntityKnight() { }
		public InventoryKnight gearInv { get; set; }
		public EntityTalkUtil talkUtil { get; set; }
		public InvKnightDialog InventoryDialog { get; set; }
		public virtual string inventoryId => "gear-" + EntityId;
		public virtual string kingdomUID => WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID");
		public virtual string leadersUID => WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("leadersUID");
		public override IInventory GearInventory => gearInv;
		public override ItemSlot LeftHandItemSlot => gearInv[15];
		public override ItemSlot RightHandItemSlot => gearInv[16];
		public virtual ItemSlot BackItemSlot => gearInv[17];
		public virtual ItemSlot FoodItemSlot => gearInv[18];
		public virtual ItemSlot HealItemSlot => gearInv[19];
		
		public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d) {
			base.Initialize(properties, api, InChunkIndex3d);
			// Initialize gear slots if not done yet.
			if (gearInv is null) {
				gearInv = new InventoryKnight(inventoryId, api);
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
			// Opening and Closing inventory packets.
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
			if (damageSource.CauseEntity is not EntityHumanoid) {
				return base.ShouldReceiveDamage(damageSource, damage);
			}
			EntityHumanoid attacker = (EntityHumanoid)damageSource.CauseEntity;
			// Don't let the entity take damage from other players within the group if friendly fire for groups is turned off.
			if (kingdomUID == attacker.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID")) {
				// Don't let the entity take damage from its owner if friendly fire for owners is turned off. But do if they are sneaking.
				if (attacker is EntityPlayer playerAttacker && playerAttacker.PlayerUID == leadersUID && playerAttacker.ServerControls.Sneak) {
					return base.ShouldReceiveDamage(damageSource, damage);
				}
				if (Api.World.Config.GetAsBool("FriendlyFireG")) {
					return base.ShouldReceiveDamage(damageSource, damage);
				}
			}
			return base.ShouldReceiveDamage(damageSource, damage);
		}

		public override void FromBytes(BinaryReader reader, bool forClient) {
			base.FromBytes(reader, forClient);
			if (gearInv is null) {
				gearInv = new InventoryKnight(Code.Path, "gearInv-" + EntityId, null);
			}
			gearInv.FromTreeAttributes(GetInventoryTree());
		}

		public override void ToBytes(BinaryWriter writer, bool forClient) {
			// Save as much as possible, but ignore anything if it catches a null reference exception.
			try { gearInv.ToTreeAttributes(GetInventoryTree()); } catch (NullReferenceException) { }
			base.ToBytes(writer, forClient);
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
			// If on server-side, not client, sent the packetid on the channel.
			if (Api is ICoreServerAPI sapi) {
				sapi.Network.BroadcastEntityPacket(EntityId, 1503, SerializerUtil.ToBytes((w) => tree.ToBytes(w)));
			}
			UpdateAllVariables();
		}

		public virtual void ToggleInventoryDialog(IPlayer player) {
			if (Api.Side != EnumAppSide.Client) {
				return;
			} else {
				var capi = (ICoreClientAPI)Api;
				if (InventoryDialog is null) {
					InventoryDialog = new InvKnightDialog(gearInv, this, capi);
					InventoryDialog.OnClosed += OnInventoryDialogClosed;
				}
				if (!InventoryDialog.TryOpen()) {
					return;
				}
				player.InventoryManager.OpenInventory(GearInventory);
				capi.Network.SendEntityPacket(EntityId, 1504);
				return;
			}
		}

		public virtual void OnInventoryDialogClosed() {
			var capi = (ICoreClientAPI)Api;
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
				var aitasking = GetBehavior<EntityBehaviorTaskAI>();
				var loyalties = GetBehavior<EntityBehaviorLoyalties>();
				var traverser = GetBehavior<EntityBehaviorTraverser>();
				if (RightHandItemSlot.Empty && LeftHandItemSlot.Empty) {
					loyalties.enlistedStatus = EnlistedStatus.CIVILIAN;
				} else {
					World.Logger.Notification("What is it's current stuff?: " + aitasking?.TaskManager?.GetTask<AiTaskSoldierWanderAbout>()?.SpawnPosition);
					aitasking?.TaskManager?.GetTask<AiTaskSoldierRespawnPost>()?.SetBlockPost(loyalties.cachedOutpost);
					aitasking?.TaskManager?.GetTask<AiTaskSoldierReturningTo>()?.SetTraverser(traverser);
					aitasking?.TaskManager?.GetTask<AiTaskSoldierWanderAbout>()?.SetTraverser(traverser);
					loyalties.enlistedStatus = EnlistedStatus.ENLISTED;
				}
			} catch (NullReferenceException e) {
				World.Logger.Error(e.ToString());
			}
		}
	}
}