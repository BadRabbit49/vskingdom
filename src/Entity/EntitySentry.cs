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
		public virtual SentryTalkUtils sentryTalk { get; set; }
		public virtual SentryDataCache cachedData { get; set; }
		public virtual ClientDataCache clientData { get; set; }
		public virtual InventorySentry gearInv { get; set; }
		public virtual InvSentryDialog InventoryDialog { get; set; }
		public override ItemSlot LeftHandItemSlot => gearInv[15];
		public override ItemSlot RightHandItemSlot => gearInv[16];
		public virtual ItemSlot BackItemSlot => gearInv[17];
		public virtual ItemSlot AmmoItemSlot => gearInv[18];
		public virtual ItemSlot HealItemSlot => gearInv[19];
		public override IInventory GearInventory => gearInv;
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
				sentryTalk = new SentryTalkUtils(capi, this);
			}
			// Register listeners if api is on server.
			if (api is ICoreServerAPI sapi) {
				ServerAPI = sapi;
				WatchedAttributes.RegisterModifiedListener("inventory", ReadInventoryFromAttributes);
				GetBehavior<EntityBehaviorHealth>().onDamaged += (dmg, dmgSource) => HealthUtility.handleDamaged(World.Api, this, dmg, dmgSource);
				ReadInventoryFromAttributes();
			}
		}

		public override void OnEntitySpawn() {
			base.OnEntitySpawn();
			this.OnEntityLoaded();
		}

		public override void OnEntityLoaded() {
			base.OnEntityLoaded();
			if (Api.Side == EnumAppSide.Server) {
				bool previousExists = cachedData != null;
				cachedData = new SentryDataCache() {
					moveSpeed = Properties.Attributes["moveSpeed"].AsFloat(0.030f),
					walkSpeed = Properties.Attributes["walkSpeed"].AsFloat(0.015f),
					postRange = Properties.Attributes["postRange"].AsFloat(6.0f),
					weapRange = Properties.Attributes["weapRange"].AsFloat(1.5f),
					idleAnims = Properties.Attributes["idleAnims"].AsString("idle").ToLower(),
					walkAnims = Properties.Attributes["walkAnims"].AsString("walk").ToLower(),
					moveAnims = Properties.Attributes["moveAnims"].AsString("move").ToLower(),
					duckAnims = Properties.Attributes["duckAnims"].AsString("duck").ToLower(),
					swimAnims = Properties.Attributes["swimAnims"].AsString("swim").ToLower(),
					jumpAnims = Properties.Attributes["jumpAnims"].AsString("jump").ToLower(),
					postBlock = WatchedAttributes.GetBlockPos("postBlock").ToVec3d(),
					kingdomGUID = WatchedAttributes.GetString("kingdomGUID"),
					cultureGUID = WatchedAttributes.GetString("cultureGUID"),
					leadersGUID = WatchedAttributes.GetString("leadersGUID"),
					recruitINFO = (previousExists ? cachedData.recruitINFO : "CIVILIAN"),
					enemiesLIST = (previousExists ? cachedData.enemiesLIST : new string[] { GlobalCodes.banditryGUID }),
					friendsLIST = (previousExists ? cachedData.friendsLIST : new string[] { GlobalCodes.commonerGUID }),
					outlawsLIST = (previousExists ? cachedData.outlawsLIST : new string[] { })
				};
				UpdateStats();
				UpdateTasks();
			}
			if (Api.Side == EnumAppSide.Client) {
				bool previousExists = clientData != null;
				clientData = new ClientDataCache() {
					kingdomNAME = new string(previousExists ? clientData.kingdomNAME : "Commoner"),
					cultureNAME = new string(previousExists ? clientData.cultureNAME : "Seraphim"),
					leadersNAME = new string(previousExists ? clientData.leadersNAME : ""),
					coloursHEXA = new string(previousExists ? clientData.coloursHEXA : "#ffffff"),
					coloursHEXB = new string(previousExists ? clientData.coloursHEXB : "#ffffff"),
					coloursHEXC = new string(previousExists ? clientData.coloursHEXA : "#ffffff")
				};
				SentryUpdateToServer update = new SentryUpdateToServer();
				update.entityUID = EntityId;
				update.kingdomGUID = WatchedAttributes.GetString("kingdomGUID");
				update.cultureGUID = WatchedAttributes.GetString("cultureGUID");
				update.leadersGUID = WatchedAttributes.GetString("leadersGUID");
				ClientAPI.Network.GetChannel("sentrynetwork").SendPacket(update);
			}
		}

		public override string GetInfoText() {
			try {
				StringBuilder infotext = new StringBuilder();
				if (!Alive && WatchedAttributes.HasAttribute("deathByPlayer")) {
					infotext.AppendLine(Lang.Get("Killed by Player: {0}", WatchedAttributes.GetString("deathByPlayer")));
				}
				bool playersCreative = false;
				if (Api.Side == EnumAppSide.Client) {
					IClientPlayer player = (World as IClientWorldAccessor).Player;
					playersCreative = player != null && player.WorldData?.CurrentGameMode == EnumGameMode.Creative;
				}
				if (playersCreative) {
					ITreeAttribute healthyTree = WatchedAttributes.GetTreeAttribute("health");
					if (healthyTree != null) {
						infotext.AppendLine($"<font color=\"#ff8888\">Health: {healthyTree.GetFloat("currenthealth")}/{healthyTree.GetFloat("maxhealth")}</font>");
					}
					infotext.AppendLine($"<font color=\"#bbbbbb\">{EntityId}</font>");
					if (WatchedAttributes.HasAttribute("kingdomGUID") && WatchedAttributes.GetString("kingdomGUID") != null) {
						infotext.AppendLine($"<font color=\"{clientData.coloursHEXC}\">{LangUtility.Get("entries-keyword-kingdom")}: {WatchedAttributes.GetString("kingdomGUID")}</font>");
					}
					if (WatchedAttributes.HasAttribute("cultureGUID") && WatchedAttributes.GetString("cultureGUID") != null) {
						infotext.AppendLine($"{LangUtility.Get("entries-keyword-culture")}: {WatchedAttributes.GetString("cultureGUID")}");
					}
					if (WatchedAttributes.HasAttribute("leadersGUID") && WatchedAttributes.GetString("leadersGUID") != null) {
						infotext.AppendLine($"{LangUtility.Get("entries-keyword-leaders")}: {WatchedAttributes.GetString("leadersGUID")}");
					}
				} else {
					ITreeAttribute nametagTree = WatchedAttributes.GetTreeAttribute("nametag");
					if (nametagTree != null && nametagTree.HasAttribute("full")) {
						infotext.AppendLine($"<font color=\"#bbbbbb\">{nametagTree.GetString("full")}</font>");
					}
					infotext.AppendLine($"<font color=\"{clientData.coloursHEXC}\">{LangUtility.Get("entries-keyword-kingdom")}: {clientData.kingdomNAME}</font>");
					infotext.AppendLine($"{LangUtility.Get("entries-keyword-culture")}: {clientData.cultureNAME}");
					infotext.AppendLine($"{LangUtility.Get("entries-keyword-leaders")}: {clientData.leadersNAME}");
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
			if ((mode != EnumInteractMode.Interact && !(mode == EnumInteractMode.Attack && !itemslot.Empty && itemslot.Itemstack.Item is ItemPoultice)) || byEntity is not EntityPlayer) {
				return;
			}
			EntityPlayer player = byEntity as EntityPlayer;
			EntitySentry entity = (ServerAPI?.World.GetEntityById(this.EntityId) as EntitySentry) ?? this;
			string theirKingdom = player.WatchedAttributes.GetString("kingdomGUID");
			string kingdomGuid = WatchedAttributes.GetString("kingdomGUID");
			string cultureGuid = WatchedAttributes.GetString("cultureGUID");
			string leadersGuid = WatchedAttributes.GetString("leadersGUID");
			bool IsTheLeader = leadersGuid != null && leadersGuid == player.PlayerUID;
			bool LootingBody = !Alive && World.Config.GetAsBool("AllowLooting");
			// Remind them to join their leaders kingdom if they aren't already in it.
			if (IsTheLeader && Api.Side == EnumAppSide.Client) {
				// This works! //
				SentryUpdateToServer update = new SentryUpdateToServer();
				update.entityUID = EntityId;
				update.kingdomGUID = theirKingdom;
				update.cultureGUID = cultureGuid;
				update.leadersGUID = leadersGuid;
				ClientAPI.Network.GetChannel("sentrynetwork").SendPacket(update);
			}
			if ((IsTheLeader || LootingBody) && player.Controls.Sneak && itemslot.Empty) {
				ToggleInventoryDialog(player.Player);
				return;
			}
			if (Alive && leadersGuid != null && leadersGuid == player.PlayerUID && Api.Side == EnumAppSide.Server) {
				entity?.GetBehavior<EntityBehaviorTaskAI>().TaskManager?.GetTask<AiTaskSentryPatrol>()?.PauseExecute(player);
				entity?.GetBehavior<EntityBehaviorTaskAI>().TaskManager?.GetTask<AiTaskSentryWander>()?.PauseExecute(player);
			}
			if (!itemslot.Empty && player.Controls.Sneak) {
				// TRY TO REVIVE!
				if (!Alive && itemslot.Itemstack.Item is ItemPoultice) {
					Revive();
					itemslot.TakeOut(1);
					itemslot.MarkDirty();
					WatchedAttributes.GetTreeAttribute("health").SetFloat("currenthealth", itemslot.Itemstack.ItemAttributes["health"].AsFloat());
					WatchedAttributes.MarkPathDirty("health");
					return;
				}
				// TRY TO EQUIP!
				if (Alive && leadersGuid != null && leadersGuid == player.PlayerUID && itemslot.Itemstack.Item is ItemWearable wearable) {
					itemslot.TryPutInto(World, gearInv[(int)wearable.DressType]);
					return;
				}
				if (Alive && leadersGuid != null && leadersGuid == player.PlayerUID && itemslot.Itemstack.Collectible.Tool != null || itemslot.Itemstack.ItemAttributes?["toolrackTransform"].Exists == true) {
					if (player.RightHandItemSlot.TryPutInto(byEntity.World, RightHandItemSlot) == 0) {
						player.RightHandItemSlot.TryPutInto(byEntity.World, LeftHandItemSlot);
						return;
					}
				}
				// TRY TO RECRUIT!
				if (Alive && leadersGuid == null && itemslot.Itemstack.ItemAttributes["currency"].Exists) {
					itemslot.TakeOut(1);
					itemslot.MarkDirty();
					if (Api.Side == EnumAppSide.Server) {
						cachedData.recruitINFO = EnlistedStatus.ENLISTED.ToString();
					}
					WatchedAttributes.SetString("leadersGUID", player.PlayerUID);
					WatchedAttributes.SetString("leadersNAME", player.Player.PlayerName);
					WatchedAttributes.SetString("kingdomGUID", theirKingdom);
					return;
				}
				// TRY TO RALLY!
				if (Alive && kingdomGuid != null && kingdomGuid == theirKingdom && itemslot?.Itemstack?.Item is ItemBanner) {
					if (!player.WatchedAttributes.HasAttribute("followerEntityUids")) {
						ServerAPI?.World.PlayerByUid(player.PlayerUID).Entity.WatchedAttributes.SetAttribute("followerEntityUids", new LongArrayAttribute(new long[] { }));
					}
					var followed = (player.WatchedAttributes.GetAttribute("followerEntityUids") as LongArrayAttribute)?.value?.ToList<long>();
					var sentries = World.GetEntitiesAround(ServerPos.XYZ, 16f, 8f, entity => (entity is EntitySentry sentry && sentry.WatchedAttributes.GetString("kingdomGUID") == theirKingdom));
					foreach (EntitySentry sentry in sentries) {
						sentry.WatchedAttributes.SetLong("guardedEntityId", player.EntityId);
						sentry.WatchedAttributes.SetBool("orderFollow", true);
						sentry.ruleOrder[1] = true;
						sentry.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager.GetTask<AiTaskSentryFollow>()?.StartExecute();
						if (!followed.Contains(sentry.EntityId)) {
							followed.Add(sentry.EntityId);
						}
						ServerAPI?.Network.GetChannel("sentrynetwork").SendPacket<SentryUpdateToServer>(new SentryUpdateToServer() {
							entityUID = sentry.EntityId,
							kingdomGUID = sentry.WatchedAttributes.GetString("kingdomGUID"),
							cultureGUID = sentry.WatchedAttributes.GetString("cultureGUID"),
							leadersGUID = sentry.WatchedAttributes.GetString("leadersGUID")
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
				case 1502:
					UpdateInfos(data);
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
				if (attacker is EntityPlayer player && WatchedAttributes.GetString("leadersGUID") == player.PlayerUID) {
					return player.ServerControls.Sneak && base.ShouldReceiveDamage(damageSource, damage);
				}
				if (WatchedAttributes.GetString("kingdomGUID") == attacker.WatchedAttributes.GetString("kingdomGUID") && Api.World.Config.GetAsBool("FriendlyFire", true)) {
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

		public override void Die(EnumDespawnReason reason = EnumDespawnReason.Death, DamageSource damageSourceForDeath = null) {
			if (!Alive) {
				return;
			}
			if (reason == EnumDespawnReason.Death) {
				PlayEntitySound("death");
			}
			if (reason != 0) {
				AllowDespawn = true;
			}
			Alive = false;
			controls.WalkVector.Set(0.0, 0.0, 0.0);
			controls.FlyVector.Set(0.0, 0.0, 0.0);
			ClimbingOnFace = null;
			if (reason == EnumDespawnReason.Death) {
				Api.Event.TriggerEntityDeath(this, damageSourceForDeath);
				ItemStack[] drops = GetDrops(World, Pos.AsBlockPos, null);
				if (drops != null) {
					for (int i = 0; i < drops.Length; i++) {
						World.SpawnItemEntity(drops[i], SidedPos.XYZ.AddCopy(0.0, 0.25, 0.0));
					}
				}
				if (Properties.Attributes["deathAnim"].Exists && Properties.Attributes["deathBone"].Exists) {
					string[] deathAnims = Properties.Attributes["deathAnim"].AsArray<string>(null);
					string[] deathBlock = Properties.Attributes["deathBone"].AsArray<string>(null);
					if (deathAnims.Length == deathBlock.Length) {
						int indexAt = World.Rand.Next(0, deathAnims.Length - 1);
						WatchedAttributes.SetString("deathAnimation", deathAnims[indexAt]);
						WatchedAttributes.SetString("deathSkeletons", deathBlock[indexAt]);
					} else {
						WatchedAttributes.SetString("deathAnimation", deathAnims[World.Rand.Next(0, deathAnims.Length - 1)]);
						WatchedAttributes.SetString("deathSkeletons", deathBlock[World.Rand.Next(0, deathBlock.Length - 1)]);
					}
				}
				AnimManager.ActiveAnimationsByAnimCode.Clear();
				AnimManager.StartAnimation(new string(WatchedAttributes.GetString("deathAnimation", "die")));
				if (reason == EnumDespawnReason.Death && damageSourceForDeath != null && World.Side == EnumAppSide.Server) {
					WatchedAttributes.SetInt("deathReason", (int)damageSourceForDeath.Source);
					WatchedAttributes.SetInt("deathDamageType", (int)damageSourceForDeath.Type);
					Entity causeEntity = damageSourceForDeath.GetCauseEntity();
					if (causeEntity != null) {
						WatchedAttributes.SetString("deathByEntityLangCode", "prefixandcreature-" + causeEntity.Code.Path.Replace("-", ""));
						WatchedAttributes.SetString("deathByEntity", causeEntity.Code.ToString());
					}
					if (causeEntity is EntityPlayer player) {
						if (!player.GearInventory[8].Empty) {
							WatchedAttributes.SetString("deathByPlayer", "Masked assailant...");
						} else {
							WatchedAttributes.SetString("deathByPlayer", (causeEntity as EntityPlayer).Player?.PlayerName);
						}
					}
					if (causeEntity is EntitySentry sentry) {
						if (!sentry.GearInventory[8].Empty) {
							WatchedAttributes.SetString("deathBySentry", "Masked assailant...");
						} else {
							WatchedAttributes.SetString("deathBySentry", (causeEntity as EntitySentry).WatchedAttributes.GetTreeAttribute("nametag")?.GetString("full"));
						}
					}
				}
				foreach (EntityBehavior behavior in SidedProperties.Behaviors) {
					behavior.OnEntityDeath(damageSourceForDeath);
				}
			}
			DespawnReason = new EntityDespawnData {
				Reason = reason,
				DamageSourceForDeath = damageSourceForDeath
			};
		}

		public override void Revive() {
			this.Alive = true;
			ReceiveDamage(new DamageSource { SourceEntity = this, CauseEntity = this, Source = EnumDamageSource.Revive, Type = EnumDamageType.Heal }, 9999f);
			AnimManager.StopAnimation(new string(WatchedAttributes.GetString("deathAnimation")));
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
				if (!Alive && gearInv.Empty && HasBehavior<EntityBehaviorDecayBody>()) {
					GetBehavior<EntityBehaviorDecayBody>()?.DecayNow(this);
				}
				return;
			}
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

		public virtual void UpdateStats() {
			if (Api.Side == EnumAppSide.Client) { return; }
			try {
				bool hasWeapon = !RightHandItemSlot.Empty;
				float massTotal = 0;
				float healTotal = 0;
				float walkSpeed = Properties.Attributes["walkSpeed"].AsFloat(0.015f);
				float moveSpeed = Properties.Attributes["moveSpeed"].AsFloat(0.030f);
				foreach (var slot in gearInv) {
					if (!slot.Empty && slot.Itemstack.Item is ItemWearable armor) {
						massTotal += (armor.StatModifers?.walkSpeed ?? 0);
						healTotal += (armor.StatModifers?.healingeffectivness ?? 0);
					}
				}
				cachedData.healRates = Math.Clamp((1f - (1f * healTotal)), 0f, 2f);
				cachedData.walkSpeed = Math.Clamp((walkSpeed + (walkSpeed * massTotal)), 0f, walkSpeed * 2f);
				cachedData.moveSpeed = Math.Clamp((moveSpeed + (moveSpeed * massTotal)), 0f, moveSpeed * 2f);
				cachedData.weapRange = hasWeapon ? RightHandItemSlot.Itemstack.Collectible.AttackRange : GlobalConstants.DefaultAttackRange;
				cachedData.recruitINFO = new string(hasWeapon ? "ENLISTED" : Properties.Attributes["baseState"].AsString("CIVILIAN"));
				if (Api.World.BlockAccessor.GetBlockEntity(WatchedAttributes.GetBlockPos("postBlock")) is BlockEntityPost post) {
					WatchedAttributes.SetDouble("postRange", cachedData.postRange = (float)post.areasize);
					WatchedAttributes.SetBlockPos("postBlock", post.Pos);
				} else {
					WatchedAttributes.SetDouble("postRange", cachedData.postRange = 6f);
				}
				// Update animations to match equipped items!
				string weapon = RightHandItemSlot.Itemstack?.Item?.FirstCodePart() ?? "";
				if (GlobalCodes.allowedWeaponry.Contains(weapon)) {
					var weaponClass = ItemsProperties.WeaponAnimations.Find(match => match.itemCode == weapon);
					string[] weaponCodes = weaponClass.allCodes;
					cachedData.UpdateAnimate(weaponCodes);
				}
			} catch (NullReferenceException e) {
				World.Logger.Error(e.ToString());
			}
		}

		public virtual void UpdateInfos(byte[] data) {
			SentryUpdateToEntity update = SerializerUtil.Deserialize<SentryUpdateToEntity>(data);
			WatchedAttributes.SetString("kingdomGUID", new string(update.kingdomGUID));
			WatchedAttributes.SetString("cultureGUID", new string(update.cultureGUID));
			WatchedAttributes.SetString("leadersGUID", new string(update.leadersGUID));
			if (Api.Side == EnumAppSide.Client) {
				clientData.UpdateLoyalty(update.kingdomNAME, update.cultureNAME, update.leadersNAME);
				clientData.UpdateColours(update.coloursHEXA, update.coloursHEXB, update.coloursHEXC);
			}
		}

		public virtual void UpdateTasks() {
			ruleOrder = new bool[7] {
				WatchedAttributes.GetBool("orderWander"),
				WatchedAttributes.GetBool("orderFollow"),
				WatchedAttributes.GetBool("orderEngage"),
				WatchedAttributes.GetBool("orderPursue"),
				WatchedAttributes.GetBool("orderShifts"),
				WatchedAttributes.GetBool("orderPatrol"),
				WatchedAttributes.GetBool("orderReturn")
			};
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