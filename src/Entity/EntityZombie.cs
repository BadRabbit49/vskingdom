using System;
using System.IO;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.Essentials;
using static VSKingdom.Utilities.DamagesUtil;

namespace VSKingdom {
	public class EntityZombie : EntityHumanoid {
		public EntityZombie() { }
		public virtual bool zombified { get; set; }
		public virtual bool  canRevive { get; set; }
		public virtual float walkSpeed { get; set; }
		public virtual float moveSpeed { get; set; }
		public virtual string inventory => "gear-" + EntityId;
		public virtual InventorySentry gearInv { get; set; }
		public virtual InvSentryDialog gearDialog { get; set; }
		public virtual WaypointsTraverser pathfinder { get; set; }
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
			walkSpeed = properties.Attributes["walkSpeed"].AsFloat(0.02f);
			moveSpeed = properties.Attributes["moveSpeed"].AsFloat(0.04f);
			if (api is ICoreServerAPI sapi) {
				WatchedAttributes.RegisterModifiedListener("inventory", ReadInventoryFromAttributes);
				var taskBehaviors = GetBehavior<EntityBehaviorTaskAI>();
				var pathTraverser = new WaypointsTraverser(this);
				taskBehaviors.PathTraverser = pathTraverser;
				taskBehaviors.TaskManager.AllTasks.ForEach(task => typeof(AiTaskBase)
					.GetField("pathTraverser", BindingFlags.Instance | BindingFlags.NonPublic)
					.SetValue(task, pathTraverser));
				this.pathfinder = pathTraverser;
				GetBehavior<EntityBehaviorHealth>().onDamaged += (dmg, dmgSource) => HandleDamaged(World.Api, this, dmg, dmgSource);
				ReadInventoryFromAttributes();
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
			if (GearInventory.Empty) {
				return;
			}
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