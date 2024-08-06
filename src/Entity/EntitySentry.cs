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
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class EntitySentry : EntityHumanoid {
		public EntitySentry() { }
		public virtual bool[] ruleOrder { get; set; }
		public virtual string inventory => "gear-" + EntityId;
		public virtual EntityTalkUtil talkUtil { get; set; }
		public virtual SentryDataCache cachedData { get; set; }
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
				//cachedData = new SentryDataCache();
			}
			if (Api.Side == EnumAppSide.Server) {
				// Wander:0 / Follow:1 / Engage:2 / Pursue:3 / Shifts:4 / Patrol:5 / Return:6 //
				ruleOrder = new bool[7] { true, false, false, true, false, false, false };
				ReadInventoryFromAttributes();
			}
			cachedData = new SentryDataCache() {
				moveSpeed = properties.Attributes["moveSpeed"].AsFloat(0.030f),
				walkSpeed = properties.Attributes["walkSpeed"].AsFloat(0.015f),
				postRange = properties.Attributes["postRange"].AsFloat(6.0f),
				weapRange = properties.Attributes["weapRange"].AsFloat(1.5f),
				idleAnims = properties.Attributes["idleAnims"].AsString("idle").ToLower(),
				walkAnims = properties.Attributes["walkAnims"].AsString("walk").ToLower(),
				moveAnims = properties.Attributes["moveAnims"].AsString("move").ToLower(),
				duckAnims = properties.Attributes["duckAnims"].AsString("duck").ToLower(),
				swimAnims = properties.Attributes["swimAnims"].AsString("swim").ToLower(),
				jumpAnims = properties.Attributes["jumpAnims"].AsString("jump").ToLower(),
				diesAnims = properties.Attributes["diesAnims"].AsString("dies").ToLower(),
				kingdomGUID = Loyalties?.GetString("kingdom_guid") ?? properties.Attributes["baseSides"].AsString(GlobalCodes.commonerGUID),
				cultureGUID = Loyalties?.GetString("culture_guid") ?? properties.Attributes["baseGroup"].AsString(GlobalCodes.seraphimGUID),
				leadersGUID = Loyalties?.GetString("leaders_guid") ?? null,
				leadersNAME = api.World.PlayerByUid(Loyalties?.GetString("leaders_guid"))?.PlayerName ?? null,
				recruitNAME = WatchedAttributes.GetTreeAttribute("nametag")?.GetString("full"),
				recruitINFO = new string[] {
					properties.Attributes["baseClass"].AsString("melee").ToLower(),
					properties.Attributes["baseState"].AsString("CIVILIAN")
				},
				defaultINFO = new string[] {
					properties.Attributes["baseSides"].AsString(GlobalCodes.commonerGUID),
					properties.Attributes["baseGroup"].AsString(GlobalCodes.seraphimGUID)
				},
				coloursLIST = new string[] { "#ffffff", "#ffffff", "#ffffff" },
				enemiesLIST = new string[] { null },
				friendsLIST = new string[] { null },
				outlawsLIST = new string[] { null }
			};
		}

		public override void OnEntitySpawn() {
			base.OnEntitySpawn();
			for (int i = 0; i < GlobalCodes.dressCodes.Length; i++) {
				string code = GlobalCodes.dressCodes[i] + "Spawn";
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

		public override string GetInfoText() {
			if (cachedData == null) {
				return base.GetInfoText();
			}
			try {
				StringBuilder infotext = new StringBuilder();
				if (!Alive && WatchedAttributes.HasAttribute("deathByPlayer")) {
					infotext.AppendLine(Lang.Get("Killed by Player: {0}", WatchedAttributes.GetString("deathByPlayer")));
				}
				if (World.Side == EnumAppSide.Client) {
					IClientPlayer player = (World as IClientWorldAccessor).Player;
					if (player != null) {
						string playerKingdom = player.Entity.WatchedAttributes.GetTreeAttribute("loyalties").GetString("kingdom_guid");
						string colourKingdom = cachedData.coloursLIST[2] ?? "#ffffff";
						if (player.WorldData?.CurrentGameMode == EnumGameMode.Creative) {
							ITreeAttribute healAttribute = WatchedAttributes.GetTreeAttribute("health");
							if (healAttribute != null) {
								infotext.AppendLine($"<font color=\"#ff8888\">Health: {healAttribute.GetFloat("currenthealth")}/{healAttribute.GetFloat("maxhealth")}</font>");
							}
							infotext.AppendLine($"<font color=\"#bbbbbb\">{EntityId}</font>");
							if (cachedData.kingdomGUID != null) {
								infotext.AppendLine($"<font color=\"{colourKingdom}\">{LangUtility.Get("entries-keyword-kingdom")}: {cachedData.kingdomGUID}</font>");
							}
							if (cachedData.cultureGUID != null) {
								infotext.AppendLine($"{LangUtility.Get("entries-keyword-culture")}: {cachedData.cultureGUID}");
							}
							if (cachedData.leadersGUID != null) {
								infotext.AppendLine($"{LangUtility.Get("entries-keyword-leaders")}: {cachedData.leadersGUID}");
							}
						} else if (player != null && player.WorldData?.CurrentGameMode == EnumGameMode.Survival) {
							ITreeAttribute nameAttribute = WatchedAttributes.GetTreeAttribute("nametag");
							if (nameAttribute != null) {
								if (nameAttribute.HasAttribute("full")) {
									infotext.AppendLine($"<font color=\"#bbbbbb\">{nameAttribute.GetString("full")}</font>");
								}
							}
							if (cachedData.kingdomNAME != null) {
								infotext.AppendLine($"<font color=\"{colourKingdom}\">{LangUtility.Get("entries-keyword-kingdom")}: {cachedData.kingdomNAME}</font>");
							}
							if (cachedData.cultureNAME != null) {
								infotext.AppendLine($"{LangUtility.Get("entries-keyword-culture")}: {cachedData.cultureNAME}");
							}
							if (cachedData.leadersNAME != null) {
								infotext.AppendLine($"{LangUtility.Get("entries-keyword-leaders")}: {cachedData.leadersNAME}");
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
			bool serverSide = Api.Side == EnumAppSide.Server;
			if (mode != EnumInteractMode.Interact || byEntity is not EntityPlayer || cachedData == null) {
				return;
			}
			EntityPlayer player = byEntity as EntityPlayer;
			string theirKingdom = player.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid");
			// Remind them to join their leaders kingdom if they aren't already in it.
			if (serverSide && cachedData.leadersGUID != null && cachedData.leadersGUID == player.PlayerUID && cachedData.kingdomGUID != theirKingdom) {
				WatchedAttributes.GetTreeAttribute("loyalties").SetString("kingdom_guid", theirKingdom);
				WatchedAttributes.MarkPathDirty("loyalties");
			}
			if (cachedData.leadersGUID != null && cachedData.leadersGUID == player.PlayerUID && player.ServerControls.Sneak && itemslot.Empty) {
				ToggleInventoryDialog(player.Player);
				return;
			}
			if (!itemslot.Empty && player.ServerControls.Sneak) {
				// TRY TO REVIVE!
				if (!Alive && itemslot.Itemstack.Item is ItemPoultice) {
					Revive();
					itemslot.TakeOut(1);
					itemslot.MarkDirty();
					if (serverSide) {
						WatchedAttributes.GetTreeAttribute("health").SetFloat("currenthealth", itemslot.Itemstack.ItemAttributes["health"].AsFloat());
						WatchedAttributes.MarkPathDirty("health");
					}
					return;
				}
				// TRY TO EQUIP!
				if (Alive && cachedData.leadersGUID != null && cachedData.leadersGUID == player.PlayerUID && itemslot.Itemstack.Item is ItemWearable wearable) {
					itemslot.TryPutInto(World, gearInv[(int)wearable.DressType]);
					return;
				}
				if (Alive && cachedData.leadersGUID != null && cachedData.leadersGUID == player.PlayerUID && itemslot.Itemstack.Collectible.Tool != null || itemslot.Itemstack.ItemAttributes?["toolrackTransform"].Exists == true) {
					if (player.RightHandItemSlot.TryPutInto(byEntity.World, RightHandItemSlot) == 0) {
						player.RightHandItemSlot.TryPutInto(byEntity.World, LeftHandItemSlot);
						return;
					}
				}
				// TRY TO RECRUIT!
				if (Alive && cachedData.leadersGUID == null && itemslot.Itemstack.ItemAttributes["currency"].Exists) {
					itemslot.TakeOut(1);
					itemslot.MarkDirty();
					if (serverSide) {
						WatchedAttributes.SetString("enlistedStatus", EnlistedStatus.ENLISTED.ToString());
						WatchedAttributes.GetTreeAttribute("loyalties").SetString("leaders_guid", player.PlayerUID);
						WatchedAttributes.GetTreeAttribute("loyalties").SetString("kingdom_guid", theirKingdom);
						WatchedAttributes.MarkPathDirty("loyalties");
					}
					return;
				}
				// TRY TO RALLY!
				if (Alive && cachedData.kingdomGUID != null && cachedData.kingdomGUID == theirKingdom && itemslot.Itemstack.Item is ItemBanner) {
					if (!player.WatchedAttributes.HasAttribute("followerEntityUids")) {
						ServerAPI?.World.PlayerByUid(player.PlayerUID).Entity.WatchedAttributes.SetAttribute("followerEntityUids", new LongArrayAttribute(new long[] { }));
					}
					var followed = (player.WatchedAttributes.GetAttribute("followerEntityUids") as LongArrayAttribute)?.value?.ToList<long>();
					var sentries = World.GetEntitiesAround(ServerPos.XYZ, 16f, 8f, entity => (entity is EntitySentry sentry && sentry.cachedData.kingdomGUID == theirKingdom));
					foreach (EntitySentry sentry in sentries) {
						sentry.WatchedAttributes.SetLong("guardedEntityId", player.EntityId);
						sentry.ruleOrder[1] = true;
						sentry.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager.GetTask<AiTaskSentryFollow>()?.StartExecute();
						if (!followed.Contains(sentry.EntityId)) {
							followed.Add(sentry.EntityId);
						}
						ServerAPI?.Network.GetChannel("sentrynetwork").SendPacket<SentryUpdate>(new SentryUpdate() {
							playerUID = player.EntityId,
							entityUID = sentry.EntityId,
							kingdomINFO = new string[2] { sentry.cachedData.kingdomGUID, sentry.cachedData.kingdomNAME },
							cultureINFO = new string[2] { sentry.cachedData.cultureGUID, sentry.cachedData.cultureNAME },
							leadersINFO = new string[2] { sentry.cachedData.leadersGUID, sentry.cachedData.leadersNAME }
						}, player.Player as IServerPlayer);
					}
					player.WatchedAttributes.SetAttribute("followerEntityUids", new LongArrayAttribute(followed.ToArray<long>()));
					return;
				}
			}
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
				if (attacker is EntityPlayer player && Loyalties?.GetString("leaders_guid") == player.PlayerUID) {
					return player.ServerControls.Sneak && base.ShouldReceiveDamage(damageSource, damage);
				}
				if (cachedData.kingdomGUID == attacker.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") && ServerAPI.World.Config.GetAsBool("FriendlyFire", true)) {
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
			if (Api.Side == EnumAppSide.Client) {
				return;
			}
			var nearPlayer = ServerAPI?.World.NearestPlayer(ServerPos.X, ServerPos.Y, ServerPos.Z) as IServerPlayer;
			SentryUpdate update = new SentryUpdate();
			update.playerUID = nearPlayer?.Entity.EntityId ?? 0;
			update.entityUID = this.EntityId;
			update.kingdomINFO = new string[2] { Loyalties.GetString("kingdom_guid"), null };
			update.cultureINFO = new string[2] { Loyalties.GetString("culture_guid"), null };
			update.leadersINFO = new string[2] { Loyalties.GetString("leaders_guid"), null };
			(Api as ICoreServerAPI)?.Network.GetChannel("sentrynetwork").SendPacket<SentryUpdate>(update, nearPlayer);
		}

		public virtual void UpdateStats() {
			EntitySentry thisEnt = (Api as ICoreServerAPI)?.World.GetEntityById(this.EntityId) as EntitySentry;
			try {
				Loyalties = WatchedAttributes.GetOrAddTreeAttribute("loyalties");
				thisEnt.WatchedAttributes.SetString("enlistedStatus", new string((RightHandItemSlot.Empty && LeftHandItemSlot.Empty) ? EnlistedStatus.CIVILIAN.ToString() : EnlistedStatus.ENLISTED.ToString()));
				float massTotal = 0;
				foreach (var slot in gearInv) {
					if (!slot.Empty && slot.Itemstack.Item is ItemWearable armor) {
						massTotal += (armor.StatModifers?.walkSpeed ?? 0);
					}
				}
				thisEnt.cachedData.moveSpeed = Properties.Attributes["moveSpeed"].AsFloat(0.030f) * (1 + massTotal);
				thisEnt.cachedData.walkSpeed = Properties.Attributes["walkSpeed"].AsFloat(0.015f) * (1 + massTotal);
				if (!RightHandItemSlot.Empty) {
					// Set weapon class, movement speed, and ranges to distinguish behaviors.
					thisEnt.cachedData.weapRange = RightHandItemSlot?.Itemstack.Collectible.AttackRange ?? GlobalConstants.DefaultAttackRange;
				} else {
					thisEnt.cachedData.weapRange = GlobalConstants.DefaultAttackRange;
				}
				if (Api.World.BlockAccessor.GetBlockEntity(Loyalties.GetBlockPos("outpost_xyzd")) is BlockEntityPost post) {
					thisEnt.cachedData.postRange = (float)post.areasize;
				} else {
					thisEnt.cachedData.postRange = 6f;
				}
				// Update animations to match equipped items!
				string weapon = RightHandItemSlot.Itemstack?.Item?.FirstCodePart() ?? "";
				if (!GlobalCodes.allowedWeaponry.Contains(weapon)) {
					weapon = "";
				}
				string[] weaponCodes = ItemsProperties.WeaponAnimations.Find(match => match.itemCode == weapon).allCodes;
				thisEnt.cachedData.idleAnims = weaponCodes[0];
				thisEnt.cachedData.walkAnims = weaponCodes[1];
				thisEnt.cachedData.moveAnims = weaponCodes[2];
				thisEnt.cachedData.duckAnims = weaponCodes[3];
				thisEnt.cachedData.swimAnims = weaponCodes[4];
				thisEnt.cachedData.jumpAnims = weaponCodes[5];
				thisEnt.cachedData.diesAnims = weaponCodes[6];
				thisEnt.cachedData.drawAnims = weaponCodes[7];
				thisEnt.cachedData.fireAnims = weaponCodes[8];
				thisEnt.cachedData.loadAnims = weaponCodes[9];
				thisEnt.cachedData.bashAnims = weaponCodes[10];
				thisEnt.cachedData.stabAnims = weaponCodes[11];
			} catch (NullReferenceException e) {
				World.Logger.Error(e.ToString());
			}
			if (Api.Side == EnumAppSide.Client) {
				cachedData = thisEnt.cachedData.Copy();
			}
		}

		public virtual void UpdateInfos(byte[] data) {
			SentryUpdate update = SerializerUtil.Deserialize<SentryUpdate>(data);
			if (update.kingdomINFO.Length > 0) {
				if (update.kingdomINFO[0] != null) {
					cachedData.kingdomGUID = update.kingdomINFO[0];
				}
				if (update.kingdomINFO[1] != null) {
					cachedData.kingdomNAME = update.kingdomINFO[1];
				}
				cachedData.coloursLIST = update.coloursLIST;
				cachedData.enemiesLIST = update.enemiesLIST;
				cachedData.friendsLIST = update.friendsLIST;
				cachedData.outlawsLIST = update.outlawsLIST;
			}
			if (update.cultureINFO.Length > 0) {
				if (update.cultureINFO[0] != null) {
					cachedData.cultureGUID = update.cultureINFO[0];
				}
				if (update.cultureINFO[1] != null) {
					cachedData.cultureNAME = update.cultureINFO[1];
				}
			}
			if (update.leadersINFO.Length > 0) {
				if (update.leadersINFO[0] != null) {
					cachedData.leadersGUID = update.leadersINFO[0];
				}
				if (update.leadersINFO[1] != null) {
					cachedData.leadersNAME = update.leadersINFO[1];
				}
			}
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