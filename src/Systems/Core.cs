using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class VSKingdom : ModSystem {
		Harmony harmony = new Harmony("badrabbit49.vskingdom");
		public ICoreClientAPI clientAPI;
		public ICoreServerAPI serverAPI;
		public string serverLang { get; set; }

		private List<Kingdom> kingdomList;
		private List<Culture> cultureList;

		public override void Start(ICoreAPI api) {
			base.Start(api);
			// Block Classes
			api.RegisterBlockClass("BlockBody", typeof(BlockBody));
			api.RegisterBlockClass("BlockPost", typeof(BlockPost));
			// Block Behaviors //
			api.RegisterBlockEntityBehaviorClass("Resupply", typeof(BlockBehaviorResupply));
			// Block Entities //
			api.RegisterBlockEntityClass("BlockEntityBody", typeof(BlockEntityBody));
			api.RegisterBlockEntityClass("BlockEntityPost", typeof(BlockEntityPost));
			// Items //
			api.RegisterItemClass("ItemBanner", typeof(ItemBanner));
			api.RegisterItemClass("ItemPeople", typeof(ItemPeople));
			// Entities //
			api.RegisterEntity("EntitySentry", typeof(EntitySentry));
			// Entity Behaviors //
			api.RegisterEntityBehaviorClass("KingdomFullNames", typeof(EntityBehaviorFullNames));
			api.RegisterEntityBehaviorClass("KingdomLoyalties", typeof(EntityBehaviorLoyalties));
			api.RegisterEntityBehaviorClass("SoldierDecayBody", typeof(EntityBehaviorDecayBody));
			// AITasks //
			AiTaskRegistry.Register<AiTaskSentryAttack>("SentryAttack");
			AiTaskRegistry.Register<AiTaskSentryEscape>("SentryEscape");
			AiTaskRegistry.Register<AiTaskSentryFollow>("SentryFollow");
			AiTaskRegistry.Register<AiTaskSentryHealth>("SentryHealth");
			AiTaskRegistry.Register<AiTaskSentryIdling>("SentryIdling");
			AiTaskRegistry.Register<AiTaskSentryRanged>("SentryRanged");
			AiTaskRegistry.Register<AiTaskSentryReturn>("SentryReturn");
			AiTaskRegistry.Register<AiTaskSentrySearch>("SentrySearch");
			AiTaskRegistry.Register<AiTaskSentryWander>("SentryWander");
			AiTaskRegistry.Register<AiTaskSentryWaters>("SentryWaters");
			// Patches //
			if (!Harmony.HasAnyPatches("badrabbit49.vskingdom")) {
				harmony.PatchAll();
			}
			// Create chat commands for creation, deletion, invitation, and so of kingdoms.
			api.ChatCommands.Create("kingdom")
				.RequiresPrivilege(Privilege.chat)
				.RequiresPlayer()
				.WithArgs(api.ChatCommands.Parsers.Word("commands", GlobalCodes.kingdomCommands), api.ChatCommands.Parsers.OptionalAll("argument"))
				.HandleWith(new OnCommandDelegate(OnKingdomCommand));
			// Create chat commands for creation, deletion, invitation, and so of cultures.
			api.ChatCommands.Create("culture")
				.RequiresPrivilege(Privilege.chat)
				.RequiresPlayer()
				.WithArgs(api.ChatCommands.Parsers.Word("commands", GlobalCodes.cultureCommands), api.ChatCommands.Parsers.OptionalAll("argument"))
				.HandleWith(new OnCommandDelegate(OnCultureCommand));
		}

		public override void StartClientSide(ICoreClientAPI capi) {
			base.StartClientSide(capi);
			this.clientAPI = capi;
			capi.Event.LevelFinalize += () => LevelFinalize(capi);
			capi.Network.RegisterChannel("sentrynetwork")
				.RegisterMessageType<PlayerUpdate>()
				.RegisterMessageType<SentryUpdate>().SetMessageHandler<SentryUpdate>(OnSentryUpdated)
				.RegisterMessageType<SentryOrders>().SetMessageHandler<SentryOrders>(OnSentryOrdered);
		}

		public override void StartServerSide(ICoreServerAPI sapi) {
			base.StartServerSide(sapi);
			this.serverAPI = sapi;
			this.serverLang = sapi.World.Config.GetString("ServerLanguage", "en");
			sapi.Event.SaveGameCreated += MakeAllData;
			sapi.Event.GameWorldSave += SaveAllData;
			sapi.Event.SaveGameLoaded += LoadAllData;
			sapi.Event.PlayerJoin += PlayerJoinsGame;
			sapi.Event.PlayerDisconnect += PlayerLeaveGame;
			sapi.Event.PlayerDeath += PlayerDeathFrom;
			sapi.Event.ServerRunPhase(EnumServerRunPhase.Shutdown, CleanupData);
			sapi.Network.RegisterChannel("sentrynetwork")
				.RegisterMessageType<PlayerUpdate>().SetMessageHandler<PlayerUpdate>(OnPlayerUpdated)
				.RegisterMessageType<SentryUpdate>().SetMessageHandler<SentryUpdate>(OnSentryUpdated)
				.RegisterMessageType<SentryOrders>().SetMessageHandler<SentryOrders>(OnSentryOrdered);
		}

		public override void Dispose() {
			harmony?.UnpatchAll(Mod.Info.ModID);
			base.Dispose();
		}

		private void MakeAllData() {
			byte[] kingdomData = serverAPI.WorldManager.SaveGame.GetData("kingdomData");
			byte[] cultureData = serverAPI.WorldManager.SaveGame.GetData("cultureData");
			kingdomList = kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);
			cultureList = cultureData is null ? new List<Culture>() : SerializerUtil.Deserialize<List<Culture>>(cultureData);
			if (!kingdomList.Exists(kingdomMatch => kingdomMatch.KingdomGUID == GlobalCodes.commonerGUID)) {
				CreateKingdom(GlobalCodes.commonerGUID, Lang.GetL(serverLang, "vskingdom:entries-keyword-common"), null, false);
				var commoners = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == GlobalCodes.commonerGUID);
				commoners.KingdomHEXA = "#383838";
				commoners.KingdomHEXB = "#b5b5b5";
				commoners.KingdomHEXC = "#ffffff";
				commoners.MembersROLE = "Commoner/T/T/T/T/F/F";
				SaveKingdom();
			}
			if (!kingdomList.Exists(kingdomMatch => kingdomMatch.KingdomGUID == GlobalCodes.banditryGUID)) {
				CreateKingdom(GlobalCodes.banditryGUID, Lang.GetL(serverLang, "vskingdom:entries-keyword-bandit"), null, false);
				var banditrys = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == GlobalCodes.banditryGUID);
				banditrys.KingdomHEXA = "#322211";
				banditrys.KingdomHEXB = "#0c0c0c";
				banditrys.KingdomHEXC = "#ff9300";
				banditrys.MembersROLE = "Banditry/T/T/T/T/F/F";
				SaveKingdom();
			}
			if (!cultureList.Exists(cultureMatch => cultureMatch.CultureGUID.Contains(GlobalCodes.seraphimGUID))) {
				CreateCulture(GlobalCodes.seraphimGUID, Lang.GetL(serverLang, "vskingdom:entries-keyword-seraph"), null, false);
			}
			if (!cultureList.Exists(cultureMatch => cultureMatch.CultureGUID.Contains(GlobalCodes.clockwinGUID))) {
				CreateCulture(GlobalCodes.clockwinGUID, Lang.GetL(serverLang, "vskingdom:entries-keyword-clocks"), null, false);
			}
		}

		private void SaveKingdom() {
			serverAPI.WorldManager.SaveGame.StoreData("kingdomData", SerializerUtil.Serialize(kingdomList));
		}

		private void SaveCulture() {
			serverAPI.WorldManager.SaveGame.StoreData("cultureData", SerializerUtil.Serialize(cultureList));
		}

		private void SaveAllData() {
			serverAPI.WorldManager.SaveGame.StoreData("kingdomData", SerializerUtil.Serialize(kingdomList));
			serverAPI.WorldManager.SaveGame.StoreData("cultureData", SerializerUtil.Serialize(cultureList));
		}

		private void LoadAllData() {
			byte[] kingdomData = serverAPI.WorldManager.SaveGame.GetData("kingdomData");
			byte[] cultureData = serverAPI.WorldManager.SaveGame.GetData("cultureData");
			kingdomList = kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);
			cultureList = cultureData is null ? new List<Culture>() : SerializerUtil.Deserialize<List<Culture>>(cultureData);
		}

		private void CleanupData() {
			List<string> markedForDeletion = new List<string>();
			foreach (var kingdom in kingdomList) {
				var enemies = kingdom.EnemiesGUID.ToArray();
				foreach (var enemy in enemies) {
					if (!KingdomExists(enemy)) {
						kingdom.EnemiesGUID.Remove(enemy);
					}
				}
				if (kingdom.PlayersGUID.Count == 0 && GlobalCodes.kingdomIDs.Contains(kingdom.KingdomGUID) == false) {
					markedForDeletion.Add(kingdom.KingdomGUID);
				}
			}
			if (markedForDeletion.Count > 0) {
				foreach (var marked in markedForDeletion) {
					DeleteKingdom(marked);
				}
			}
			SaveAllData();
		}

		private void LevelFinalize(ICoreClientAPI capi) {
			capi.Gui.Icons.CustomIcons["backpack"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/backpack.svg"));
			capi.Gui.Icons.CustomIcons["baguette"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/baguette.svg"));
			capi.Gui.Icons.CustomIcons["bandages"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/bandages.svg"));
			capi.Gui.Icons.CustomIcons["munition"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/munition.svg"));
			capi.Gui.Icons.CustomIcons["shieldry"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/shieldry.svg"));
			capi.Gui.Icons.CustomIcons["weaponry"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/weaponry.svg"));
			capi.Gui.Icons.CustomIcons["longbows"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/longbows.svg"));
		}

		private void PlayerJoinsGame(IServerPlayer player) {
			string kingdomGUID = player.Entity.WatchedAttributes.GetOrAddTreeAttribute("loyalties")?.GetString("kingdom_guid");
			string cultureGUID = player.Entity.WatchedAttributes.GetOrAddTreeAttribute("loyalties")?.GetString("culture_guid");
			if (kingdomGUID is null || kingdomGUID == "" || KingdomExists(kingdomGUID) == false) {
				serverAPI.Logger.Error(LangUtility.RefL(serverLang, "command-error-cantfind", "entries-keyword-kingdom"));
				SwitchKingdom(player, GlobalCodes.commonerGUID, "Commoner");
			}
			if (cultureGUID is null || cultureGUID == "" || CultureExists(cultureGUID) == false) {
				serverAPI.Logger.Error(LangUtility.RefL(serverLang, "command-error-cantfind", "entries-keyword-culture"));
				SwitchCulture(player, GlobalCodes.commonerGUID);
			}
			UpdateSentries(player.Entity.ServerPos.XYZ);
		}

		private void PlayerLeaveGame(IServerPlayer player) {
			try {
				if (player.Entity.WatchedAttributes.GetOrAddTreeAttribute("loyalties").HasAttribute("kingdom_guid")) {
					serverAPI.Logger.Notification($"{player.PlayerName} was member of {GetKingdomNAME(player.Entity.WatchedAttributes.GetTreeAttribute("loyalties").GetString("kingdom_guid"))}, unloading data.");
					UpdateSentries(player.Entity.ServerPos.XYZ);
				}
			} catch (NullReferenceException) {
				serverAPI.Logger.Error($"{player.PlayerName} didn't have a kingdomGUID string.");
			}
			SaveAllData();
		}

		private void PlayerDeathFrom(IServerPlayer player, DamageSource damage) {
			if (serverAPI.World.Config.GetAsBool("DropsOnDeath") && damage.GetCauseEntity().WatchedAttributes.HasAttribute("loyalties")) {
				var killer = damage.GetCauseEntity();
				var victim = player.Entity;
				var thatKingdom = killer.WatchedAttributes.GetTreeAttribute("loyalties").GetString("kingdom_guid", GlobalCodes.commonerGUID);
				var thisKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == victim.WatchedAttributes.GetTreeAttribute("loyalties").GetString("kingdom_guid", GlobalCodes.commonerGUID));
				// If the entities were at war with eachother then loot will be dropped. Specifically their armor and what they had in their right hand slot.
				if (thisKingdom.EnemiesGUID.Contains(thatKingdom) || thatKingdom == GlobalCodes.banditryGUID) {
					// If the killer can, try looting the player corpse right away, take what is better.
					if (serverAPI.World.Config.GetAsBool("AllowLooting") && killer is EntitySentry sentry) {
						for (int i = 12; i < 14; i++) {
							if (victim.GearInventory[i].Empty) {
								continue;
							}
							float ownGearDmgRed = (sentry.GearInventory[i]?.Itemstack?.Item as ItemWearable)?.ProtectionModifiers.FlatDamageReduction ?? 0f;
							if (victim.GearInventory[i].Itemstack.Item is ItemWearable gear && gear.ProtectionModifiers.FlatDamageReduction > ownGearDmgRed) {
								try {
									victim.GearInventory[i].TryFlipWith(sentry.gearInv[i]);
									sentry.GearInvSlotModified(i);
								} catch { }
							}
						}
						foreach (var slot in victim.Player.InventoryManager.GetHotbarInventory()) {
							if (slot.Empty) {
								continue;
							} else if (sentry.cachedData.recruitINFO[0] != "range" && slot.Itemstack.Item is ItemBow) {
								continue;
							} else if (sentry.cachedData.recruitINFO[0] != "melee" && slot.Itemstack.Item is not ItemBow) {
								continue;
							}
							ItemStack victimWeapon = victim.RightHandItemSlot?.Itemstack ?? null;
							ItemStack killerWeapon = sentry.RightHandItemSlot?.Itemstack ?? null;
							float victimWeapValue = victimWeapon != null ? (victimWeapon?.Collectible.Durability ?? 1f) * (victimWeapon?.Collectible.AttackPower ?? victimWeapon?.Collectible.Attributes?["damage"].AsFloat() ?? 1f) : 0f;
							float killerWeapValue = killerWeapon != null ? (killerWeapon?.Collectible.Durability ?? 1f) * (killerWeapon?.Collectible.AttackPower ?? killerWeapon?.Collectible.Attributes?["damage"].AsFloat() ?? 1f) : 0f;
							if (victimWeapValue > killerWeapValue) {
								slot.TryFlipWith(sentry.gearInv[16]);
								sentry.GearInvSlotModified(16);
							}
						}
					}

					var blockAccessor = victim.World.BlockAccessor;
					double x = victim.ServerPos.X + victim.SelectionBox.X1 - victim.OriginSelectionBox.X1;
					double y = victim.ServerPos.Y + victim.SelectionBox.Y1 - victim.OriginSelectionBox.Y1;
					double z = victim.ServerPos.Z + victim.SelectionBox.Z1 - victim.OriginSelectionBox.Z1;
					double d = victim.ServerPos.Dimension;

					BlockPos bonePos = new BlockPos((int)x, (int)y, (int)z, (int)d);
					string[] SkeletonBodies = new string[] { "humanoid1", "humanoid2" };
					Block skeletonBlock = serverAPI.World.GetBlock(new AssetLocation(LangUtility.Get("body-" + SkeletonBodies[serverAPI.World.Rand.Next(0, SkeletonBodies.Length)])));
					Block exblock = blockAccessor.GetBlock(bonePos);
					bool placedBlock = false;
					// Ensure the blocks here are replaceable like grass or something.
					if (exblock.IsReplacableBy(new BlockRequireSolidGround())) {
						blockAccessor.SetBlock(skeletonBlock.BlockId, bonePos);
						blockAccessor.MarkBlockDirty(bonePos);
						placedBlock = true;
					} else {
						foreach (BlockFacing facing in BlockFacing.HORIZONTALS) {
							facing.IterateThruFacingOffsets(bonePos);
							exblock = blockAccessor.GetBlock(bonePos);
							if (exblock.IsReplacableBy(new BlockRequireSolidGround())) {
								blockAccessor.SetBlock(skeletonBlock.BlockId, bonePos);
								blockAccessor.MarkBlockDirty(bonePos);
								placedBlock = true;
								break;
							}
						}
					}
					// Spawn the body block here if it was placed, drop all items if not possible.
					if (placedBlock && victim.WatchedAttributes.HasAttribute("inventory")) {
						// Initialize BlockEntityBody here and put stuff into it.
						if (blockAccessor.GetBlockEntity(bonePos) is BlockEntityBody decblock) {
							// Get the inventory of the person who died if they have one.
							for (int i = 12; i < 14; i++) {
								try { victim.GearInventory[i].TryPutInto(serverAPI.World, decblock.gearInv[i], victim.GearInventory[i].StackSize); } catch { }
							}
							if (!victim.RightHandItemSlot.Empty && victim.RightHandItemSlot.Itemstack.Collectible.Attributes["toolrackTransform"].Exists) {
								try { victim.RightHandItemSlot.TryPutInto(serverAPI.World, decblock.gearInv[16], 1); } catch { }
							}
						}
					} else {
						foreach (ItemSlot item in victim.GearInventory) {
							if (!item.Empty) {
								serverAPI.World.SpawnItemEntity(item.Itemstack, victim.ServerPos.XYZ);
								item.Itemstack = null;
								item.MarkDirty();
							}
						}
					}
				}
			}
		}

		private void UpdateSentries(Vec3d position) {
			var nearbyEnts = serverAPI.World.GetEntitiesAround(position, 200, 60, (ent => (ent is EntitySentry)));
			var bestPlayer = serverAPI.World.NearestPlayer(position.X, position.Y, position.Z) as IServerPlayer;
			foreach (var sentry in nearbyEnts) {
				OnSentryUpdated(bestPlayer, new SentryUpdate() {
					playerUID = bestPlayer.Entity.EntityId,
					entityUID = sentry.EntityId,
					kingdomINFO = new string[2] { sentry.WatchedAttributes.GetTreeAttribute("loyalties").GetString("kingdom_guid"), null },
					cultureINFO = new string[2] { sentry.WatchedAttributes.GetTreeAttribute("loyalties").GetString("culture_guid"), null },
					leadersINFO = new string[2] { sentry.WatchedAttributes.GetTreeAttribute("loyalties").GetString("leaders_guid"), null }
				});
			}
		}

		private void OnPlayerUpdated(IServerPlayer player, PlayerUpdate playerUpdate) {
			ITreeAttribute loyalties = player.Entity.WatchedAttributes.GetTreeAttribute("loyalties");
			if (playerUpdate.kingdomID != null && KingdomExists(playerUpdate.kingdomID)) {
				loyalties.SetString("kingdom_guid", playerUpdate.kingdomID);
				player.Entity.WatchedAttributes.MarkPathDirty("loyalties");
			}
			if (playerUpdate.cultureID != null && CultureExists(playerUpdate.cultureID)) {
				loyalties.SetString("culture_guid", playerUpdate.kingdomID);
				player.Entity.WatchedAttributes.MarkPathDirty("loyalties");
			}
			if (!player.Entity.WatchedAttributes.HasAttribute("followerEntityUids")) {
				player.Entity.WatchedAttributes.SetAttribute("followerEntityUids", new LongArrayAttribute(playerUpdate.followers));
				return;
			}
			if (playerUpdate.followers != null && playerUpdate.followers.Length != 0) {
				if (playerUpdate.operation == null) {
					playerUpdate.operation = "add";
				}
				bool[] operation = new bool[] { playerUpdate.operation == "add", playerUpdate.operation == "del" };
				long[] followers = (player.Entity.WatchedAttributes.GetAttribute("followerEntityUids") as LongArrayAttribute)?.value;
				List<long> currentFollowers = new List<long>();
				for (int i = 0; i < followers.Length; i++) {
					var entity = serverAPI.World.GetEntityById(followers[i]);
					if (entity == null || entity is not EntitySentry) {
						continue;
					}
					bool follow = entity.Alive && !(operation[1] && playerUpdate.followers.Contains(followers[i])) && entity.ServerPos.DistanceTo(player.Entity.ServerPos) < 20f && MathUtility.CanSeeEnt(entity, player.Entity);
					entity.WatchedAttributes.SetBool("orderFollow", follow);
					entity.WatchedAttributes.SetLong("guardedEntityId", follow ? player.Entity.EntityId : 0);
					((EntitySentry)entity).ruleOrder[1] = follow;
					if (follow) {
						currentFollowers.Add(followers[i]);
					}
				}
				player.Entity.WatchedAttributes.SetAttribute("followerEntityUids", new LongArrayAttribute(currentFollowers.ToArray<long>()));
			}
		}

		private void OnSentryUpdated(SentryUpdate sentryUpdate) {
			// Do nothing...
		}

		private void OnSentryUpdated(IServerPlayer player, SentryUpdate sentryUpdate) {
			EntitySentry sentry = serverAPI.World.GetEntityById(sentryUpdate.entityUID) as EntitySentry;
			SentryUpdate update = new SentryUpdate();

			// NEEDS TO BE DONE ON THE SERVER SIDE HERE.
			if (sentryUpdate.kingdomINFO.Length > 0 && sentryUpdate.kingdomINFO[0] != null) {
				var kingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == sentryUpdate.kingdomINFO[0]);
				sentry.cachedData.kingdomGUID = kingdom.KingdomGUID;
				sentry.cachedData.kingdomNAME = kingdom.KingdomNAME;
				sentry.cachedData.enemiesLIST = kingdom.EnemiesGUID.ToArray();
				sentry.cachedData.friendsLIST = kingdom.FriendsGUID.ToArray();
				sentry.cachedData.outlawsLIST = kingdom.OutlawsGUID.ToArray();
				update.kingdomINFO = new string[2] { kingdom.KingdomGUID, kingdom.KingdomNAME };
				update.coloursLIST = new string[3] { kingdom.KingdomHEXA, kingdom.KingdomHEXB, kingdom.KingdomHEXC };
				update.enemiesLIST = kingdom.EnemiesGUID.ToArray();
				update.friendsLIST = kingdom.FriendsGUID.ToArray();
				update.outlawsLIST = kingdom.OutlawsGUID.ToArray();
			}
			if (sentryUpdate.cultureINFO.Length > 0 && sentryUpdate.cultureINFO[0] != null) {
				var culture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == sentryUpdate.cultureINFO[0]);
				sentry.cachedData.cultureGUID = culture.CultureGUID;
				sentry.cachedData.cultureNAME = culture.CultureNAME;
				update.cultureINFO = new string[2] { culture.CultureGUID, culture.CultureNAME };
			}
			if (sentryUpdate.leadersINFO.Length > 0 && sentryUpdate.leadersINFO[0] != null) {
				var leaders = serverAPI.World.PlayerByUid(sentryUpdate?.leadersINFO[0]) ?? null;
				sentry.cachedData.leadersGUID = leaders.PlayerUID;
				sentry.cachedData.leadersNAME = leaders.PlayerName;
				update.leadersINFO = new string[2] { leaders.PlayerUID, leaders.PlayerName };
			}
			// Try to fully sync entity packet to the sentry if possible.
			serverAPI.Network.SendEntityPacket(player, update.entityUID, 1502, SerializerUtil.Serialize<SentryUpdate>(update));
		}

		private void OnSentryOrdered(SentryOrders sentryOrders) {
			EntityPlayer player = serverAPI.World.GetEntityById(sentryOrders.playerUID) as EntityPlayer;
			OnSentryOrdered(player.Player as IServerPlayer, sentryOrders);
		}

		private void OnSentryOrdered(IServerPlayer player, SentryOrders sentryOrders) {
			EntitySentry sentry = serverAPI.World.GetEntityById(sentryOrders.entityUID) as EntitySentry;
			// WATCHED VARIABLES ONLY CAN BE SET FROM SERVER (I.E. HERE).
			bool[] oldOrders = {
				sentry.WatchedAttributes.GetBool("orderWander"),
				sentry.WatchedAttributes.GetBool("orderFollow"),
				sentry.WatchedAttributes.GetBool("orderEngage"),
				sentry.WatchedAttributes.GetBool("orderPursue"),
				sentry.WatchedAttributes.GetBool("orderShifts"),
				sentry.WatchedAttributes.GetBool("orderPatrol"),
				sentry.WatchedAttributes.GetBool("orderReturn")
			};
			bool[] newOrders = {
				sentryOrders.wandering ?? oldOrders[0],
				sentryOrders.following ?? oldOrders[1],
				sentryOrders.attacking ?? oldOrders[2],
				sentryOrders.pursueing ?? oldOrders[3],
				sentryOrders.shifttime ?? oldOrders[4],
				sentryOrders.patroling ?? oldOrders[5],
				sentryOrders.returning ?? oldOrders[6]
			};
			// Wander:0 / Follow:1 / Engage:2 / Pursue:3 / Shifts:4 / Patrol:5 / Return:6 //
			sentry.ruleOrder = newOrders;
			sentry.WatchedAttributes.SetBool("orderWander", sentryOrders.wandering ?? oldOrders[0]);
			sentry.WatchedAttributes.SetBool("orderFollow", sentryOrders.following ?? oldOrders[1]);
			sentry.WatchedAttributes.SetBool("orderEngage", sentryOrders.attacking ?? oldOrders[2]);
			sentry.WatchedAttributes.SetBool("orderPursue", sentryOrders.pursueing ?? oldOrders[3]);
			sentry.WatchedAttributes.SetBool("orderShifts", sentryOrders.patroling ?? oldOrders[4]);
			sentry.WatchedAttributes.SetBool("orderPatrol", sentryOrders.shifttime ?? oldOrders[5]);
			sentry.WatchedAttributes.SetBool("orderReturn", sentryOrders.returning ?? oldOrders[6]);
			// Additional arguments go here broken up by commas as: (type), (name), (value).
			if (sentryOrders.attribute != null) {
				try {
					string[] attribute = sentryOrders.attribute.Replace(" ", "").Split(',');
					switch (attribute[0]) {
						case "b": sentry.WatchedAttributes.SetBool(attribute[1], bool.Parse(attribute[2])); break;
						case "i": sentry.WatchedAttributes.SetInt(attribute[1], int.Parse(attribute[2])); break;
						case "l": sentry.WatchedAttributes.SetLong(attribute[1], long.Parse(attribute[2])); break;
						case "f": sentry.WatchedAttributes.SetFloat(attribute[1], float.Parse(attribute[2])); break;
						case "d": sentry.WatchedAttributes.SetDouble(attribute[1], double.Parse(attribute[2])); break;
						case "p": sentry.WatchedAttributes.SetBlockPos(attribute[1], new BlockPos(int.Parse(attribute[2]), int.Parse(attribute[3]), int.Parse(attribute[4]), int.Parse(attribute[5]))); break;
						case "v": sentry.WatchedAttributes.SetVec3i(attribute[1], new Vec3i(int.Parse(attribute[2]), int.Parse(attribute[3]), int.Parse(attribute[4]))); break;
						case "s": sentry.WatchedAttributes.SetString(attribute[1], attribute[2]); break;
					}
				} catch { }
			}
			if (sentryOrders.usedorder) {
				PlayerUpdate newUpdate = new PlayerUpdate() { followers = new long[] { sentry.EntityId }, operation = (newOrders[1] ? "add" : "del") };
				OnPlayerUpdated(player, newUpdate);
			}
		}

		private TextCommandResult OnKingdomCommand(TextCommandCallingArgs args) {
			string fullargs = (string)args[1];
			string callerID = args.Caller.Player.PlayerUID;
			string langCode = args.LanguageCode;
			IPlayer thisPlayer = args.Caller.Player;
			IPlayer thatPlayer = serverAPI.World.PlayerByUid(serverAPI.PlayerData.GetPlayerDataByLastKnownName(fullargs)?.PlayerUID) ?? null;
			Kingdom thisKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thisPlayer.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid")) ?? null;
			Kingdom thatKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomNAME.ToLowerInvariant() == fullargs?.ToLowerInvariant()) ?? null;
			// Determine privillege role level and if they are allowed to make new kingdoms/cultures.
			bool inKingdom = thisKingdom != null && thisKingdom.KingdomGUID != GlobalCodes.commonerGUID;
			bool theLeader = inKingdom && thisKingdom.LeadersGUID == callerID;
			bool usingArgs = fullargs != null && fullargs != "" && fullargs != " ";
			bool canInvite = inKingdom && KingUtility.GetRolesPRIV(thisKingdom.MembersROLE, KingUtility.GetMemberROLE(thisKingdom.PlayersINFO, callerID))[5];
			bool adminPass = args.Caller.HasPrivilege(Privilege.controlserver) || thisPlayer.PlayerName == "BadRabbit49";
			bool canCreate = args.Caller.GetRole(serverAPI).PrivilegeLevel >= serverAPI.World.Config.GetAsInt("MinCreateLevel", -1);
			bool maxCreate = serverAPI.World.Config.GetInt("MaxNewKingdoms", -1) != -1 || serverAPI.World.Config.GetInt("MaxNewKingdoms", -1) < (kingdomList.Count + 1);

			string[] keywords = {
				LangUtility.GetL(langCode, "entries-keyword-kingdom"),
				LangUtility.GetL(langCode, "entries-keyword-player"),
				LangUtility.GetL(langCode, "entries-keyword-players")
			};

			if (adminPass && inKingdom) {
				try { serverAPI.Logger.Debug(KingUtility.ListedAllData(serverAPI, thisKingdom.KingdomGUID)); } catch { }
			}

			switch ((string)args[0]) {
				// Creates new owned Kingdom.
				case "create":
					if (!usingArgs) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-argsnone", string.Concat(args.Command.ToString(), (string)args[0])));
					} else if (NameTaken(fullargs)) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-nametook", keywords[0]));
					} else if (InKingdom(callerID)) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-ismember", keywords[0]));
					} else if (!canCreate) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-badperms", (string)args[0]));
					} else if (!maxCreate) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-capacity", keywords[0]));
					}
					CreateKingdom(null, fullargs, thisPlayer.PlayerUID, !inKingdom);
					return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-create", fullargs));
				// Deletes the owned Kingdom.
				case "delete":
					if (usingArgs && adminPass && thatKingdom is not null) {
						DeleteKingdom(thatKingdom.KingdomGUID);
						return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-delete", fullargs));
					} else if (!usingArgs || thatKingdom is null) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-cantfind", keywords[0]));
					} else if (!adminPass && thatKingdom.LeadersGUID != callerID) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-badperms", (string)args[0]));
					}
					DeleteKingdom(thatKingdom.KingdomGUID);
					return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-delete", fullargs));
				// Updates and changes kingdom properties.
				case "update":
					if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (!usingArgs) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-argument", fullargs));
					} else if (!adminPass && thisKingdom.LeadersGUID != callerID) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-badperms", fullargs));
					}
					string[] fullset = { fullargs };
					try { fullset = fullargs.Split(' '); } catch { }
					string results = ChangeKingdom(langCode, thisKingdom.KingdomGUID, fullset[0], fullset[1], string.Join(' ', fullset.Skip(2)));
					thisPlayer.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/writing"), thisPlayer.Entity);
					return TextCommandResult.Success(results);
				// Invites player to join Kingdom.
				case "invite":
					/** TODO: THIS DOESN'T WANT TO PROPERLY SEND INVITES, DETERMINE IF THEY ARE GETTING THROUGH AND BEING SAVED. **/
					if (!usingArgs && canInvite && thisPlayer.CurrentEntitySelection.Entity is EntityPlayer playerEnt) {
						thisKingdom.InvitesGUID.Add(playerEnt.PlayerUID);
						SaveKingdom();
						serverAPI.SendMessage(thatPlayer, 0, LangUtility.SetL((thatPlayer as IServerPlayer)?.LanguageCode ?? langCode, "command-message-invite", thisPlayer.PlayerName, thisKingdom.KingdomNAME), EnumChatType.Notification);
						return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-invite", playerEnt.Player.PlayerName));
					} else if (thatPlayer == null || !serverAPI.World.AllOnlinePlayers.Contains(thatPlayer)) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-noplayer", fullargs));
					} else if (thisKingdom.PlayersGUID.Contains(thatPlayer.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-playerin", thisKingdom.KingdomNAME));
					} else if (!canInvite) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-badperms", (string)args[0]));
					}
					thisKingdom.InvitesGUID.Add(thatPlayer.PlayerUID);
					SaveKingdom();
					serverAPI.SendMessage(thatPlayer, 0, LangUtility.SetL((thatPlayer as IServerPlayer)?.LanguageCode ?? langCode, "command-message-invite", thisPlayer.PlayerName, thisKingdom.KingdomNAME), EnumChatType.Notification);
					return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-invite", fullargs));
				// Accept invites and requests.
				case "accept":
					if (!usingArgs) {
						return TextCommandResult.Success(KingdomInvite(callerID, canInvite));
					} else if (thatKingdom != null && inKingdom && theLeader && thisKingdom.PeaceOffers.Contains(thatKingdom.KingdomGUID)) {
						serverAPI.BroadcastMessageToAllGroups(LangUtility.SetL(langCode, "command-message-peacea", thisKingdom.KingdomLONG, thatKingdom.KingdomLONG), EnumChatType.Notification);
						EndWarKingdom(thisKingdom.KingdomGUID, thatKingdom.KingdomGUID);
						return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-treaty", fullargs));
					} else if (thatPlayer != null && inKingdom && thisKingdom.RequestGUID.Contains(thatPlayer.PlayerUID)) {
						if (!canInvite) {
							return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-badperms", string.Concat((string)args[0], thatPlayer.PlayerName)));
						}
						SwitchKingdom(thatPlayer as IServerPlayer, thisKingdom.KingdomGUID);
						return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-accept", thatPlayer.PlayerName));
					} else if (thatKingdom != null && thatKingdom.InvitesGUID.Contains(thisPlayer.PlayerUID)) {
						SwitchKingdom(thisPlayer as IServerPlayer, thatKingdom.KingdomGUID);
						var leadPlayer = serverAPI.World.PlayerByUid(thatKingdom.LeadersGUID);
						serverAPI.SendMessage(leadPlayer, 0, LangUtility.SetL((leadPlayer as IServerPlayer)?.LanguageCode ?? langCode, "command-message-accept", thisPlayer.PlayerName, thatKingdom.KingdomLONG), EnumChatType.Notification);
						return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-accept", thatPlayer.PlayerName));
					}
					return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-noinvite", keywords[0]));
				// Reject invites and requests.
				case "reject":
					if (!usingArgs) {
						return TextCommandResult.Success(KingdomInvite(callerID, canInvite));
					} else if (thatKingdom != null && inKingdom && theLeader && thisKingdom.PeaceOffers.Contains(thatKingdom.KingdomGUID)) {
						serverAPI.BroadcastMessageToAllGroups(LangUtility.SetL(langCode, "command-message-peacer", thisKingdom.KingdomLONG, thatKingdom.KingdomLONG), EnumChatType.Notification);
						kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thisKingdom.KingdomGUID).PeaceOffers.Remove(thatKingdom.KingdomGUID);
						SaveKingdom();
						return TextCommandResult.Success(LangUtility.SetL(langCode, "command-failure-treaty", fullargs));
					} else if (thatPlayer != null && inKingdom && thisKingdom.RequestGUID.Contains(thatPlayer.PlayerUID)) {
						if (!canInvite) {
							return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-badperms", string.Concat((string)args[0], thatPlayer.PlayerName)));
						}
						thisKingdom.RequestGUID.Remove(thatPlayer.PlayerUID);
						return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-reject", thatPlayer.PlayerName));
					} else if (thatKingdom != null && thatKingdom.InvitesGUID.Contains(thisPlayer.PlayerUID)) {
						thatKingdom.InvitesGUID.Remove(callerID);
						var leadPlayer = serverAPI.World.PlayerByUid(thatKingdom.LeadersGUID);
						serverAPI.SendMessage(leadPlayer, 0, LangUtility.SetL((leadPlayer as IServerPlayer)?.LanguageCode ?? langCode, "command-choices-reject", thisPlayer.PlayerName, thatKingdom.KingdomLONG), EnumChatType.Notification);
						return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-reject", thatPlayer.PlayerName));
					}
					return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-noinvite", keywords[0]));
				// Removes player from Kingdom.
				case "remove":
					if (thatPlayer == null || !serverAPI.World.AllOnlinePlayers.Contains(thatPlayer)) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-noplayer", fullargs));
					} else if (!thisKingdom.PlayersGUID.Contains(thatPlayer.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-nomember", thisKingdom.KingdomNAME));
					} else if (!adminPass && GetLeadersGUID(null, callerID) != callerID) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-badperms", string.Concat((string)args[0], thatPlayer.PlayerName)));
					}
					/** TODO: ADD SPECIAL CIRCUMSTANCE BASED ON PRIVILEGE AND ELECTIONS **/
					thisKingdom.PlayersINFO.Remove(KingUtility.GetMemberINFO(thisKingdom.PlayersINFO, thatPlayer.PlayerUID));
					thisKingdom.PlayersGUID.Remove(thatPlayer.PlayerUID);
					SaveKingdom();
					return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-remove", fullargs));
				// Requests access to Leader.
				case "become":
					if (!KingdomExists(null, fullargs)) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-notexist", keywords[0]));
					} else if (GetKingdom(null, fullargs).PlayersGUID.Contains(thisPlayer.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-ismember", keywords[0]));
					}
					/** TODO: ADD REQUEST TO JOIN TO QUERY thatKingdom.RequestGUID **/
					thatKingdom.RequestGUID.Add(thisPlayer.PlayerUID);
					SaveKingdom();
					serverAPI.SendMessage(serverAPI.World.PlayerByUid(thatKingdom.LeadersGUID), 0, LangUtility.Set("command-message-invite", thisPlayer.PlayerName, thisKingdom.KingdomNAME), EnumChatType.Notification);
					return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-become", fullargs));
				// Leaves current Kingdom.
				case "depart":
					if (!thisPlayer.Entity.HasBehavior<EntityBehaviorLoyalties>()) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-unknowns", (string)args[0]));
					} else if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (serverAPI.World.Claims.All.Any(landClaim => landClaim.OwnedByPlayerUid == thisPlayer.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-ownsland", keywords[0]));
					}
					string kingdomName = thisKingdom.KingdomNAME;
					SwitchKingdom(thisPlayer as IServerPlayer, GlobalCodes.commonerGUID);
					return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-depart", kingdomName));
				// Revolt against the Kingdom.
				case "revolt":
					if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (theLeader) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-isleader", (string)args[0]));
					}
					thisKingdom.OutlawsGUID.Add(callerID);
					SaveKingdom();
					return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-revolt", thisKingdom.KingdomNAME));
				// Rebels against the Kingdom.
				case "rebels":
					if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (theLeader) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-isleader", (string)args[0]));
					}
					thisKingdom.OutlawsGUID.Add(callerID);
					SwitchKingdom(thisPlayer as IServerPlayer, GlobalCodes.commonerGUID);
					return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-rebels", thisKingdom.KingdomNAME));
				// Declares war on Kingdom.
				case "attack":
					if (!IsKingdom(null, fullargs)) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-notexist", fullargs));
					} else if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (GetLeadersGUID(thisKingdom.KingdomGUID, callerID) != callerID) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-badperms", (string)args[0]));
					} else if (thisKingdom.EnemiesGUID.Contains(thatKingdom.KingdomGUID)) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-atwarnow", thatKingdom.KingdomNAME));
					} else {
						SetWarKingdom(thisKingdom.KingdomGUID, thatKingdom.KingdomGUID);
						return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-attack", fullargs));
					}
				// Declares peace to Kingdom.
				case "treaty":
					if (!IsKingdom(null, fullargs)) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-notexist", fullargs));
					} else if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (GetLeadersGUID(thisKingdom.KingdomGUID, callerID) != callerID) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-badperms", (string)args[0]));
					} else if (!thisKingdom.EnemiesGUID.Contains(thatKingdom.KingdomGUID)) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-notatwar", thatKingdom.KingdomNAME));
					}
					if (thisKingdom.PeaceOffers.Contains(thatKingdom.KingdomGUID) || GlobalCodes.kingdomIDs.Contains(thatKingdom.KingdomGUID)) {
						EndWarKingdom(thisKingdom.KingdomGUID, thatKingdom.KingdomGUID);
					} else {
						kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thatKingdom.KingdomGUID).PeaceOffers.Add(thisKingdom.KingdomGUID);
						SaveKingdom();
						serverAPI.BroadcastMessageToAllGroups(LangUtility.SetL(langCode, "command-message-peaces", thisKingdom.KingdomLONG, thatKingdom.KingdomLONG), EnumChatType.Notification);
					}
					return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-treaty", fullargs));
				// Sets an enemy of the Kingdom.
				case "outlaw":
					if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (thatPlayer == null || !serverAPI.World.AllOnlinePlayers.Contains(thatPlayer)) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-noplayer", fullargs));
					} else if (!theLeader) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-badperms", (string)args[0]));
					}
					thisKingdom.OutlawsGUID.Add(thatPlayer.PlayerUID);
					SaveKingdom();
					return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-outlaw", thatPlayer.PlayerName));
				// Pardons an enemy of the Kingdom.
				case "pardon":
					if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (thatPlayer == null || !serverAPI.World.AllOnlinePlayers.Contains(thatPlayer)) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-noplayer", fullargs));
					} else if (!theLeader) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-badperms", (string)args[0]));
					}
					thisKingdom.OutlawsGUID.Remove(thatPlayer.PlayerUID);
					SaveKingdom();
					return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-pardon", thatPlayer.PlayerName));
				// Gets all enemies of the Kingdom.
				case "wanted":
					if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-nopartof", keywords[0]));
					}
					return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-wanted", keywords[2], thisKingdom.KingdomLONG, KingdomWanted(thisKingdom.OutlawsGUID)));
			}
			if ((string)args[0] == null || ((string)args[0]).Contains("help")) {
				return TextCommandResult.Success(CommandInfo(langCode, "kingdom", "help"));
			}
			if (((string)args[0]).Contains("desc")) {
				return TextCommandResult.Success(CommandInfo(langCode, "kingdom", "desc"));
			}
			return TextCommandResult.Error(LangUtility.GetL(langCode, "command-help-kingdom"));
		}

		private TextCommandResult OnCultureCommand(TextCommandCallingArgs args) {
			string fullargs = (string)args[1];
			string callerID = args.Caller.Player.PlayerUID;
			string langCode = args.LanguageCode;
			IPlayer thisPlayer = args.Caller.Player;
			IPlayer thatPlayer = serverAPI.World.PlayerByUid(serverAPI.PlayerData.GetPlayerDataByLastKnownName(fullargs)?.PlayerUID) ?? null;
			Culture thisCulture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == thisPlayer.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("culture_guid")) ?? null;
			Culture thatCulture = cultureList.Find(cultureMatch => cultureMatch.CultureNAME.ToLowerInvariant() == fullargs?.ToLowerInvariant()) ?? null;
			// Determine privillege role level and if they are allowed to make new kingdoms/cultures.
			bool inCulture = thisCulture != null && thisCulture.CultureGUID != GlobalCodes.commonerGUID;
			bool usingArgs = fullargs != null && fullargs != "" && fullargs != " ";
			bool adminPass = args.Caller.HasPrivilege(Privilege.controlserver) || thisPlayer.PlayerName == "BadRabbit49";
			bool canCreate = args.Caller.GetRole(serverAPI).PrivilegeLevel >= serverAPI.World.Config.GetInt("MinCreateLevel", -1);
			bool maxCreate = serverAPI.World.Config.GetInt("MaxNewCultures", -1) != -1 && serverAPI.World.Config.GetInt("MaxNewCultures", -1) < (kingdomList.Count + 1);
			bool hoursTime = (serverAPI.World.Calendar.TotalHours - thisCulture.FoundedHOUR) > (serverAPI.World.Calendar.TotalHours - serverAPI.World.Config.GetInt("MinCultureMake"));

			string[] keywords = {
				LangUtility.GetL(langCode, "entries-keyword-culture"),
				LangUtility.GetL(langCode, "entries-keyword-player"),
				LangUtility.GetL(langCode, "entries-keyword-players")
			};

			switch ((string)args[0]) {
				// Creates brand new Culture.
				case "create":
					if (!usingArgs) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-argsnone", (string)args[0]));
					} else if (cultureList.Exists(cultureMatch => cultureMatch.CultureNAME.ToLowerInvariant() == fullargs?.ToLowerInvariant())) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-nametook", keywords[0]));
					} else if (!canCreate && !adminPass) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-badperms", (string)args[0]));
					} else if (!maxCreate && !adminPass) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-capacity", keywords[0]));
					} else if (!hoursTime && !adminPass) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-tooearly", (thisCulture.FoundedHOUR - serverAPI.World.Config.GetInt("MinCultureMake")).ToString()));
					}
					CreateCulture(null, fullargs.UcFirst(), callerID, !inCulture);
					return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-create", fullargs.UcFirst()));
				// Deletes existing culture.
				case "delete":
					if (!cultureList.Exists(cultureMatch => cultureMatch.CultureNAME.ToLowerInvariant() == fullargs?.ToLowerInvariant())) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (!adminPass) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-notadmin", (string)args[0]));
					}
					DeleteCulture(thatCulture.CultureGUID);
					return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-delete", fullargs));
				// Edits existing culture.
				case "update":
					if (!inCulture) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (!adminPass && thisCulture.FounderGUID != callerID) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-badperms", (string)args[0]));
					}
					thisPlayer.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/writing"), thisPlayer.Entity);
					string[] fullset = { fullargs };
					try { fullset = fullargs.Split(' '); } catch { }
					string results = ChangeCulture(langCode, thisCulture.CultureGUID, fullset[0], fullset[1], string.Join(' ', fullset.Skip(2)));
					return TextCommandResult.Success(results);
				// Invite a player into culture.
				case "invite":
					if (!usingArgs && thisPlayer.CurrentEntitySelection.Entity is EntityPlayer) {
						thatPlayer = (thisPlayer.CurrentEntitySelection.Entity as EntityPlayer)?.Player;
						thisCulture.InviteGUID.Add(thatPlayer.PlayerUID);
						SaveCulture();
						return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-invite", thatPlayer.PlayerName));
					} else if (!usingArgs) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-argsnone", keywords[1]));
					} else if (thatPlayer == null) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-noplayer", fullargs));
					}
					thisCulture.InviteGUID.Add(thatPlayer.PlayerUID);
					SaveCulture();
					return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-invite", thatPlayer.PlayerName));
				// Accept invite to join culture.
				case "accept":
					if (!usingArgs) {
						return TextCommandResult.Success(CultureInvite(callerID));
					} else if (thatCulture != null && thatCulture.InviteGUID.Contains(thisPlayer.PlayerUID)) {
						SwitchCulture(thisPlayer as IServerPlayer, thatCulture.CultureGUID);
						return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-accept", thatCulture.CultureNAME));
					}
					return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-noinvite", keywords[0]));
				// Reject invite to join culture.
				case "reject":
					if (!usingArgs) {
						return TextCommandResult.Success(CultureInvite(callerID));
					} else if (inCulture && thatPlayer != null && thisCulture.InviteGUID.Contains(thatPlayer.PlayerUID)) {
						thisCulture.InviteGUID.Remove(thatPlayer.PlayerUID);
						SaveCulture();
						return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-reject", thatPlayer.PlayerUID));
					} else if (thatCulture != null && thatCulture.InviteGUID.Contains(thisPlayer.PlayerUID)) {
						thatCulture.InviteGUID.Remove(thisPlayer.PlayerUID);
						SaveCulture();
						return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-reject", thatCulture.CultureNAME));
					}
					return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-noinvite", keywords[0]));
				case "remove":
					if (!adminPass) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-notadmin", (string)args[0]));
					} else if (!inCulture) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (thatPlayer == null) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-noplayer", fullargs));
					}
					thatPlayer.Entity.WatchedAttributes.GetTreeAttribute("loyalties").SetString("culture_guid", GlobalCodes.commonerGUID);
					return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-remove", thisCulture.CultureNAME));
				case "become":
					if (!adminPass) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-notadmin", (string)args[0]));
					} else if (thatCulture != null || !!cultureList.Exists(cultureMatch => cultureMatch.CultureNAME.ToLowerInvariant() == fullargs?.ToLowerInvariant())) {
						return TextCommandResult.Error(LangUtility.SetL(langCode, "command-error-notexist", keywords[0]));
					}
					SwitchCulture(thisPlayer as IServerPlayer, thatCulture.CultureGUID);
					return TextCommandResult.Success(LangUtility.SetL(langCode, "command-success-become", thisCulture.CultureNAME));
			}
			if (((string)args[0]).Contains("help")) {
				return TextCommandResult.Success(CommandInfo(langCode, "culture", "help"));
			}
			if (((string)args[0]).Contains("desc")) {
				return TextCommandResult.Success(CommandInfo(langCode, "culture", "desc"));
			}
			return TextCommandResult.Error(LangUtility.GetL(langCode, "command-help-culture"));
		}

		public void CreateKingdom(string newKingdomGUID, string newKingdomNAME, string founderGUID, bool autoJoin) {
			Kingdom newKingdom = new Kingdom();
			newKingdom.KingdomGUID = GuidUtility.RandomizeGUID(newKingdomGUID, 8, GetKingdomGUIDs());
			newKingdom.KingdomTYPE = KingUtility.CorrectedTYPE(newKingdomNAME);
			newKingdom.KingdomNAME = KingUtility.CorrectedNAME(newKingdom.KingdomTYPE, newKingdomNAME, true, false);
			newKingdom.KingdomLONG = KingUtility.CorrectedNAME(newKingdom.KingdomTYPE, newKingdomNAME, false, true);
			newKingdom.KingdomDESC = null;
			newKingdom.KingdomHEXA = "#383838";
			newKingdom.KingdomHEXB = "#b5b5b5";
			newKingdom.KingdomHEXC = "#ffffff";
			newKingdom.LeadersGUID = null;
			newKingdom.LeadersNAME = KingUtility.CorrectedLEAD(newKingdom.KingdomTYPE, true, false, false);
			newKingdom.LeadersLONG = KingUtility.CorrectedLEAD(newKingdom.KingdomTYPE, false, true, false);
			newKingdom.LeadersDESC = KingUtility.CorrectedLEAD(newKingdom.KingdomTYPE, false, false, true);
			newKingdom.MembersROLE = KingUtility.CorrectedROLE(newKingdom.KingdomTYPE);
			newKingdom.FoundedMETA = DateTime.Now.ToLongDateString();
			newKingdom.FoundedDATE = serverAPI.World.Calendar.PrettyDate();
			newKingdom.FoundedHOUR = serverAPI.World.Calendar.TotalHours;
			newKingdom.CurrentVOTE = null;
			if (founderGUID != null && serverAPI.World.PlayerByUid(founderGUID) is IPlayer founder) {
				if (autoJoin && founder.Entity.HasBehavior<EntityBehaviorLoyalties>()) {
					founder.Entity.GetBehavior<EntityBehaviorLoyalties>().kingdomGUID = newKingdom.KingdomGUID;
					newKingdom.LeadersGUID = founderGUID;
					newKingdom.PlayersGUID.Add(founderGUID);
					newKingdom.PlayersINFO.Add(KingUtility.PlayerDetails(founderGUID, newKingdom.MembersROLE, KingUtility.GetLeaderROLE(newKingdom.MembersROLE)));
					newKingdom.EntitiesALL.Add(founder.Entity.EntityId);
				}
				founder.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/cashregister"), founder.Entity);
			}
			kingdomList.Add(newKingdom);
			SaveKingdom();
		}

		public void CreateCulture(string newCultureGUID, string newCultureNAME, string founderGUID, bool autoJoin) {
			Culture newCulture = new Culture();
			newCulture.CultureGUID = GuidUtility.RandomizeGUID(newCultureGUID, 8, GetCultureGUIDs());
			newCulture.CultureNAME = CultUtility.CorrectedNAME(newCultureNAME);
			newCulture.CultureLONG = CultUtility.CorrectedLONG(newCultureNAME);
			newCulture.CultureDESC = null;
			newCulture.FounderGUID = founderGUID;
			newCulture.FoundedMETA = DateTime.Now.ToLongDateString();
			newCulture.FoundedDATE = serverAPI.World.Calendar.PrettyDate();
			newCulture.FoundedHOUR = serverAPI.World.Calendar.TotalHours;
			newCulture.Predecessor = null;
			newCulture.MascFNames = LangUtility.Open(serverAPI.World.Config.GetString("BasicMascNames")).ToHashSet<string>();
			newCulture.FemmFNames = LangUtility.Open(serverAPI.World.Config.GetString("BasicFemmNames")).ToHashSet<string>();
			newCulture.CommLNames = LangUtility.Open(serverAPI.World.Config.GetString("BasicLastNames")).ToHashSet<string>();
			if (autoJoin && founderGUID != null && serverAPI.World.PlayerByUid(founderGUID) is IPlayer founder) {
				string oldCultureGUID = founder.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("culture_guid");
				newCulture.Predecessor = oldCultureGUID;
				if (oldCultureGUID != GlobalCodes.commonerGUID && cultureList.Exists(cultureMatch => cultureMatch.CultureGUID == oldCultureGUID)) {
					Culture oldCulture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == oldCultureGUID);
					newCulture.MascFNames = oldCulture.MascFNames;
					newCulture.FemmFNames = oldCulture.FemmFNames;
					newCulture.CommLNames = oldCulture.CommLNames;
					newCulture.SkinColors = oldCulture.SkinColors;
					newCulture.EyesColors = oldCulture.EyesColors;
					newCulture.HairColors = oldCulture.HairColors;
					newCulture.HairStyles = oldCulture.HairStyles;
					newCulture.HairExtras = oldCulture.HairExtras;
					newCulture.FaceStyles = oldCulture.FaceStyles;
					newCulture.FaceBeards = oldCulture.FaceBeards;
					newCulture.WoodBlocks = oldCulture.WoodBlocks;
					newCulture.RockBlocks = oldCulture.RockBlocks;
				}
				founder.Entity.GetBehavior<EntityBehaviorLoyalties>().cultureGUID = newCulture.CultureGUID;
				var AvailableSkinParts = founder.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
				foreach (var skinpart in AvailableSkinParts.AppliedSkinParts) {
					switch (skinpart.PartCode) {
						case "baseskin": newCulture.SkinColors.Add(skinpart.Code.ToString()); continue;
						case "eyecolor": newCulture.EyesColors.Add(skinpart.Code.ToString()); continue;
						case "haircolor": newCulture.HairColors.Add(skinpart.Code.ToString()); continue;
						case "hairbase": newCulture.HairStyles.Add(skinpart.Code.ToString()); continue;
						case "hairextra": newCulture.HairExtras.Add(skinpart.Code); continue;
						case "mustache": newCulture.FaceStyles.Add(skinpart.Code); continue;
						case "beard": newCulture.FaceBeards.Add(skinpart.Code); continue;
					}
				}
				founder.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/writing"), founder.Entity);
			}
			cultureList.Add(newCulture);
			SaveCulture();
		}

		public void DeleteKingdom(string kingdomGUID) {
			Kingdom kingdom = GetKingdom(kingdomGUID);
			foreach (string member in GetOnlinesGUIDs(kingdom.KingdomGUID, null)) {
				serverAPI.World.PlayerByUid(member)?.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/deepbell"), serverAPI.World.PlayerByUid(member)?.Entity);
				UpdateSentries(serverAPI.World.PlayerByUid(member)?.Entity.ServerPos.XYZ);
			}
			foreach (var entity in serverAPI.World.LoadedEntities.Values) {
				if (!entity.WatchedAttributes.HasAttribute("loyalties")) {
					continue;
				} else if (entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") == kingdomGUID) {
					entity.WatchedAttributes.GetTreeAttribute("loyalties")?.SetString("kingdom_guid", GlobalCodes.commonerGUID);
				}
			}
			kingdomList.Remove(kingdom);
			SaveKingdom();
		}

		public void DeleteCulture(string cultureGUID) {
			Culture culture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == cultureGUID);
			foreach (string member in GetOnlinesGUIDs(null, culture.CultureGUID)) {
				serverAPI.World.PlayerByUid(member)?.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/deepbell"), serverAPI.World.PlayerByUid(member)?.Entity);
			}
			foreach (var entity in serverAPI.World.LoadedEntities.Values) {
				if (!entity.HasBehavior<EntityBehaviorLoyalties>()) {
					continue;
				} else if (entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("culture_guid") == cultureGUID) {
					entity.WatchedAttributes.GetTreeAttribute("loyalties")?.SetString("culture_guid", GlobalCodes.commonerGUID);
				}
			}
			cultureList.Remove(culture);
			SaveCulture();
		}

		public void SwitchKingdom(IServerPlayer caller, string kingdomGUID, string specificROLE = null) {
			Kingdom oldKingdom = GetKingdom(caller.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid", GlobalCodes.commonerGUID));
			Kingdom newKingdom = GetKingdom(kingdomGUID);
			oldKingdom.PlayersGUID.Remove(caller.PlayerUID);
			oldKingdom.EntitiesALL.Remove(caller.Entity.EntityId);
			oldKingdom.InvitesGUID.Remove(caller.PlayerUID);
			oldKingdom.RequestGUID.Remove(caller.PlayerUID);
			newKingdom.InvitesGUID.Remove(caller.PlayerUID);
			newKingdom.RequestGUID.Remove(caller.PlayerUID);
			foreach (var player in oldKingdom.PlayersINFO) {
				if (player.Split(':')[0] == caller.PlayerUID) {
					oldKingdom.PlayersINFO.Remove(player);
					break;
				}
			}
			if (oldKingdom.PlayersGUID.Count == 0) {
				DeleteKingdom(oldKingdom.KingdomGUID);
			} else if (oldKingdom.LeadersGUID == caller.PlayerUID) {
				/** TODO: Start Elections if Kingdom is REPUBLIC or assign by RANK! **/
				oldKingdom.LeadersGUID = KingUtility.MostSeniority(serverAPI, oldKingdom.KingdomGUID);
			}
			newKingdom.PlayersGUID.Add(caller.PlayerUID);
			newKingdom.PlayersINFO.Add(KingUtility.PlayerDetails(caller.PlayerUID, newKingdom.MembersROLE, specificROLE));
			newKingdom.EntitiesALL.Add(caller.Entity.EntityId);
			caller.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.SetString("kingdom_guid", kingdomGUID);
			UpdateSentries(caller.Entity.ServerPos.XYZ);
			SaveKingdom();
		}

		public void SwitchCulture(IServerPlayer caller, string cultureGUID) {
			Culture oldCulture = GetCulture(caller.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("culture_guid"));
			Culture newCulture = GetCulture(cultureGUID);
			oldCulture.InviteGUID.Remove(caller.PlayerUID);
			newCulture.InviteGUID.Remove(caller.PlayerUID);
			caller.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.SetString("culture_guid", cultureGUID);
			SaveCulture();
		}

		public bool AddMemberRole(string kingdomGUID, string membersROLE) {
			Kingdom thisKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID);
			string[] privileges = membersROLE.Split(' ').RemoveEntry(0);
			string[] testRole = thisKingdom.MembersROLE.Replace("/T", "").Replace("/F", "").Split(':');
			membersROLE = membersROLE.Split(' ')[0];
			foreach (var role in testRole) {
				if (role.ToLowerInvariant() == membersROLE.RemoveDiacritics().ToLowerInvariant()) {
					return false;
				}
			}
			string setPrivs = string.Empty;
			string[] positives = { "t", "y", "true", "yes", "yep", "net", "shi", "ja", "tak", "ye", "si", "sim", "oo", "hayir", "naeam" };
			string[] negatives = { "f", "n", "false", "no", "nope", "net", "bu", "nee", "nein", "nie", "aniyo", "nao", "hindi", "evet", "la" };
			for (int i = 0; i < 5; i++) {
				if ((privileges.Length - 1) < i || negatives.Contains(privileges[i].RemoveDiacritics().ToLowerInvariant())) {
					setPrivs += "/F";
					continue;
				}
				if (positives.Contains(privileges[i].RemoveDiacritics().ToLowerInvariant())) {
					setPrivs += "/T";
				}
			}
			thisKingdom.MembersROLE += ":" + membersROLE + setPrivs;
			SaveKingdom();
			return true;
		}
		
		public bool SetMemberRole(string kingdomGUID, string playersGUID, string membersROLE) {
			Kingdom thisKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID);
			string[] testRole = thisKingdom.MembersROLE.Replace("/T", "").Replace("/F", "").Split(':');
			bool roleFnd = false;
			bool roleSet = false;
			foreach (var role in testRole) {
				if (role.ToLowerInvariant() == membersROLE.ToLowerInvariant()) {
					membersROLE = role;
					roleFnd = true;
					break;
				}
			}
			if (!roleFnd) {
				return false;
			}
			foreach (var player in thisKingdom.PlayersINFO) {
				string[] infoCard = player.Split(':');
				if (infoCard[0] == playersGUID) {
					string keepName = infoCard[1];
					string keepDate = infoCard[3];
					thisKingdom.PlayersINFO.Remove(player);
					thisKingdom.PlayersINFO.Add(playersGUID + ":" + keepName + ":" + membersROLE + ":" + keepDate);
					roleSet = true;
					SaveKingdom();
					break;
				}
			}
			return roleSet;
		}
		
		public void SetWarKingdom(string kingdomGUID1, string kingdomGUID2) {
			Kingdom kingdomONE = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID1);
			Kingdom kingdomTWO = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID2);
			kingdomONE.EnemiesGUID.Remove(kingdomGUID2);
			kingdomTWO.EnemiesGUID.Remove(kingdomGUID1);
			kingdomONE.PeaceOffers.Remove(kingdomGUID2);
			kingdomTWO.PeaceOffers.Remove(kingdomGUID1);
			kingdomONE.EnemiesGUID.Add(kingdomGUID2);
			kingdomTWO.EnemiesGUID.Add(kingdomGUID1);
			SaveKingdom();
			foreach (var player in serverAPI.World.AllOnlinePlayers) {
				serverAPI.SendMessage(player, GlobalConstants.GeneralChatGroup, LangUtility.SetL((player as IServerPlayer).LanguageCode, "command-message-attack", kingdomONE.KingdomNAME, kingdomTWO.KingdomNAME), EnumChatType.Notification);
				UpdateSentries(player.Entity.ServerPos.XYZ);
			}
		}

		public void EndWarKingdom(string kingdomGUID1, string kingdomGUID2) {
			Kingdom kingdomONE = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID1);
			Kingdom kingdomTWO = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID2);
			kingdomONE.EnemiesGUID.Remove(kingdomGUID2);
			kingdomTWO.EnemiesGUID.Remove(kingdomGUID1);
			kingdomONE.PeaceOffers.Remove(kingdomGUID2);
			kingdomTWO.PeaceOffers.Remove(kingdomGUID1);
			SaveKingdom();
			foreach (var player in serverAPI.World.AllOnlinePlayers) {
				serverAPI.SendMessage(player, 0, LangUtility.Set("command-message-treaty", kingdomONE.KingdomLONG, kingdomTWO.KingdomLONG), EnumChatType.Notification);
				UpdateSentries(player.Entity.ServerPos.XYZ);
			}
			if (kingdomONE.LeadersGUID == null || kingdomTWO.LeadersGUID == null) {
				return;
			}
			IPlayer leadersONE = serverAPI.World.PlayerByUid(kingdomONE.LeadersGUID);
			IPlayer leadersTWO = serverAPI.World.PlayerByUid(kingdomTWO.LeadersGUID);
			if (serverAPI.World.AllOnlinePlayers.Contains(leadersONE) && serverAPI.World.AllOnlinePlayers.Contains(leadersTWO) && leadersONE.Entity.ServerPos.HorDistanceTo(leadersTWO.Entity.ServerPos) <= 10) {
				foreach (var itemSlot in leadersONE.Entity.GearInventory) {
					if (itemSlot.Itemstack?.Item is ItemBook && itemSlot.Itemstack?.Attributes.GetString("signedby") == null) {
						string composedTreaty = $"On the day of {serverAPI.World.Calendar.PrettyDate()}, {kingdomONE.KingdomLONG.UcFirst} and {kingdomTWO.KingdomLONG} agree to formally end all hostilities.";
						itemSlot.Itemstack.Attributes.SetString("text", composedTreaty);
						itemSlot.Itemstack.Attributes.SetString("title", $"Treaty of {kingdomONE.KingdomNAME}");
						itemSlot.Itemstack.Attributes.SetString("signedby", $"{leadersONE.PlayerName} & {leadersTWO.PlayerName}");
						itemSlot.TakeOut(1);
						itemSlot.MarkDirty();
						break;
					}
				}
			}
		}

		public string KingdomWanted(HashSet<string> enemiesGUID) {
			List<string> playerList = new List<string>();
			foreach (var player in serverAPI.World.AllPlayers) {
				playerList.Add(player.PlayerUID);
			}
			List<string> namesList = new List<string>();
			foreach (var playerGUID in enemiesGUID) {
				if (playerList.Contains(playerGUID)) {
					namesList.Add(serverAPI.PlayerData.GetPlayerDataByUid(playerGUID).LastKnownPlayername);
				}
			}
			return string.Join(", ", namesList);
		}

		public string KingdomInvite(string playersGUID, bool getRequests) {
			string langCode = (serverAPI.World.PlayerByUid(playersGUID) as IServerPlayer)?.LanguageCode ?? "en";
			string[] playerNames = Array.Empty<string>();
			string[] playerGuids = Array.Empty<string>();
			List<string> invites = new List<string>();
			foreach (Kingdom kingdom in kingdomList) {
				if (kingdom.InvitesGUID.Contains(playersGUID)) {
					invites.Add(kingdom.KingdomNAME);
				}
				if (getRequests && kingdom.PlayersGUID.Contains(playersGUID)) {
					playerGuids = kingdom.RequestGUID.ToArray();
					playerNames = new string[kingdom.RequestGUID.Count];
					foreach (string playerGUID in playerGuids) {
						playerNames.AddToArray(serverAPI.World.PlayerByUid(playerGUID).PlayerName);
					}
				}
			}
			string invbox = LangUtility.SetL(langCode, "command-success-invbox", invites.Count.ToString(), string.Join("\n", invites)) ;
			string reqbox = LangUtility.SetL(langCode, "command-success-reqbox", playerNames.Length.ToString(), string.Join("\n", playerNames));
			if (getRequests) {
				return invbox + "\n" + reqbox;
			}
			return invbox;
		}

		public string CultureInvite(string playersGUID) {
			string langCode = (serverAPI.World.PlayerByUid(playersGUID) as IServerPlayer)?.LanguageCode ?? "en";
			List<string> invites = new List<string>();
			foreach (Culture culture in cultureList) {
				if (culture.InviteGUID.Contains(playersGUID)) {
					invites.Add(culture.CultureNAME);
				}
			}
			return LangUtility.SetL(langCode, "command-success-invbox", invites.Count.ToString(), string.Join("\n", invites));
		}

		public string ChangeKingdom(string langCode, string kingdomGUID, string subcomm, string subargs, string changes) {
			Kingdom kingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID);
			string[] keywords = { LangUtility.GetL(langCode, "entries-keyword-kingdom"), LangUtility.GetL(langCode, "entries-keyword-player"), LangUtility.GetL(langCode, "entries-keyword-players") };
			switch (subcomm) {
				case "append":
					switch (subargs) {
						case "roles": AddMemberRole(kingdomGUID, changes); break;
						default: return LangUtility.SetL(langCode, "command-help-update-kingdom-append", keywords[0]);
					}
					break;
				case "remove":
					switch (subargs) {
						case "roles": kingdom.MembersROLE = string.Join(":", kingdom.MembersROLE.Split(':').RemoveEntry(kingdom.MembersROLE.Replace("/T", "").Replace("/F", "").Split(':').IndexOf(changes))).TrimEnd(':'); break;
						default: return LangUtility.SetL(langCode, "command-help-update-kingdom-remove", keywords[0]);
					}
					break;
				case "rename":
					switch (subargs) {
						case "title": kingdom.KingdomNAME = changes; break;
						case "longs": kingdom.KingdomLONG = changes; break;
						case "descs": kingdom.KingdomDESC = LangUtility.Mps(changes.Remove(changes.Length - 1).Remove(0).UcFirst()); break;
						case "ruler": kingdom.LeadersNAME = changes; break;
						case "names": kingdom.LeadersLONG = changes; break;
						case "short": kingdom.LeadersDESC = LangUtility.Mps(changes.Remove(changes.Length - 1).Remove(0).UcFirst()); break;
						default: return LangUtility.SetL(langCode, "command-help-update-kingdom-rename", keywords[0]);
					}
					break;
				case "player":
					switch (subargs) {
						case "roles": SetMemberRole(kingdomGUID, GetAPlayer(changes.Split(' ')[0]).PlayerUID ?? null, changes.Split(' ')[1]); break;
						default: return LangUtility.SetL(langCode, "command-help-update-kingdom-player", keywords[0]);
					}
					break;
				case "getall":
					switch (subargs) {
						case "basic": return KingUtility.ListedAllData(serverAPI, kingdomGUID);
						default: return LangUtility.SetL(langCode, "command-help-update-kingdom-getall", keywords[0]);
					}
				default: return $"{LangUtility.SetL(langCode, "command-failure-update", keywords[0])}\n{LangUtility.SetL(langCode, "command-help-update-kingdom", keywords[0])}";
			}
			SaveKingdom();
			return LangUtility.SetL(langCode, "command-success-update", keywords[0]);
		}

		public string ChangeCulture(string langCode, string cultureGUID, string subcomm, string subargs, string changes) {
			Culture culture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == cultureGUID);
			string[] acceptedAppend = new string[] { "add", "addnew", "new" };
			string[] acceptedRemove = new string[] { "delete", "clear", "erase", "ban" };
			string[] acceptedRename = new string[] { "name", "change", "setname" };
			string[] acceptedGetall = new string[] { "getinfo", "info", "details" };
			if (acceptedAppend.Contains(subcomm)) {
				subcomm = "append";
			} else if (acceptedRemove.Contains(subcomm)) {
				subcomm = "remove";
			} else if (acceptedRename.Contains(subcomm)) {
				subcomm = "rename";
			} else if (acceptedGetall.Contains(subcomm)) {
				subcomm = "getall";
			}
			switch (subcomm) {
				case "append":
					switch (subargs) {
						case "mascs": culture.MascFNames.Add(changes.UcFirst()); break;
						case "femms": culture.FemmFNames.Add(changes.UcFirst()); break;
						case "names": culture.CommLNames.Add(changes.UcFirst()); break;
						case "skins": culture.SkinColors.Add(changes); break;
						case "pupil": culture.EyesColors.Add(changes); break;
						case "hairs": culture.HairColors.Add(changes); break;
						case "style": culture.HairStyles.Add(changes); break;
						case "extra": culture.HairExtras.Add(changes); break;
						case "beard": culture.FaceBeards.Add(changes); break;
						default: return LangUtility.SetL(langCode, "command-help-update-culture-append", "culture");
					}
					break;
				case "remove":
					switch (subargs) {
						case "mascs": culture.MascFNames.Remove(changes); break;
						case "femms": culture.FemmFNames.Remove(changes); break;
						case "names": culture.CommLNames.Remove(changes); break;
						case "skins": culture.SkinColors.Remove(changes); break;
						case "pupil": culture.EyesColors.Remove(changes); break;
						case "hairs": culture.HairColors.Remove(changes); break;
						case "style": culture.HairStyles.Remove(changes); break;
						case "extra": culture.HairExtras.Remove(changes); break;
						case "beard": culture.FaceBeards.Remove(changes); break;
						default: return LangUtility.SetL(langCode, "command-help-update-culture-remove", "culture");
					}
					break;
				case "rename":
					switch (subargs) {
						case "title": culture.CultureNAME = changes; break;
						case "longs": culture.CultureLONG = changes; break;
						case "descs": culture.CultureDESC = LangUtility.Mps(changes.Remove(changes.Length - 1).Remove(0).UcFirst()); break;
						default: return LangUtility.SetL(langCode, "command-help-update-culture-rename", "culture");
					}
					break;
				case "getall":
					switch (subargs) {
						case "basic": return CultUtility.ListedAllData(serverAPI, cultureGUID);
						case "mascs": return LangUtility.Msg(culture.MascFNames.ToArray());
						case "femms": return LangUtility.Msg(culture.FemmFNames.ToArray());
						case "names": return LangUtility.Msg(culture.CommLNames.ToArray());
						case "skins": return LangUtility.Msg(culture.SkinColors.ToArray());
						case "pupil": return LangUtility.Msg(culture.EyesColors.ToArray());
						case "hairs": return LangUtility.Msg(culture.HairColors.ToArray());
						case "style": return LangUtility.Msg(culture.HairStyles.ToArray());
						case "extra": return LangUtility.Msg(culture.HairExtras.ToArray());
						case "beard": return LangUtility.Msg(culture.FaceBeards.ToArray());
						default: return LangUtility.SetL(langCode, "command-help-update-culture-getall", "culture");
					}
				default: return $"{LangUtility.SetL(langCode, "command-failure-update", "culture")}\n{LangUtility.SetL(langCode, "command-help-update", "culture")}";
			}
			SaveCulture();
			return LangUtility.SetL(langCode, "command-success-update", LangUtility.Get("entries-keyword-culture"));
		}

		private string CommandInfo(string langCode, string kindsof = "kingdom", string informs = "desc") {
			string[] commands = { "create", "delete", "update", "invite", "remove", "become", "depart", "revolt", "rebels", "attack", "treaty", "outlaw", "pardon", "wanted", "accept", "reject", "voting" };
			string langkeys = "command-" + informs + "-";
			string messages = "";
			foreach (string com in commands) {
				if (Lang.HasTranslation("vskingdom:" + langkeys + com)) {
					messages += "\n" + LangUtility.RefL(langCode, langkeys + com, kindsof, ("entries-keyword-" + kindsof), "entries-keyword-players", "entries-keyword-replies");
				}
			}
			return messages;
		}

		private string GetKingdomNAME(string kingdomGUID) {
			return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID)?.KingdomNAME;
		}

		private string GetLeadersGUID(string kingdomGUID, string PlayersUID = null) {
			if (kingdomGUID != null) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID)?.LeadersGUID;
			}
			if (PlayersUID != null) {
				foreach (Kingdom kingdom in kingdomList) {
					if (kingdom.PlayersGUID.Contains(PlayersUID)) {
						return kingdom.LeadersGUID;
					}
				}
			}
			return null;
		}

		private string[] GetKingdomGUIDs() {
			string[] kingdomGUIDs = Array.Empty<string>();
			foreach (var kingdom in kingdomList) {
				kingdomGUIDs.AddItem(kingdom.KingdomGUID);
			}
			return kingdomGUIDs;
		}

		private string[] GetCultureGUIDs() {
			string[] cultureGUIDs = Array.Empty<string>();
			foreach (var culture in cultureList) {
				cultureGUIDs.AddItem(culture.CultureGUID);
			}
			return cultureGUIDs;
		}

		private string[] GetOnlinesGUIDs(string kingdomGUID = null, string cultureGUID = null) {
			string[] allOnlines = Array.Empty<string>();
			if (kingdomGUID != null) {
				foreach (var player in serverAPI.World.AllOnlinePlayers) {
					if (player.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") == kingdomGUID) {
						allOnlines.AddItem(player.PlayerUID);
					}
				}
			}
			if (cultureGUID != null) {
				foreach (var player in serverAPI.World.AllOnlinePlayers) {
					if (player.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("culture_guid") == cultureGUID) {
						allOnlines.AddItem(player.PlayerUID);
					}
				}
			}
			return allOnlines;
		}

		private bool KingdomExists(string kingdomGUID = null, string kingdomNAME = null) {
			bool lookingForGUID = kingdomGUID != null;
			bool lookingForNAME = kingdomNAME != null;
			foreach (var kingdom in kingdomList) {
				if (lookingForGUID && kingdom.KingdomGUID == kingdomGUID) {
					return true;
				}
				if (lookingForNAME && kingdom.KingdomNAME == kingdomNAME) {
					return true;
				}
			}
			return false;
		}

		private bool CultureExists(string cultureGUID = null, string cultureNAME = null) {
			bool lookingForGUID = cultureGUID != null;
			bool lookingForNAME = cultureNAME != null;
			foreach (var culture in cultureList) {
				if (lookingForGUID && culture.CultureGUID == cultureGUID) {
					return true;
				}
				if (lookingForNAME && culture.CultureNAME == cultureNAME) {
					return true;
				}
			}
			return false;
		}

		private bool IsKingdom(string kingdomGUID, string kingdomNAME = null) {
			foreach (Kingdom kingdom in kingdomList) {
				if (kingdomGUID != null && kingdom.KingdomGUID == kingdomGUID) {
					return true;
				}
				if (kingdomNAME != null && kingdom.KingdomNAME.ToLowerInvariant() == kingdomNAME.ToLowerInvariant()) {
					return true;
				}
			}
			return false;
		}

		private bool InKingdom(string playerUID) {
			foreach (Kingdom kingdom in kingdomList) {
				if (kingdom.PlayersGUID.Contains(playerUID)) {
					return true;
				}
			}
			return false;
		}

		private bool NameTaken(string kingdomNAME) {
			foreach (Kingdom kingdom in kingdomList) {
				if (kingdom.KingdomNAME.ToLowerInvariant() == kingdomNAME.ToLowerInvariant()) {
					return true;
				}
			}
			return false;
		}

		private Kingdom GetKingdom(string kingdomGUID, string kingdomNAME = null) {
			if (kingdomGUID != null) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID);
			}
			if (kingdomNAME != null) {
				return kingdomList.Find(kingdomMatch => kingdomMatch.KingdomNAME == kingdomNAME);
			}
			return null;
		}

		private Culture GetCulture(string cultureGUID, string cultureNAME = null) {
			if (cultureGUID != null) {
				return cultureList.Find(cultureMatch => cultureMatch.CultureGUID == cultureGUID);
			}
			if (cultureNAME != null) {
				return cultureList.Find(cultureMatch => cultureMatch.CultureNAME == cultureNAME);
			}
			return null;
		}

		private IServerPlayer GetAPlayer(string playersNAME) {
			return serverAPI.World.AllPlayers.ToList<IPlayer>().Find(playerMatch => playerMatch.PlayerName == playersNAME) as IServerPlayer ?? null;
		}
	}
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class PlayerUpdate {
		public long[] followers;
		public string operation;
		public string kingdomID;
		public string cultureID;
	}
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class SentryUpdate {
		public long playerUID;
		public long entityUID;
		public string[] kingdomINFO;
		public string[] cultureINFO;
		public string[] leadersINFO;
		public string[] coloursLIST;
		public string[] enemiesLIST;
		public string[] friendsLIST;
		public string[] outlawsLIST;
	}
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class SentryOrders {
		public long playerUID;
		public long entityUID;
		public bool usedorder;
		public bool? wandering = null;
		public bool? following = null;
		public bool? attacking = null;
		public bool? pursueing = null;
		public bool? shifttime = null;
		public bool? patroling = null;
		public bool? returning = null;
		public string attribute = null;
	}
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class WeaponAnims {
		public string itemCode = null;
		public string idleAnim = "idle";
		public string walkAnim = "walk";
		public string moveAnim = "move";
		public string duckAnim = "duck";
		public string swimAnim = "swim";
		public string jumpAnim = "jump";
		public string diesAnim = "dies";
		public string drawAnim = "draw";
		public string fireAnim = "fire";
		public string loadAnim = "load";
		public string bashAnim = "bash";
		public string stabAnim = "bash";
		public string[] allCodes => new string[12] { idleAnim, walkAnim, moveAnim, duckAnim, swimAnim, jumpAnim, diesAnim, drawAnim, fireAnim, loadAnim, bashAnim, stabAnim };
	}
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class ClassPrivileges {
		public bool ownLand;
		public bool ownArms;
		public bool canVote;
		public bool canLock;
		public bool canKill;
		public bool invites;
	}
}