using System;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using VSKingdom.Utilities;

namespace VSKingdom {
	public class EntityZombie : EntityHumanoid {
		public EntityZombie() { }
		public virtual bool zombified { get; set; }
		public virtual bool canRevive { get; set; }
		public virtual string inventory => "gear-" + EntityId;
		public virtual InventorySentry gearInv { get; set; }
		public virtual InvSentryDialog gearDialog { get; set; }
		public override ItemSlot LeftHandItemSlot => gearInv[15];
		public override ItemSlot RightHandItemSlot => gearInv[16];
		public virtual ItemSlot BackItemSlot => gearInv[17];
		public virtual ItemSlot AmmoItemSlot => gearInv[18];
		public virtual ItemSlot HealItemSlot => gearInv[19];
		public override IInventory GearInventory => gearInv;
		public override bool StoreWithChunk => true;
		public override bool AlwaysActive => false;
		public override double LadderFixDelta { get => Properties.SpawnCollisionBox.Y2 - SelectionBox.YSize; }

		public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d) {
			base.Initialize(properties, api, InChunkIndex3d);
			if (gearInv is null) {
				gearInv = new InventorySentry(inventory, api);
				gearInv.SlotModified += GearInvSlotModified;
			} else {
				gearInv.LateInitialize(inventory, api);
			}
			if (api is ICoreServerAPI sapi) {
				WatchedAttributes.RegisterModifiedListener("inventory", ReadInventoryFromAttributes);
				GetBehavior<EntityBehaviorHealth>().onDamaged += (dmg, dmgSource) => DamagesUtil.HandleDamaged(World.Api, this, dmg, dmgSource);
				ReadInventoryFromAttributes();
			}
		}

		public override void OnEntitySpawn() {
			base.OnEntitySpawn();
			if (Api.Side != EnumAppSide.Server) { return; }
			if (zombified) { return; }
			for (int i = 0; i < GearsDressCodes.Length; i++) {
				string spawnCode = GearsDressCodes[i] + "Spawn";
				if (Properties.Attributes[spawnCode].Exists) {
					try {
						var _items = Api.World.GetItem(new AssetLocation(GenericUtil.GetRandom(Properties.Attributes[spawnCode].AsArray<string>(null))));
						var _stack = new ItemStack(_items, 1);
						int _durab = (int)_stack.Attributes.GetDecimal("durability") / 2;
						_items.Durability = Api.World.Rand.Next(1, _durab);
						if (!TryGiveItemStack(_stack)) {
							var newstack = Api.World.SpawnItemEntity(_stack, ServerPos.XYZ) as EntityItem;
							GearInventory[i].Itemstack = newstack?.Itemstack;
							newstack.Die(EnumDespawnReason.PickedUp, null);
						}
						GearInvSlotModified(i);
					} catch { }
				}
			}
		}

		public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode) {
			base.OnInteract(byEntity, itemslot, hitPosition, mode);
			if (mode != EnumInteractMode.Interact) {
				return;
			}
			if (byEntity.Controls.RightMouseDown && byEntity is EntityPlayer player && !Alive && World.Config.GetAsBool(CanLootNpcs)) {
				ToggleInventoryDialog(player.Player);
			}
		}

		public override void OnEntityDespawn(EntityDespawnData despawn) {
			gearDialog?.TryClose();
			base.OnEntityDespawn(despawn);
		}

		public override void OnTesselation(ref Shape entityShape, string shapePathForLogging) {
			base.OnTesselation(ref entityShape, shapePathForLogging);
			for (int i = 0; i < GearInventory.Count; i++) {
				addGearToShape(GearInventory[i], entityShape, shapePathForLogging);
			}
		}

		public override void OnReceivedClientPacket(IServerPlayer player, int packetid, byte[] data) {
			base.OnReceivedClientPacket(player, packetid, data);
			switch (packetid) {
				case 1505:
					player.InventoryManager.OpenInventory(GearInventory);
					return;
				case 1506:
					player.InventoryManager.CloseInventory(GearInventory);
					Skeletalize();
					return;
			}
		}

		public override void OnReceivedServerPacket(int packetid, byte[] data) {
			base.OnReceivedServerPacket(packetid, data);
			switch (packetid) {
				case 1504:
					UpdateTrees(data);
					return;
				case 1506:
					(World as IClientWorldAccessor).Player.InventoryManager.CloseInventory(GearInventory);
					gearDialog?.TryClose();
					return;
			}
		}

		public override void FromBytes(BinaryReader reader, bool forClient) {
			base.FromBytes(reader, forClient);
			if (gearInv is null) {
				gearInv = new InventorySentry(Code.Path, "gearInv-" + EntityId, null);
			}
			gearInv.FromTreeAttributes(GetInventoryTree());
		}

		public override void ToBytes(BinaryWriter writer, bool forClient) {
			base.ToBytes(writer, forClient);
			try { gearInv.ToTreeAttributes(GetInventoryTree()); } catch (NullReferenceException) { }
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
			if (Api is ICoreServerAPI sapi) {
				sapi.Network.BroadcastEntityPacket(EntityId, 1504, SerializerUtil.ToBytes((w) => tree.ToBytes(w)));
			}
		}

		public virtual void ToggleInventoryDialog(IPlayer player) {
			if (Api.Side != EnumAppSide.Client) { return; }
			if (gearDialog is null) {
				gearDialog = new InvSentryDialog(gearInv, this, Api as ICoreClientAPI);
				gearDialog.OnClosed += OnInventoryDialogClosed;
			}
			if (!gearDialog.TryOpen()) {
				return;
			}
			player.InventoryManager.OpenInventory(GearInventory);
		}

		public virtual void OnInventoryDialogClosed() {
			(Api as ICoreClientAPI)?.World.Player.InventoryManager.CloseInventory(GearInventory);
			(Api as ICoreClientAPI)?.Network.SendEntityPacket(EntityId, 1506);
			gearDialog?.Dispose();
			gearDialog = null;
		}

		public virtual void ReadInventoryFromAttributes() {
			ITreeAttribute treeAttribute = WatchedAttributes["inventory"] as ITreeAttribute;
			if (gearInv != null && treeAttribute != null) {
				gearInv.FromTreeAttributes(treeAttribute);
			}
			(Properties.Client.Renderer as EntitySkinnableShapeRenderer)?.MarkShapeModified();
		}

		public virtual void UpdateTrees(byte[] data) {
			TreeAttribute tree = new TreeAttribute();
			SerializerUtil.FromBytes(data, (r) => tree.FromBytes(r));
			gearInv.FromTreeAttributes(tree);
			foreach (var slot in gearInv) {
				slot.OnItemSlotModified(slot.Itemstack);
			}
		}

		public virtual void Skeletalize() {
			if (Api.Side != EnumAppSide.Client && !Alive && gearInv.Empty && HasBehavior<EntityBehaviorDecayBody>()) {
				GetBehavior<EntityBehaviorDecayBody>()?.DecayNow(this);
			}
		}
	}
}