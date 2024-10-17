using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
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
using static VSKingdom.Utilities.DamagesUtil;
using static VSKingdom.Utilities.ReadingUtil;

namespace VSKingdom {
	public class EntitySentry : EntityHumanoid {
		public EntitySentry() { }
		public virtual bool[] ruleOrder { get; set; }
		public virtual bool[] passenger { get; set; }
		public virtual string inventory => "gear-" + EntityId;
		public virtual SentryTalkUtils sentryTalk { get; set; }
		public virtual SentryDataCache cachedData { get; set; }
		public virtual ClientDataCache clientData { get; set; }
		public virtual InventorySentry gearInv { get; set; }
		public virtual InvSentryDialog gearDialog { get; set; }
		public virtual SentryTraverser pathfinder { get; set; }
		public override ItemSlot LeftHandItemSlot => gearInv[15];
		public override ItemSlot RightHandItemSlot => gearInv[16];
		public virtual ItemSlot BackItemSlot => gearInv[17];
		public virtual ItemSlot AmmoItemSlot => gearInv[18];
		public virtual ItemSlot HealItemSlot => gearInv[19];
		public override bool StoreWithChunk => true;
		public override bool AlwaysActive => false;
		public override double LadderFixDelta { get => Properties.SpawnCollisionBox.Y2 - SelectionBox.YSize; }
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
			if (sentryTalk is null) {
				sentryTalk = new SentryTalkUtils(api, this);
			}
			// Register stuff for client-side api.
			if (api is ICoreClientAPI capi) {
				ClientAPI = capi;
			}
			// Register listeners if api is on server.
			if (api is ICoreServerAPI sapi) {
				ServerAPI = sapi;
				ruleOrder = new bool[7] { true, false, true, true, false, false, false };
				passenger = new bool[2] { false, false };
				WatchedAttributes.RegisterModifiedListener("inventory", ReadInventoryFromAttributes);
				WatchedAttributes.RegisterModifiedListener("mountedOn", updateMountedState);
				if (WatchedAttributes["mountedOn"] != null) {
					MountedOn = World.ClassRegistry.CreateMountable(WatchedAttributes["mountedOn"] as TreeAttribute);
					if (MountedOn != null) {
						TryMount(MountedOn);
						passenger = new bool[2] { true, MountedOn.CanControl };
					}
				}
				if (cachedData is null) {
					cachedData = new SentryDataCache();
				}
				var taskBehaviors = GetBehavior<EntityBehaviorTaskAI>();
				var pathTraverser = new SentryTraverser(this);
				taskBehaviors.PathTraverser = pathTraverser;
				taskBehaviors.TaskManager.AllTasks.ForEach(task => typeof(AiTaskBase)
					.GetField("pathTraverser", BindingFlags.Instance | BindingFlags.NonPublic)
					.SetValue(task, pathTraverser));
				this.pathfinder = pathTraverser;
				GetBehavior<EntityBehaviorHealth>().onDamaged += (dmg, dmgSource) => handleDamaged(World.Api, this, dmg, dmgSource);
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
					moveSpeed = Properties.Attributes["moveSpeed"].AsFloat(0.04f),
					walkSpeed = Properties.Attributes["walkSpeed"].AsFloat(0.02f),
					postRange = Properties.Attributes["postRange"].AsFloat(6.0f),
					weapRange = Properties.Attributes["weapRange"].AsFloat(1.5f),
					postBlock = WatchedAttributes.GetBlockPos("postBlock").ToVec3d(),
					kingdomGUID = WatchedAttributes.GetString(KingdomGUID),
					cultureGUID = WatchedAttributes.GetString(CultureGUID),
					leadersGUID = WatchedAttributes.GetString(LeadersGUID),
					recruitINFO = (previousExists ? cachedData.recruitINFO : "CIVILIAN"),
					enemiesLIST = (previousExists ? cachedData.enemiesLIST : new string[] { BanditrysID }),
					friendsLIST = (previousExists ? cachedData.friendsLIST : new string[] { CommonersID }),
					outlawsLIST = (previousExists ? cachedData.outlawsLIST : new string[] { })
				};
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
				update.kingdomGUID = WatchedAttributes.GetString(KingdomGUID);
				update.cultureGUID = WatchedAttributes.GetString(CultureGUID);
				update.leadersGUID = WatchedAttributes.GetString(LeadersGUID);
				ClientAPI.Network.GetChannel("sentrynetwork").SendPacket(update);
			}
			UpdateStats();
			UpdateTasks();
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
						infotext.AppendLine($"<font color=\"#ff8888\">Health: {healthyTree.GetFloat("currenthealth")}/{healthyTree.GetFloat("basemaxhealth")}</font>");
					}
					infotext.AppendLine($"<font color=\"#bbbbbb\">{EntityId}</font>");
					if (WatchedAttributes.HasAttribute(KingdomGUID) && WatchedAttributes.GetString(KingdomGUID) != null) {
						infotext.AppendLine($"<font color=\"{clientData.coloursHEXC}\">{Get("entries-keyword-kingdom")}: {WatchedAttributes.GetString(KingdomGUID)}</font>");
					}
					if (WatchedAttributes.HasAttribute(CultureGUID) && WatchedAttributes.GetString(CultureGUID) != null) {
						infotext.AppendLine($"{Get("entries-keyword-culture")}: {WatchedAttributes.GetString(CultureGUID)}");
					}
					if (WatchedAttributes.HasAttribute(LeadersGUID) && WatchedAttributes.GetString(LeadersGUID) != null) {
						infotext.AppendLine($"{Get("entries-keyword-leaders")}: {WatchedAttributes.GetString(LeadersGUID)}");
					}
				} else {
					ITreeAttribute nametagTree = WatchedAttributes.GetTreeAttribute("nametag");
					if (nametagTree != null && nametagTree.HasAttribute("full")) {
						infotext.AppendLine($"<font color=\"#bbbbbb\">{nametagTree.GetString("full")}</font>");
					}
					infotext.AppendLine($"<font color=\"{clientData.coloursHEXC}\">{Get("entries-keyword-kingdom")}: {clientData.kingdomNAME}</font>");
					infotext.AppendLine($"{Get("entries-keyword-culture")}: {clientData.cultureNAME}");
					infotext.AppendLine($"{Get("entries-keyword-leaders")}: {clientData.leadersNAME}");
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

		public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player) {
			List<WorldInteraction> interactions = new List<WorldInteraction>();
			if (!Alive) {
				List<ItemStack> bandages = new List<ItemStack>();
				for (int i = 0; i < VanillaBandages.Length; i++) {
					var item = world.GetItem(new AssetLocation(VanillaBandages[i]));
					if (item != null) {
						bandages.Add(new ItemStack(item));
					}
				}
				interactions.Add(new WorldInteraction() {
					ActionLangCode = "vskingdom:entries-keyword-revive",
					MouseButton = EnumMouseButton.Right,
					RequireFreeHand = false,
					Itemstacks = bandages.ToArray()
				});
			}
			if (WatchedAttributes.GetString(LeadersGUID) == player.PlayerUID) {
				if (!WatchedAttributes.GetBool(OrderWander) && !WatchedAttributes.GetBool(OrderFollow) && !WatchedAttributes.GetBool(OrderEngage) &&
					!WatchedAttributes.GetBool(OrderPursue) && !WatchedAttributes.GetBool(OrderShifts) && !WatchedAttributes.GetBool(OrderPatrol)) {
					interactions.Add(new WorldInteraction() {
						ActionLangCode = "game:blockhelp-ingotmold-pickup",
						MouseButton = EnumMouseButton.Right,
						RequireFreeHand = false,
						HotKeyCode = "shift",
						Itemstacks = new ItemStack[1] { new ItemStack(world.GetItem(new AssetLocation("game:firewood"))) }
					});
				}
				interactions.Add(new WorldInteraction() {
					ActionLangCode = "Inventory",
					MouseButton = EnumMouseButton.Right,
					HotKeyCode = "shift",
					Itemstacks = null
				});
			}
			if (interactions.Count > 0) {
				return interactions.ToArray();
			}
			return base.GetInteractionHelp(world, es, player);
		}

		public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode) {
			base.OnInteract(byEntity, itemslot, hitPosition, mode);
			if (mode != EnumInteractMode.Interact || byEntity is not EntityPlayer || !byEntity.Controls.RightMouseDown) {
				return;
			}
			EntityPlayer player = byEntity as EntityPlayer;
			string theirKingdom = player.WatchedAttributes.GetString(KingdomGUID);
			string kingdomGuid = WatchedAttributes.GetString(KingdomGUID);
			string cultureGuid = WatchedAttributes.GetString(CultureGUID);
			string leadersGuid = WatchedAttributes.GetString(LeadersGUID);
			bool IsTheLeader = leadersGuid != null && leadersGuid == player.PlayerUID;
			bool LootingBody = !Alive && World.Config.GetAsBool(CanLootNpcs);
			bool NoOrdersSet = Api.Side == EnumAppSide.Server && !ruleOrder[0] && !ruleOrder[1] && !ruleOrder[2] && !ruleOrder[3] && !ruleOrder[4] && !ruleOrder[5] && !ruleOrder[6];
			// Pickup the sentry with a piece of firewood.
			if (IsTheLeader && NoOrdersSet && Alive && player.Controls.Sneak && !itemslot.Empty && itemslot.Itemstack?.Item is ItemFirewood) {
				string stackCode = $"{this.Code.Domain}:people-{this.Code.Path}";
				if (byEntity.World.GetItem(new AssetLocation(stackCode)) == null) {
					byEntity.World.Logger.Error("Could not get {0}", stackCode);
					return;
				}
				ItemStack people = new ItemStack(byEntity.World.GetItem(new AssetLocation(stackCode)), 1);
				ITreeAttribute inventories = people.Attributes.GetOrAddTreeAttribute("entInventory");
				ITreeAttribute nametagTree = WatchedAttributes.GetTreeAttribute("nametag");
				ITreeAttribute healthyTree = WatchedAttributes.GetTreeAttribute("health");
				ITreeAttribute appliedTree = WatchedAttributes.GetTreeAttribute("skinConfig");
				for (int i = 0; i < GearsDressCodes.Length; i++) {
					if (!gearInv[i].Empty) {
						inventories.SetItemstack(new string(GearsDressCodes[i] + "Stack"), gearInv[i]?.Itemstack);
					}
				}
				if (healthyTree.HasAttribute("currenthealth")) {
					people.Attributes.SetInt("durability", (int)(100 * (healthyTree.GetFloat("currenthealth") / healthyTree.GetFloat("basemaxhealth"))));
				}
				if (nametagTree.HasAttribute("name") || nametagTree.HasAttribute("last") || nametagTree.HasAttribute("full")) {
					var nametree = people.Attributes.GetOrAddTreeAttribute("nametagParts");
					nametree.SetString("name", nametagTree?.GetString("name"));
					nametree.SetString("last", nametagTree?.GetString("last"));
				}
				if (appliedTree.HasAttribute("appliedParts")) {
					var skintree = people.Attributes.GetOrAddTreeAttribute("appliedParts");
					skintree.SetString("baseskin", appliedTree.GetString("baseskin"));
					skintree.SetString("eyecolor", appliedTree.GetString("eyecolor"));
					skintree.SetString("haircolor", appliedTree.GetString("haircolor"));
					skintree.SetString("hairextra", appliedTree.GetString("hairextra"));
					skintree.SetString("hairbase", appliedTree.GetString("hairbase"));
					skintree.SetString("mustache", appliedTree.GetString("mustache"));
					skintree.SetString("beard", appliedTree.GetString("beard"));
					skintree.SetString("underwear", appliedTree.GetString("underwear"));
					skintree.SetString("facialexpression", appliedTree.GetString("facialexpression"));
				}
				people.Attributes.SetString("gender", this.Code.EndVariant());
				if (!byEntity.TryGiveItemStack(people)) {
					var peopleItem = byEntity.World.SpawnItemEntity(people, ServerPos.XYZ) as EntityItem;
				}
				Die(EnumDespawnReason.Removed);
				return;
			}
			if (IsTheLeader && byEntity.RightHandItemSlot.Empty && byEntity.Pos.DistanceTo(Pos) < 1.2) {
				AiTaskManager tmgr = GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
				tmgr?.StopTask(typeof(AiTaskSentrySearch));
				tmgr?.StopTask(typeof(AiTaskSentryWander));
			}
			// Remind them to join their leaders kingdom if they aren't already in it.
			if (IsTheLeader && Api.Side == EnumAppSide.Client) {
				SentryUpdateToServer update = new SentryUpdateToServer();
				update.entityUID = EntityId;
				update.kingdomGUID = theirKingdom;
				update.cultureGUID = cultureGuid;
				update.leadersGUID = leadersGuid;
				ClientAPI.Network.GetChannel("sentrynetwork").SendPacket(update);
			}
			if ((IsTheLeader || LootingBody) && player.Controls.Sneak && itemslot.Empty) {
				ToggleInventoryDialog(player.Player);
				PlayEntitySound("meets", player.Player);
				return;
			}
			if (Alive && leadersGuid != null && leadersGuid == player.PlayerUID && Api.Side == EnumAppSide.Server) {
				GetBehavior<EntityBehaviorTaskAI>()?.TaskManager?.GetTask<AiTaskSentryPatrol>()?.PauseExecute(player);
				GetBehavior<EntityBehaviorTaskAI>()?.TaskManager?.GetTask<AiTaskSentryWander>()?.PauseExecute(player);
			}
			if (!itemslot.Empty && player.Controls.Sneak) {
				// TRY TO EQUIP ARMOR!
				if (Alive && leadersGuid != null && leadersGuid == player.PlayerUID && itemslot.Itemstack.Item is ItemWearable wearable) {
					itemslot.TryPutInto(World, gearInv[(int)wearable.DressType]);
					return;
				}
				// TRY TO EQUIP WEAPONS!
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
					WatchedAttributes.SetString(LeadersGUID, player.PlayerUID);
					WatchedAttributes.SetString(LeadersNAME, player.Player.PlayerName);
					WatchedAttributes.SetString(KingdomGUID, theirKingdom);
					return;
				}
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
				case 1502:
					UpdateInfos(data);
					return;
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

		public override bool TryMount(IMountable onmount) {
			bool mounted = base.TryMount(onmount);
			bool driving = mounted ? onmount.CanControl : false;
			passenger = new bool[2] { mounted, driving };
			return mounted;
		}

		public override bool ShouldReceiveDamage(DamageSource damageSource, float damage) {
			if (damageSource.GetCauseEntity() is EntityHumanoid attacker) {
				if (attacker is EntityPlayer player && WatchedAttributes.GetString(LeadersGUID) == player.PlayerUID) {
					return player.ServerControls.Sneak && base.ShouldReceiveDamage(damageSource, damage);
				}
				if (WatchedAttributes.GetString(KingdomGUID) == attacker.WatchedAttributes.GetString(KingdomGUID)) {
					return Api.World.Config.GetAsBool(FriendlyDmg, true) && base.ShouldReceiveDamage(damageSource, damage);
				}
				return base.ShouldReceiveDamage(damageSource, damage);
			}
			return base.ShouldReceiveDamage(damageSource, damage);
		}

		public override void Die(EnumDespawnReason reason = EnumDespawnReason.Death, DamageSource damageSourceForDeath = null) {
			if (!Alive) { return; }
			if (reason != 0) {
				AllowDespawn = true;
			}
			Alive = false;
			controls.WalkVector.Set(0.0, 0.0, 0.0);
			controls.FlyVector.Set(0.0, 0.0, 0.0);
			ClimbingOnFace = null;
			TryUnmount();
			passenger = new bool[2] { false, false };
			if (reason == EnumDespawnReason.Removed) {
				if (HasBehavior<EntityBehaviorDecayBody>()) {
					RemoveBehavior(GetBehavior<EntityBehaviorDecayBody>());
				}
				AllowDespawn = true;
			}
			if (reason == EnumDespawnReason.Death) {
				PlayEntitySound("death");
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
				AnimManager.StartAnimation(new string(WatchedAttributes.GetString("deathAnimation", "dies")));
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
			Alive = true;
			ReceiveDamage(new DamageSource { SourceEntity = this, CauseEntity = this, Source = EnumDamageSource.Revive, Type = EnumDamageType.Heal }, 5f);
			AnimManager.StopAnimation(new string(WatchedAttributes.GetString("deathAnimation")));
			AnimManager.StartAnimation(new string(WatchedAttributes.GetString("deathAnimation").Replace("dies", "ress")));
			IsOnFire = false;
			State = EnumEntityState.Active;
			// AI seems to go dead after revive and does not return to doing tasks? Fix.
			foreach (EntityBehavior behavior in SidedProperties.Behaviors) {
				if (behavior is EntityBehaviorTaskAI taskai) {
					taskai.OnEntityLoaded();
				}
				behavior.OnEntityRevive();
			}
		}

		public override void PlayEntitySound(string sound, IPlayer dualCallByPlayer = null, bool randomizePitch = true, float range = 24) {
			switch (sound) {
				case "idle1": sentryTalk?.Talk(EnumTalkType.Idle); return;
				case "idle2": sentryTalk?.Talk(EnumTalkType.IdleShort); return;
				case "hurt1": sentryTalk?.Talk(EnumTalkType.Hurt); return;
				case "hurt2": sentryTalk?.Talk(EnumTalkType.Hurt2); return;
				case "meets": sentryTalk?.Talk(EnumTalkType.Meet); return;
				case "argue": sentryTalk?.Talk(EnumTalkType.Complain); return;
				case "laugh": sentryTalk?.Talk(EnumTalkType.Laugh); return;
				case "stabs": sentryTalk?.Talk(EnumTalkType.Thrust); return;
				case "leave": sentryTalk?.Talk(EnumTalkType.Goodbye); return;
				case "death": sentryTalk?.Talk(EnumTalkType.Death); return;
			}
			base.PlayEntitySound(sound, dualCallByPlayer, randomizePitch, range);
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
			}
			UpdateStats(slotId);
		}

		public virtual void ToggleInventoryDialog(IPlayer player) {
			if (Api.Side != EnumAppSide.Client) { return; }
			if (gearDialog is null) {
				gearDialog = new InvSentryDialog(gearInv, this, ClientAPI);
				gearDialog.OnClosed += OnInventoryDialogClosed;
			}
			if (!gearDialog.TryOpen()) {
				return;
			}
			player.InventoryManager.OpenInventory(GearInventory);
			ClientAPI.Network.SendEntityPacket(EntityId, 1505);
		}

		public virtual void OnInventoryDialogClosed() {
			ClientAPI.World.Player.InventoryManager.CloseInventory(GearInventory);
			ClientAPI.Network.SendEntityPacket(EntityId, 1506);
			gearDialog?.Dispose();
			gearDialog = null;
			PlayEntitySound("leave");
		}

		public virtual void ReadInventoryFromAttributes() {
			ITreeAttribute treeAttribute = WatchedAttributes["inventory"] as ITreeAttribute;
			if (gearInv != null && treeAttribute != null) {
				gearInv.FromTreeAttributes(treeAttribute);
			}
			(Properties.Client.Renderer as EntitySkinnableShapeRenderer)?.MarkShapeModified();
		}

		public virtual void UpdateStats(int? slotId = null) {
			if (Api.Side == EnumAppSide.Client) { return; }
			try {
				bool updateAll = (slotId == null);
				if (updateAll || slotId == 16 || slotId == 18) {
					bool hasWeapon = !RightHandItemSlot.Empty;
					cachedData.usesRange = hasWeapon && AllowedWeaponry.Contains(RightHandItemSlot.Itemstack.Item.Code.FirstCodePart());
					cachedData.usesMelee = !hasWeapon || RightHandItemSlot?.Itemstack?.Collectible?.DamagedBy?.Length > 0;
					cachedData.UpdateWeapons(this.GearInventory);
					cachedData.UpdateReloads(this.GearInventory);
					pathfinder.curIdleAnims = cachedData.idleAnims;
					pathfinder.curWalkAnims = cachedData.walkAnims;
					pathfinder.curMoveAnims = cachedData.moveAnims;
					pathfinder.curSwimAnims = cachedData.swimAnims;
					cachedData.weapRange = hasWeapon ? RightHandItemSlot.Itemstack.Collectible.AttackRange : GlobalConstants.DefaultAttackRange;
					cachedData.recruitINFO = new string(hasWeapon ? "ENLISTED" : Properties.Attributes["baseState"].AsString("CIVILIAN"));
				}
				if (updateAll || (slotId != 16 && slotId != 18)) {
					float massTotal = 0;
					float healTotal = 0;
					float walkSpeed = Properties.Attributes["walkSpeed"].AsFloat(0.020f);
					float moveSpeed = Properties.Attributes["moveSpeed"].AsFloat(0.040f);
					for (int i = 0; i < gearInv.Count; i++) {
						if (!gearInv[i].Empty && gearInv[i].Itemstack.Item is ItemWearable armor) {
							massTotal += (armor.StatModifers?.walkSpeed ?? 0);
							healTotal += (armor.StatModifers?.healingeffectivness ?? 0);
						}
					}
					cachedData.healRates = Math.Clamp(1f - (1f * healTotal), 0f, 2f);
					cachedData.walkSpeed = Math.Clamp(walkSpeed + (walkSpeed * massTotal), 0f, walkSpeed * 2f);
					cachedData.moveSpeed = Math.Clamp(moveSpeed + (moveSpeed * massTotal), 0f, moveSpeed * 2f);
				}
				if (updateAll) {
					if (Api.World.BlockAccessor.GetBlockEntity(WatchedAttributes.GetBlockPos("postBlock")) is BlockEntityPost post) {
						WatchedAttributes.SetDouble("postRange", cachedData.postRange = (float)post.areasize);
					} else {
						WatchedAttributes.SetDouble("postRange", cachedData.postRange = 6f);
						WatchedAttributes.SetBlockPos("postBlock", ServerPos.AsBlockPos.Copy());
					}
				}
			} catch (NullReferenceException e) {
				World.Logger.Error(e.ToString());
			}
		}
		
		public virtual void UpdateInfos(byte[] data) {
			SentryUpdateToEntity update = SerializerUtil.Deserialize<SentryUpdateToEntity>(data);
			if (update.kingdomGUID != null) { WatchedAttributes.SetString(KingdomGUID, new string(update.kingdomGUID)); }
			if (update.cultureGUID != null) { WatchedAttributes.SetString(CultureGUID, new string(update.cultureGUID)); }
			if (update.leadersGUID != null) { WatchedAttributes.SetString(LeadersGUID, new string(update.leadersGUID)); }
			if (Api.Side == EnumAppSide.Client) {
				clientData.UpdateLoyalty(update.kingdomNAME, update.cultureNAME, update.leadersNAME);
				clientData.UpdateColours(update.coloursHEXA, update.coloursHEXB, update.coloursHEXC);
			}
		}

		public virtual void UpdateTrees(byte[] data) {
			TreeAttribute tree = new TreeAttribute();
			SerializerUtil.FromBytes(data, (r) => tree.FromBytes(r));
			gearInv.FromTreeAttributes(tree);
			foreach (var slot in gearInv) {
				slot.OnItemSlotModified(slot.Itemstack);
			}
		}

		public virtual void UpdateTasks() {
			ruleOrder = new bool[7] {
				WatchedAttributes.GetBool(OrderWander),
				WatchedAttributes.GetBool(OrderFollow),
				WatchedAttributes.GetBool(OrderEngage),
				WatchedAttributes.GetBool(OrderPursue),
				WatchedAttributes.GetBool(OrderShifts),
				WatchedAttributes.GetBool(OrderPatrol),
				WatchedAttributes.GetBool(OrderReturn)
			};
		}

		public virtual void Skeletalize() {
			if (Api.Side != EnumAppSide.Client && !Alive && gearInv.Empty && HasBehavior<EntityBehaviorDecayBody>()) {
				GetBehavior<EntityBehaviorDecayBody>()?.DecayNow(this);
			}
		}
	}
}