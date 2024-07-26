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
		public virtual bool[] ruleOrder { get; set; }
		public virtual double moveSpeed { get; set; }
		public virtual double walkSpeed { get; set; }
		public virtual double massTotal { get; set; }
		public virtual string weapClass { get; set; }
		public virtual double weapSkill { get; set; }
		public virtual double weapValue { get; set; }
		public virtual double weapRates { get; set; }
		public virtual double weapRange { get; set; }
		public virtual double postRange { get; set; }
		public virtual string baseGroup { get; set; }
		public virtual string kingdomID { get; set; }
		public virtual string cultureID { get; set; }
		public virtual string leadersID { get; set; }
		public virtual string[] friendsID { get; set; } = new string[] { };
		public virtual string[] enemiesID { get; set; } = new string[] { };
		public virtual string[] outlawsID { get; set; } = new string[] { };
		public virtual string inventory => "gear-" + EntityId;
		public virtual EntityTalkUtil talkUtil { get; set; }
		public virtual InventorySentry gearInv { get; set; }
		public virtual InvSentryDialog InventoryDialog { get; set; }
		public override ItemSlot LeftHandItemSlot => gearInv[15];
		public override ItemSlot RightHandItemSlot => gearInv[16];
		public virtual ItemSlot BackItemSlot => gearInv[17];
		public virtual ItemSlot AmmoItemSlot => gearInv[18];
		public virtual ItemSlot HealItemSlot => gearInv[19];
		public override IInventory GearInventory => gearInv;
		public ITreeAttribute Loyalties;
		public ICoreClientAPI ClientAPI => (Api as ICoreClientAPI);
		public ICoreServerAPI ServerAPI => (Api as ICoreServerAPI);

		public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d) {
			base.Initialize(properties, api, InChunkIndex3d);
			// Initialize gear slots if not done yet.
			if (gearInv is null) {
				gearInv = new InventorySentry(inventory, api);
				gearInv.SlotModified += GearInvSlotModified;
			} else {
				gearInv.LateInitialize(inventory, api);
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
			Loyalties = WatchedAttributes.GetOrAddTreeAttribute("loyalties");
			ruleOrder = new bool[7] { true, false, true, true, false, false, false, };
			moveSpeed = properties.Attributes["moveSpeed"].AsDouble(0.030);
			walkSpeed = properties.Attributes["walkSpeed"].AsDouble(0.015);
			weapClass = properties.Attributes["weapClass"].AsString("melee").ToLowerInvariant();
			baseGroup = properties.Attributes["baseGroup"].AsString("00000000");
			kingdomID = Loyalties.GetString("kingdom_guid") ?? properties.Attributes["baseGroup"].AsString("00000000");
			cultureID = Loyalties.GetString("culture_guid") ?? "00000000";
			leadersID = Loyalties.GetString("leaders_guid", null);
			friendsID = new string[] { };
			enemiesID = new string[] { };
			outlawsID = new string[] { };
			if (baseGroup == "xxxxxxxx") {
				Loyalties.SetString("kingdom_guid", "xxxxxxxx");
				Loyalties.SetString("leaders_guid", null);
				WatchedAttributes.MarkPathDirty("loyalties");
				kingdomID = "xxxxxxxx";
				leadersID = null;
			}
			ReadInventoryFromAttributes();
		}

		public override void OnEntitySpawn() {
			base.OnEntitySpawn();
			// Consult EnumCharacterDressType for details on which is which.
			string[] dressCodes = { "head", "tops", "gear", "pant", "shoe", "hand", "neck", "icon", "mask", "belt", "arms", "coat", "helm", "body", "legs", "left", "weap", "back", "ammo", "heal" };
			for (int i = 0; i < dressCodes.Length; i++) {
				string code = dressCodes[i] + "Spawn";
				if (Properties.Attributes[code].Exists) {
					try {
						var item = World.GetItem(new AssetLocation(MathUtility.GetRandom(Properties.Attributes[code].AsArray<string>(null))));
						ItemStack itemstack = new ItemStack(item, 1);
						if (i == 18 && GearInventory[16].Itemstack.Item is ItemBow) {
							itemstack = new ItemStack(item, MathUtility.GetRandom(item.MaxStackSize, 5));
						}
						var newstack = World.SpawnItemEntity(itemstack, this.ServerPos.XYZ) as EntityItem;
						GearInventory[i].Itemstack = newstack?.Itemstack;
						newstack.Die(EnumDespawnReason.PickedUp, null);
						GearInvSlotModified(i);
					} catch { }
				}
			}
			SentryUpdate update = new SentryUpdate() { entityUID = this.EntityId, kingdomID = this.kingdomID };
			(Api as ICoreServerAPI)?.Network.GetChannel("sentrynetwork").SendPacket<SentryUpdate>(update, World.NearestPlayer(ServerPos.X, ServerPos.Y, ServerPos.Z) as IServerPlayer);
			UpdateStats();
			/**UpdateTasks(null);**/
			ruleOrder = new bool[] {
				Loyalties.GetBool("command_wander", true),
				Loyalties.GetBool("command_follow", false),
				Loyalties.GetBool("command_firing", true),
				Loyalties.GetBool("command_pursue", true),
				Loyalties.GetBool("command_shifts", false),
				Loyalties.GetBool("command_nights", false),
				Loyalties.GetBool("command_return", false)
			};
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
			if (packetid == 1505) {
				player.InventoryManager.OpenInventory(GearInventory);
			}
			if (packetid == 1506) {
				player.InventoryManager.CloseInventory(GearInventory);
			}
		}

		public override void OnReceivedServerPacket(int packetid, byte[] data) {
			base.OnReceivedServerPacket(packetid, data);
			switch (packetid) {
				case 1501:
					UpdateStats();
					return;
				case 1502:
					UpdateInfos(data);
					return;
				case 1503:
					UpdateTasks(data);
					return;
				case 1504:
					UpdateTrees(data);
					return;
				case 1506:
					(World as IClientWorldAccessor).Player.InventoryManager.CloseInventory(GearInventory);
					InventoryDialog?.TryClose();
					return;
			}
		}

		public override bool ShouldReceiveDamage(DamageSource damageSource, float damage) {
			if (damageSource.GetCauseEntity() is EntityHumanoid attacker) {
				if (attacker is EntityPlayer player && leadersID == player.PlayerUID) {
					return player.ServerControls.Sneak && base.ShouldReceiveDamage(damageSource, damage);
				}
				if (kingdomID == attacker.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") && ServerAPI.World.Config.GetAsBool("FriendlyFire", true)) {
					return base.ShouldReceiveDamage(damageSource, damage);
				}
				return base.ShouldReceiveDamage(damageSource, damage);
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

		public override void Revive() {
			this.Alive = true;
			ReceiveDamage(new DamageSource { SourceEntity = this, CauseEntity = this, Source = EnumDamageSource.Revive, Type = EnumDamageType.Heal }, 9999f);
			AnimManager.StopAnimation("dies");
			IsOnFire = false;
			foreach (EntityBehavior behavior in SidedProperties.Behaviors) {
				behavior.OnEntityRevive();
			}
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
			(Api as ICoreServerAPI)?.Network.BroadcastEntityPacket(EntityId, 1504, SerializerUtil.ToBytes((w) => tree.ToBytes(w)));
			UpdateStats();
		}

		public virtual void ToggleInventoryDialog(IPlayer player) {
			if (Api.Side != EnumAppSide.Client) {
				return;
			} else {
				if (InventoryDialog is null) {
					InventoryDialog = new InvSentryDialog(gearInv, this, ClientAPI);
					InventoryDialog.OnClosed += OnInventoryDialogClosed;
				}
				if (!InventoryDialog.TryOpen()) {
					return;
				}
				player.InventoryManager.OpenInventory(GearInventory);
				ClientAPI.Network.SendEntityPacket(EntityId, 1505);
			}
		}

		public virtual void OnInventoryDialogClosed() {
			ClientAPI.World.Player.InventoryManager.CloseInventory(GearInventory);
			ClientAPI.Network.SendEntityPacket(EntityId, 1506);
			InventoryDialog?.Dispose();
			InventoryDialog = null;
			UpdateStats();
		}

		public virtual void ReadInventoryFromAttributes() {
			ITreeAttribute treeAttribute = WatchedAttributes["inventory"] as ITreeAttribute;
			if (gearInv != null && treeAttribute != null) {
				gearInv.FromTreeAttributes(treeAttribute);
			}
			(Properties.Client.Renderer as EntitySkinnableShapeRenderer)?.MarkShapeModified();
		}

		public virtual void UpdateStats() {
			try {
				if (RightHandItemSlot.Empty && LeftHandItemSlot.Empty) {
					GetBehavior<EntityBehaviorLoyalties>().enlistedStatus = EnlistedStatus.CIVILIAN;
				} else {
					GetBehavior<EntityBehaviorLoyalties>().enlistedStatus = EnlistedStatus.ENLISTED;
				}
				EntitySentry thisEnt = (Api as ICoreServerAPI)?.World.GetEntityById(this.EntityId) as EntitySentry;
				ItemStack weapon = RightHandItemSlot?.Itemstack ?? null;
				// Set weapon class, movement speed, and ranges to distinguish behaviors.
				thisEnt.weapValue = (double)((weapon?.Collectible?.Durability ?? 1f) * (weapon?.Collectible.AttackPower ?? weapon?.Collectible.Attributes?["damage"].AsFloat() ?? 1f));
				thisEnt.weapRange = RightHandItemSlot?.Itemstack?.Item?.AttackRange ?? 1.5;
				thisEnt.massTotal = 0;
				thisEnt.weapSkill = 1;
				thisEnt.weapRates = 1;
				foreach (var slot in gearInv) {
					if (!slot.Empty && slot.Itemstack.Item is ItemWearable armor) {
						thisEnt.massTotal += (armor?.StatModifers?.walkSpeed ?? 0);
						thisEnt.weapSkill += (armor?.StatModifers?.rangedWeaponsAcc ?? 0);
						thisEnt.weapRates += (armor?.StatModifers?.rangedWeaponsSpeed ?? 0);
					}
				}
				thisEnt.moveSpeed = Properties.Attributes["moveSpeed"].AsDouble(0.030) + massTotal;
				thisEnt.walkSpeed = Properties.Attributes["walkSpeed"].AsDouble(0.015) + massTotal;
				thisEnt.postRange = GetBehavior<EntityBehaviorLoyalties>()?.outpostSIZE ?? 6.0;
			} catch (NullReferenceException e) {
				World.Logger.Error(e.ToString());
			}
		}

		public virtual void UpdateInfos(byte[] data) {
			SentryUpdate update = SerializerUtil.Deserialize<SentryUpdate>(data);
			kingdomID = Loyalties?.GetString("kingdom_guid") ?? baseGroup ?? "00000000";
			cultureID = Loyalties?.GetString("culture_guid") ?? "00000000";
			leadersID = Loyalties?.GetString("leaders_guid") ?? null;
			friendsID = update.friendsID;
			enemiesID = update.enemiesID;
			outlawsID = update.outlawsID;
		}

		public virtual void UpdateTasks(byte[] data) {
			/**WatchedAttributes.MarkPathDirty("loyalties");
			if (data != null) {
				SentryOrders orders = SerializerUtil.Deserialize<SentryOrders>(data);
				ruleOrder = new bool[] {
					orders.wandering ?? Loyalties.GetBool("command_wander", true),
					orders.following ?? Loyalties.GetBool("command_follow", false),
					orders.attacking ?? Loyalties.GetBool("command_firing", true),
					orders.pursueing ?? Loyalties.GetBool("command_pursue", true),
					orders.shifttime ?? Loyalties.GetBool("command_shifts", false),
					orders.nighttime ?? Loyalties.GetBool("command_nights", false),
					orders.returning ?? Loyalties.GetBool("command_return", false)
				};
			} else {
				ruleOrder = new bool[] {
					Loyalties.GetBool("command_wander", true),
					Loyalties.GetBool("command_follow", false),
					Loyalties.GetBool("command_firing", true),
					Loyalties.GetBool("command_pursue", true),
					Loyalties.GetBool("command_shifts", false),
					Loyalties.GetBool("command_nights", false),
					Loyalties.GetBool("command_return", false)
				};
			}**/
		}

		public virtual void UpdateTrees(byte[] data) {
			TreeAttribute tree = new TreeAttribute();
			SerializerUtil.FromBytes(data, (r) => tree.FromBytes(r));
			gearInv.FromTreeAttributes(tree);
			foreach (var slot in gearInv) {
				slot.OnItemSlotModified(slot.Itemstack);
			}
		}
	}
}