using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class EntitySentry : EntityHumanoid {
		public EntitySentry() { }
		public virtual bool[] ruleOrder { get; set; }
		public virtual double moveSpeed { get; set; }
		public virtual double walkSpeed { get; set; }
		public virtual string weapClass { get; set; }
		public virtual double weapRange { get; set; }
		public virtual double postRange { get; set; }
		public virtual string baseGroup { get; set; }
		public virtual string kingdomID { get; set; }
		public virtual string kingdomNM { get; set; }
		public virtual string cultureID { get; set; }
		public virtual string cultureNM { get; set; }
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
		public ICoreClientAPI ClientAPI;
		public ICoreServerAPI ServerAPI;

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
				ClientAPI = capi;
				talkUtil = new EntityTalkUtil(capi, this);
			}
			// Register listeners if api is on server.
			if (api is ICoreServerAPI sapi) {
				ServerAPI = sapi;
				WatchedAttributes.RegisterModifiedListener("inventory", ReadInventoryFromAttributes);
				GetBehavior<EntityBehaviorHealth>().onDamaged += (dmg, dmgSource) => HealthUtility.handleDamaged(World.Api, this, dmg, dmgSource);
			}
			if (Api.Side == EnumAppSide.Server) {
				moveSpeed = properties.Attributes["moveSpeed"].AsDouble(0.030);
				walkSpeed = properties.Attributes["walkSpeed"].AsDouble(0.015);
				weapClass = properties.Attributes["weapClass"].AsString("melee").ToLowerInvariant();
				baseGroup = properties.Attributes["baseGroup"].AsString("00000000");
				friendsID = new string[] { };
				enemiesID = new string[] { };
				outlawsID = new string[] { };
				ReadInventoryFromAttributes();
			}
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
			SendUpdates();
		}
		
		public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player) {
			try {
				if (!WatchedAttributes.HasAttribute("loyalties")) {
					return base.GetInteractionHelp(world, es, player);
				}
				if (Loyalties.HasAttribute("leaders_guid") && Loyalties.GetString("leaders_guid") == player.PlayerUID) {
					string[] actions = { "wander", "follow", "firing", "pursue", "shifts", "nights", "return" };
					string actionLangCode = $"{Lang.Get("vskingdom:entries-keyword-orders")}:\n";
					for (int i = 0; i < actions.Length; i++) {
						actionLangCode += $"{Lang.Get($"vskingdom:entries-keyword-{actions[i]}")} {new string(ruleOrder[i] ? "✔" : "✘")}\n";
					}
					return new WorldInteraction[] { new WorldInteraction() { ActionLangCode = actionLangCode, RequireFreeHand = true, MouseButton = EnumMouseButton.Right } };
				}
			} catch (NullReferenceException e) {
				World.Logger.Error(e);
			}
			return base.GetInteractionHelp(world, es, player);
		}

		public override string GetInfoText() {
			try {
				StringBuilder infotext = new StringBuilder();
				if (!Alive && WatchedAttributes.HasAttribute("deathByPlayer")) {
					infotext.AppendLine(Lang.Get("Killed by Player: {0}", WatchedAttributes.GetString("deathByPlayer")));
				}
				if (World.Side == EnumAppSide.Client) {
					IClientPlayer player = (World as IClientWorldAccessor).Player;
					if (player != null) {
						string playerKingdom = player.Entity.WatchedAttributes.GetTreeAttribute("loyalties").GetString("kingdom_guid");
						string colourKingdom = "#ffffff";
						if (kingdomID == playerKingdom) {
							colourKingdom = "#5CC5FF";
						} else if (enemiesID.Contains(playerKingdom)) {
							colourKingdom = "#ff2525";
						} else if (outlawsID.Contains(player.PlayerUID)) {
							colourKingdom = "#ff8625";
						} else if (kingdomID == "xxxxxxxx") {
							colourKingdom = "#ff9300";
						}
						if (player.WorldData?.CurrentGameMode == EnumGameMode.Creative) {
							ITreeAttribute healAttribute = WatchedAttributes.GetTreeAttribute("health");
							if (healAttribute != null) {
								infotext.AppendLine($"<font color=\"#ff8888\">Health: {healAttribute.GetFloat("currenthealth")}/{healAttribute.GetFloat("maxhealth")}</font>");
							}
							infotext.AppendLine("<font color=\"#bbbbbb\">Guid: " + EntityId + "</font>");
							if (kingdomID != null) {
								infotext.AppendLine($"<font color=\"{colourKingdom}\">{LangUtility.Get("entries-keyword-kingdom")}: {kingdomID}</font>");
							}
							if (cultureID != null) {
								infotext.AppendLine($"{LangUtility.Get("entries-keyword-culture")}: {cultureID}");
							}
							if (leadersID != null) {
								infotext.AppendLine($"{LangUtility.Get("entries-keyword-leaders")}: {leadersID}");
							}
						} else if (player != null && player.WorldData?.CurrentGameMode == EnumGameMode.Survival) {
							ITreeAttribute nameAttribute = WatchedAttributes.GetTreeAttribute("nametag");
							if (nameAttribute != null) {
								if (nameAttribute.HasAttribute("name")) {
									infotext.AppendLine($"<font color=\"#bbbbbb\">Name: {nameAttribute.GetString("name")}</font>");
								}
								if (nameAttribute.HasAttribute("last")) {
									infotext.AppendLine($"<font color=\"#bbbbbb\">Last: {nameAttribute.GetString("last")}</font>");
								}
							}
							if (kingdomNM != null) {
								infotext.AppendLine($"<font color=\"{colourKingdom}\">{LangUtility.Get("entries-keyword-kingdom")}: {kingdomNM}</font>");
							}
							if (cultureNM != null) {
								infotext.AppendLine($"{LangUtility.Get("entries-keyword-culture")}: {cultureNM}");
							}
							if (leadersID != null) {
								infotext.AppendLine($"{LangUtility.Get("entries-keyword-leaders")}: {ServerAPI?.World.PlayerByUid(leadersID).PlayerName}");
							}
						}
					}
				}
				if (WatchedAttributes.HasAttribute("extraInfoText")) {
					foreach (KeyValuePair<string, IAttribute> item in WatchedAttributes.GetTreeAttribute("extraInfoText")) {
						infotext.AppendLine(item.Value.ToString());
					}
				}
				if (Api is ICoreClientAPI coreClientAPI && coreClientAPI.Settings.Bool["extendedDebugInfo"]) {
					infotext.AppendLine($"<font color=\"#bbbbbb\">Code: {Code?.ToString()}</font>");
				}
				return infotext.ToString();
			} catch {
				return base.GetInfoText();
			}
		}

		public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode) {
			base.OnInteract(byEntity, itemslot, hitPosition, mode);
			if (mode != EnumInteractMode.Interact || byEntity is not EntityPlayer) {
				return;
			}
			EntityPlayer player = byEntity as EntityPlayer;
			string theirKingdom = player.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid");
			// Remind them to join their leaders kingdom if they aren't already in it.
			if (leadersID == player.PlayerUID && kingdomID != theirKingdom) {
				ServerAPI?.World.GetEntityById(EntityId).WatchedAttributes.GetTreeAttribute("loyalties").SetString("kingdom_guid", theirKingdom);
				ServerAPI?.World.GetEntityById(EntityId).WatchedAttributes.MarkPathDirty("loyalties");
			}
			if (leadersID == player.PlayerUID && player.ServerControls.Sneak && itemslot.Empty) {
				ToggleInventoryDialog(player.Player);
				return;
			}
			if (!itemslot.Empty && player.ServerControls.Sneak) {
				// TRY TO REVIVE!
				if (!Alive && itemslot.Itemstack.Item is ItemPoultice) {
					Revive();
					ServerAPI?.World.GetEntityById(EntityId).WatchedAttributes.GetTreeAttribute("health").SetFloat("currenthealth", itemslot.Itemstack.ItemAttributes["health"].AsFloat());
					itemslot.TakeOut(1);
					itemslot.MarkDirty();
					return;
				}
				// TRY TO EQUIP!
				if (Alive && leadersID == player.PlayerUID && itemslot.Itemstack.Item is ItemWearable wearable) {
					itemslot.TryPutInto(World, gearInv[(int)wearable.DressType]);
					return;
				}
				if (Alive && leadersID == player.PlayerUID && itemslot.Itemstack.Collectible.Tool != null || itemslot.Itemstack.ItemAttributes?["toolrackTransform"].Exists == true) {
					if (player.RightHandItemSlot.TryPutInto(byEntity.World, RightHandItemSlot) == 0) {
						player.RightHandItemSlot.TryPutInto(byEntity.World, LeftHandItemSlot);
						return;
					}
				}
				// TRY TO RECRUIT!
				if (Alive && leadersID == null && itemslot.Itemstack.ItemAttributes["currency"].Exists) {
					ServerAPI?.World.GetEntityById(EntityId).WatchedAttributes.GetTreeAttribute("loyalties").SetString("enlistedStatus", EnlistedStatus.ENLISTED.ToString());
					ServerAPI?.World.GetEntityById(EntityId).WatchedAttributes.GetTreeAttribute("loyalties").SetString("leaders_guid", player.PlayerUID);
					ServerAPI?.World.GetEntityById(EntityId).WatchedAttributes.GetTreeAttribute("loyalties").SetString("kingdom_guid", theirKingdom);
					ServerAPI?.World.GetEntityById(EntityId).WatchedAttributes.MarkPathDirty("loyalties");
					itemslot.TakeOut(1);
					itemslot.MarkDirty();
					return;
				}
				// TRY TO RALLY!
				if (Alive && kingdomID == theirKingdom && itemslot.Itemstack.Item is ItemBanner) {
					if (!player.WatchedAttributes.HasAttribute("followerEntityUids")) {
						ServerAPI?.World.PlayerByUid(player.PlayerUID).Entity.WatchedAttributes.SetAttribute("followerEntityUids", new LongArrayAttribute(new long[] { }));
					}
					var followed = (player.WatchedAttributes.GetAttribute("followerEntityUids") as LongArrayAttribute)?.value?.ToList<long>();
					var sentries = World.GetEntitiesAround(ServerPos.XYZ, 16f, 8f, entity => (entity is EntitySentry sentry && sentry.kingdomID == theirKingdom));
					foreach (EntitySentry sentry in sentries) {
						sentry.WatchedAttributes.SetLong("guardedEntityId", player.EntityId);
						sentry.ruleOrder[1] = true;
						sentry.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager.GetTask<AiTaskSentryFollow>()?.StartExecute();
						if (!followed.Contains(sentry.EntityId)) {
							followed.Add(sentry.EntityId);
						}
						ServerAPI?.Network.GetChannel("sentrynetwork").SendPacket<SentryUpdate>(new SentryUpdate() { entityUID = sentry.EntityId, kingdomID = sentry.kingdomID, cultureID = sentry.cultureID }, player.Player as IServerPlayer);
					}
					player.WatchedAttributes.SetAttribute("followerEntityUids", new LongArrayAttribute(followed.ToArray<long>()));
					return;
				}
			}
			base.OnInteract(byEntity, itemslot, hitPosition, mode);
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
			switch (packetid) {
				case 1505:
					player.InventoryManager.OpenInventory(GearInventory);
					return;
				case 1506:
					player.InventoryManager.CloseInventory(GearInventory);
					return;
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
			if (Api is ICoreServerAPI sapi) {
				sapi.Network.BroadcastEntityPacket(EntityId, 1504, SerializerUtil.ToBytes((w) => tree.ToBytes(w)));
				UpdateStats();
			}
		}

		public virtual void ToggleInventoryDialog(IPlayer player) {
			if (Api.Side != EnumAppSide.Client) {
				return;
			} else {
				if (InventoryDialog is null) {
					//InventoryDialog = new InvSentryDialog(gearInv, this, ClientAPI)??? Trying to do serverside, maybe remove the Api.Side requirement above this?
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
		}

		public virtual void ReadInventoryFromAttributes() {
			ITreeAttribute treeAttribute = WatchedAttributes["inventory"] as ITreeAttribute;
			if (gearInv != null && treeAttribute != null) {
				gearInv.FromTreeAttributes(treeAttribute);
			}
			(Properties.Client.Renderer as EntitySkinnableShapeRenderer)?.MarkShapeModified();
		}
		
		public virtual void SendUpdates() {
			SentryUpdate update = new SentryUpdate() { entityUID = this.EntityId, kingdomID = this.kingdomID, cultureID = this.cultureID };
			(Api as ICoreServerAPI)?.Network.GetChannel("sentrynetwork").SendPacket<SentryUpdate>(update, World.NearestPlayer(ServerPos.X, ServerPos.Y, ServerPos.Z) as IServerPlayer);
		}

		public virtual void UpdateStats() {
			try {
				Loyalties.SetString("recruit_type", new string((RightHandItemSlot.Empty && LeftHandItemSlot.Empty) ? EnlistedStatus.CIVILIAN.ToString() : EnlistedStatus.ENLISTED.ToString()));
				WatchedAttributes.MarkPathDirty("loyalties");
				EntitySentry thisEnt = (Api as ICoreServerAPI)?.World.GetEntityById(this.EntityId) as EntitySentry;
				if (!RightHandItemSlot.Empty) {
					// Set weapon class, movement speed, and ranges to distinguish behaviors.
					thisEnt.weapRange = (double)(RightHandItemSlot.Itemstack.Collectible.AttackRange);
				}
				float massTotal = 0;
				foreach (var slot in gearInv) {
					if (!slot.Empty && slot.Itemstack.Item is ItemWearable armor) {
						massTotal += (armor.StatModifers?.walkSpeed ?? 0);
					}
				}
				thisEnt.moveSpeed = Properties.Attributes["moveSpeed"].AsDouble(0.030) * (1 + massTotal);
				thisEnt.walkSpeed = Properties.Attributes["walkSpeed"].AsDouble(0.015) * (1 + massTotal);
				thisEnt.postRange = Loyalties.GetDouble("outpost_size", 6.0);
			} catch (NullReferenceException e) {
				World.Logger.Error(e.ToString());
			}
		}

		public virtual void UpdateInfos(byte[] data) {
			SentryUpdate update = SerializerUtil.Deserialize<SentryUpdate>(data);
			kingdomID = update.kingdomID ?? Loyalties?.GetString("kingdom_guid") ?? baseGroup ?? "00000000";
			kingdomNM = update.kingdomNM;
			cultureID = update.cultureID ?? Loyalties?.GetString("culture_guid") ?? "00000000";
			cultureNM = update.cultureNM;
			leadersID = Loyalties?.GetString("leaders_guid") ?? null;
			friendsID = update.friendsID;
			enemiesID = update.enemiesID;
			outlawsID = update.outlawsID;
		}

		public virtual void UpdateTasks(byte[] data) {
			// Does nothing right now.
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