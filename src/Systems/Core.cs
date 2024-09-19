global using static VSKingdom.Constants.GlobalCodes;
global using static VSKingdom.Constants.GlobalDicts;
global using static VSKingdom.Constants.GlobalEnums;
global using static VSKingdom.Constants.GlobalPaths;
global using static VSKingdom.Constants.GlobalProps;
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
using static VSKingdom.Utilities.ColoursUtil;
using static VSKingdom.Utilities.CultureUtil;
using static VSKingdom.Utilities.GenericUtil;
using static VSKingdom.Utilities.KingdomUtil;
using static VSKingdom.Utilities.ReadingUtil;
using static VSKingdom.Extension.KingdomListExtension;
using static VSKingdom.Extension.CultureListExtension;
using static VSKingdom.Extension.ServerExtension;

namespace VSKingdom {
	public class VSKingdom : ModSystem {
		Harmony harmony = new Harmony("badrabbit49.vskingdom");

		public ICoreClientAPI clientAPI;
		public ICoreServerAPI serverAPI;

		private List<Kingdom> kingdomList;
		private List<Culture> cultureList;

		public override void Start(ICoreAPI api) {
			base.Start(api);
			// Block Classes //
			api.RegisterBlockClass("BlockBody", typeof(BlockBody));
			api.RegisterBlockClass("BlockPost", typeof(BlockPost));
			// Block Entities //
			api.RegisterBlockEntityClass("BlockEntityBody", typeof(BlockEntityBody));
			api.RegisterBlockEntityClass("BlockEntityPost", typeof(BlockEntityPost));
			// Items Classes //
			api.RegisterItemClass("ItemBanner", typeof(ItemBanner));
			api.RegisterItemClass("ItemPeople", typeof(ItemPeople));
			// Entity Classes //
			api.RegisterEntity("EntitySentry", typeof(EntitySentry));
			// Entity Behaviors //
			api.RegisterEntityBehaviorClass("KingdomFullNames", typeof(EntityBehaviorFullNames));
			api.RegisterEntityBehaviorClass("KingdomLoyalties", typeof(EntityBehaviorLoyalties));
			api.RegisterEntityBehaviorClass("SoldierDecayBody", typeof(EntityBehaviorDecayBody));
			// AITask Classes //
			AiTaskRegistry.Register<AiTaskSentryAttack>("SentryAttack");
			AiTaskRegistry.Register<AiTaskSentryFollow>("SentryFollow");
			AiTaskRegistry.Register<AiTaskSentryIdling>("SentryIdling");
			AiTaskRegistry.Register<AiTaskSentryPatrol>("SentryPatrol");
			AiTaskRegistry.Register<AiTaskSentryRanged>("SentryRanged");
			AiTaskRegistry.Register<AiTaskSentrySearch>("SentrySearch");
			AiTaskRegistry.Register<AiTaskSentryWander>("SentryWander");
			// Patches //
			if (!Harmony.HasAnyPatches("badrabbit49.vskingdom")) {
				harmony.PatchAll();
			}
			// Create chat commands for creation, deletion, invitation, and so of kingdoms.
			api.ChatCommands.Create("kingdoms")
				.RequiresPrivilege(Privilege.chat)
				.RequiresPlayer()
				.WithAlias("kingdom")
				.WithArgs(api.ChatCommands.Parsers.Word("commands", kingdomCommands), api.ChatCommands.Parsers.OptionalAll("argument"))
				.HandleWith(new OnCommandDelegate(OnKingdomsCommand));
			// Create chat commands for creation, deletion, invitation, and so of cultures.
			api.ChatCommands.Create("cultures")
				.RequiresPrivilege(Privilege.chat)
				.RequiresPlayer()
				.WithAlias("culture")
				.WithArgs(api.ChatCommands.Parsers.Word("commands", cultureCommands), api.ChatCommands.Parsers.OptionalAll("argument"))
				.HandleWith(new OnCommandDelegate(OnCulturesCommand));
		}

		public override void StartClientSide(ICoreClientAPI capi) {
			base.StartClientSide(capi);
			this.clientAPI = capi;
			capi.Event.LevelFinalize += () => LevelFinalize(capi);
			capi.Network.RegisterChannel("sentrynetwork")
				.RegisterMessageType<SentryOrdersToServer>()
				.RegisterMessageType<SentryUpdateToServer>()
				.RegisterMessageType<PlayerUpdateToServer>();
		}

		public override void StartServerSide(ICoreServerAPI sapi) {
			base.StartServerSide(sapi);
			this.serverAPI = sapi;
			sapi.Event.SaveGameCreated += MakeAllData;
			sapi.Event.GameWorldSave += WriteToDisk;
			sapi.Event.SaveGameLoaded += LoadAllData;
			sapi.Event.PlayerJoin += PlayerJoinsGame;
			sapi.Event.PlayerDisconnect += PlayerLeaveGame;
			sapi.Event.PlayerDeath += PlayerDeathFrom;
			sapi.Event.ServerRunPhase(EnumServerRunPhase.Shutdown, CleanupData);
			sapi.Network.RegisterChannel("sentrynetwork")
				.RegisterMessageType<SentryOrdersToServer>().SetMessageHandler<SentryOrdersToServer>(OnSentryOrdered)
				.RegisterMessageType<SentryUpdateToServer>().SetMessageHandler<SentryUpdateToServer>(OnSentryUpdated)
				.RegisterMessageType<PlayerUpdateToServer>().SetMessageHandler<PlayerUpdateToServer>(OnPlayerUpdated);
		}

		public override void Dispose() {
			harmony?.UnpatchAll(Mod.Info.ModID);
			base.Dispose();
		}

		private void MakeAllData() {
			if (kingdomList is null || kingdomList.Count == 0) {
				byte[] kingdomData = serverAPI.WorldManager.SaveGame.GetData("kingdomData");
				kingdomList = kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);
			}
			if (cultureList is null || cultureList.Count == 0) {
				byte[] cultureData = serverAPI.WorldManager.SaveGame.GetData("cultureData");
				cultureList = cultureData is null ? new List<Culture>() : SerializerUtil.Deserialize<List<Culture>>(cultureData);
			}
			Dictionary<string, Kingdom> baseKingdoms = serverAPI.Assets.TryGet(AssetLocation.Create(defaultKingdoms)).ToObject<Dictionary<string, Kingdom>>();
			Dictionary<string, Culture> baseCultures = serverAPI.Assets.TryGet(AssetLocation.Create(defaultCultures)).ToObject<Dictionary<string, Culture>>();
			string[] kingdomkeys = baseKingdoms.Keys.ToArray();
			string[] culturekeys = baseCultures.Keys.ToArray();
			for (int i = 0; i < baseKingdoms.Count; i++) {
				if (!kingdomList.KingdomExists(kingdomkeys[i])) {
					kingdomList.Add(baseKingdoms[kingdomkeys[i]]);
				}
			}
			for (int i = 0; i < baseCultures.Count; i++) {
				if (!cultureList.CultureExists(kingdomkeys[i])) {
					cultureList.Add(baseCultures[culturekeys[i]]);
				}
			}
			SaveKingdom();
			SaveCulture();
			WriteToDisk();
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
			serverAPI.GetOrCreateDataPath("ModConfig/VSKingdom");
			KingdomData kingdomData = VSKingdomData.ReadData<KingdomData>(serverAPI, "VSKingdom/KingdomData.json");
			CultureData cultureData = VSKingdomData.ReadData<CultureData>(serverAPI, "VSKingdom/CultureData.json");
			serverAPI.WorldManager.SaveGame.StoreData("kingdomData", SerializerUtil.Serialize(kingdomData.Kingdoms.Values.ToList()));
			serverAPI.WorldManager.SaveGame.StoreData("cultureData", SerializerUtil.Serialize(cultureData.Cultures.Values.ToList()));
			kingdomList = kingdomData.Kingdoms.Values.ToList();
			cultureList = cultureData.Cultures.Values.ToList();
			MakeAllData();
			serverAPI.World.Config.SetBool("mapHideOtherPlayers", serverAPI.World.Config.GetBool("HideAllNames", true));
		}

		private void LoadBitData() {
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
					if (!kingdomList.KingdomExists(enemy)) {
						kingdom.EnemiesGUID.Remove(enemy);
					}
				}
				if (kingdom.PlayersGUID.Count == 0 && kingdomIDs.Contains(kingdom.KingdomGUID) == false) {
					markedForDeletion.Add(kingdom.KingdomGUID);
				}
			}
			if (markedForDeletion.Count > 0) {
				foreach (var marked in markedForDeletion) {
					DeleteKingdom(marked);
				}
			}
			SaveAllData();
			WriteToDisk();
		}

		private void WriteToDisk() {
			serverAPI.WorldManager.SaveGame.StoreData("kingdomData", SerializerUtil.Serialize(kingdomList));
			serverAPI.WorldManager.SaveGame.StoreData("cultureData", SerializerUtil.Serialize(cultureList));
			serverAPI.GetOrCreateDataPath("ModConfig/VSKingdom");
			VSKingdomData.ReadData<KingdomData>(serverAPI, "VSKingdom/KingdomData.json");
			VSKingdomData.ReadData<CultureData>(serverAPI, "VSKingdom/CultureData.json");
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
			bool _notInKingdom = !player.Entity.WatchedAttributes.HasAttribute("kingdomGUID") || !kingdomList.KingdomExists(player.Entity.WatchedAttributes.GetKingdom());
			bool _notInCulture = !player.Entity.WatchedAttributes.HasAttribute("cultureGUID") || !cultureList.CultureExists(player.Entity.WatchedAttributes.GetCulture());
			bool _hideNametags = serverAPI.World.Config.GetBool("HideAllNames", true);
			long _nametagRange = serverAPI.World.Config.GetInt("NameRenderDist", 100);
			player.Entity.WatchedAttributes.GetTreeAttribute("nametag")?.SetBool("showtagonlywhentargeted", _hideNametags);
			player.Entity.WatchedAttributes.GetTreeAttribute("nametag")?.SetInt("renderRange", (int)_nametagRange);
			if (_notInKingdom) {
				serverAPI.Logger.Error(SetL(serverLang, "command-error-cantfind", "entries-keyword-kingdom"));
				foreach (Kingdom kingdom in kingdomList) {
					bool _needsInfo = true;
					bool _commoners = kingdom.KingdomGUID == commonerGUID;
					if (kingdom.PlayersGUID.Contains(player.PlayerUID)) {
						player.Entity.WatchedAttributes.SetString("kingdomGUID", kingdom.KingdomGUID);
						foreach (var member in kingdom.PlayersINFO) {
							if (member.Split(':')[0] == player.PlayerUID) {
								_needsInfo = false;
								break;
							}
						}
						if (_needsInfo && !_commoners) {
							kingdom.PlayersINFO.Add(PlayerDetails(player.PlayerUID, kingdom.MembersROLE, null));
						}
						if (!kingdom.PlayersGUID.Contains(player.PlayerUID) && !_commoners) {
							kingdom.PlayersGUID.Add(player.PlayerUID);
						}
						SaveKingdom();
						break;
					}
				}
			}
			if (_notInCulture) {
				serverAPI.Logger.Error(SetL(serverLang, "command-error-cantfind", "entries-keyword-culture"));
				foreach (Culture culture in cultureList) {
					bool _commoners = culture.CultureGUID == seraphimGUID;
					if (culture.PlayersGUID.Contains(player.PlayerUID)) {
						player.Entity.WatchedAttributes.SetString("cultureGUID", culture.CultureGUID);
						if (!culture.PlayersGUID.Contains(player.PlayerUID) && !_commoners) {
							culture.PlayersGUID.Add(player.PlayerUID);
						}
						SaveCulture();
						break;
					}
				}
			}
			UpdateSentries(player.Entity.ServerPos.XYZ);
		}

		private void PlayerLeaveGame(IServerPlayer player) {
			SaveAllData();
		}

		private void PlayerDeathFrom(IServerPlayer player, DamageSource damage) {
			if (serverAPI.ModLoader.IsModEnabled("playercorpse")) { return; }
			if (serverAPI.World.Config.GetAsBool("AllowLooting") && damage.GetCauseEntity() is EntitySentry) {
				var killer = damage.GetCauseEntity();
				var victim = player.Entity;
				var thatKingdom = killer.WatchedAttributes.GetString("kingdomGUID", commonerGUID);
				var thisKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == victim.WatchedAttributes.GetString("kingdomGUID", commonerGUID));
				// If the entities were at war with eachother then loot will be dropped. Specifically their armor and what they had in their right hand slot.
				if (thisKingdom.EnemiesGUID.Contains(thatKingdom) || thatKingdom == banditryGUID) {
					// If the killer can, try looting the player corpse right away, take what is better.
					if (killer is EntitySentry sentry) {
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
							string sentryClass = sentry.Properties.Attributes["baseClass"].AsString("melee").ToLower();
							if (slot.Empty) {
								continue;
							} else if (sentryClass != "range" && slot.Itemstack.Item is ItemBow) {
								continue;
							} else if (sentryClass != "melee" && slot.Itemstack.Item is not ItemBow) {
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
				}
			}
			if (serverAPI.World.Config.GetAsBool("DropsOnDeath")) {
				var blockAccessor = serverAPI.World.BlockAccessor;
				double x = player.Entity.ServerPos.X + player.Entity.SelectionBox.X1 - player.Entity.OriginSelectionBox.X1;
				double y = player.Entity.ServerPos.Y + player.Entity.SelectionBox.Y1 - player.Entity.OriginSelectionBox.Y1;
				double z = player.Entity.ServerPos.Z + player.Entity.SelectionBox.Z1 - player.Entity.OriginSelectionBox.Z1;
				double d = player.Entity.ServerPos.Dimension;

				BlockPos bonePos = new BlockPos((int)x, (int)y, (int)z, (int)d);
				string[] SkeletonBodies = new string[] { "humanoid1", "humanoid2" };
				Block skeletonBlock = serverAPI.World.GetBlock(new AssetLocation(Get("body-" + SkeletonBodies[serverAPI.World.Rand.Next(0, SkeletonBodies.Length)])));
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
				if (placedBlock && player.Entity.WatchedAttributes.HasAttribute("inventory")) {
					// Initialize BlockEntityBody here and put stuff into it.
					if (blockAccessor.GetBlockEntity(bonePos) is BlockEntityBody decblock) {
						// Get the inventory of the person who died if they have one.
						for (int i = 12; i < 14; i++) {
							try { player.Entity.GearInventory[i].TryPutInto(serverAPI.World, decblock.gearInv[i], player.Entity.GearInventory[i].StackSize); } catch { }
						}
						if (!player.Entity.RightHandItemSlot.Empty && player.Entity.RightHandItemSlot.Itemstack.Collectible.Attributes["toolrackTransform"].Exists) {
							try { player.Entity.RightHandItemSlot.TryPutInto(serverAPI.World, decblock.gearInv[16], 1); } catch { }
						}
					}
				} else {
					foreach (ItemSlot item in player.Entity.GearInventory) {
						if (!item.Empty) {
							serverAPI.World.SpawnItemEntity(item.Itemstack, player.Entity.ServerPos.XYZ);
							item.Itemstack = null;
							item.MarkDirty();
						}
					}
				}
			}
		}

		private void UpdateSentries(Vec3d position) {
			var nearbyEnts = serverAPI.World.GetEntitiesAround(position, 200, 60, (ent => (ent is EntitySentry)));
			var bestPlayer = serverAPI.World.NearestPlayer(position.X, position.Y, position.Z) as IServerPlayer;
			if (bestPlayer == null) { return; }
			foreach (var sentry in nearbyEnts) {
				sentry.GetBehavior<EntityBehaviorTaskAI>().TaskManager.GetTask<AiTaskSentrySearch>().ResetsTargets();
				OnSentryUpdated(bestPlayer, new SentryUpdateToServer() {
					entityUID = sentry.EntityId,
					kingdomGUID = sentry.WatchedAttributes.GetString("kingdomGUID"),
					cultureGUID = sentry.WatchedAttributes.GetString("cultureGUID"),
					leadersGUID = sentry.WatchedAttributes.GetString("leadersGUID")
				});
			}
		}

		private void OnPlayerUpdated(IServerPlayer player, PlayerUpdateToServer playerUpdate) {
			if (playerUpdate.kingdomID != null && kingdomList.KingdomExists(playerUpdate.kingdomID)) {
				player.Entity.WatchedAttributes.SetString("kingdomGUID", playerUpdate.kingdomID);
			}
			if (playerUpdate.cultureID != null && cultureList.CultureExists(playerUpdate.cultureID)) {
				player.Entity.WatchedAttributes.SetString("cultureGUID", playerUpdate.kingdomID);
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
					bool follow = entity.Alive && !(operation[1] && playerUpdate.followers.Contains(followers[i])) && entity.ServerPos.DistanceTo(player.Entity.ServerPos) < 20f && CanSeeEnt(entity, player.Entity);
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

		private void OnSentryUpdated(IServerPlayer player, SentryUpdateToServer sentryUpdate) {
			// NEEDS TO BE DONE ON THE SERVER SIDE HERE!
			SentryUpdateToEntity update = new SentryUpdateToEntity();
			if (sentryUpdate.kingdomGUID != null && sentryUpdate.kingdomGUID.Length > 0) {
				try {
					var kingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == sentryUpdate.kingdomGUID);
					update.kingdomGUID = new string(kingdom.KingdomGUID);
					update.kingdomNAME = new string(kingdom.KingdomNAME);
					update.coloursHEXA = new string(kingdom.KingdomHEXA);
					update.coloursHEXB = new string(kingdom.KingdomHEXB);
					update.coloursHEXC = new string(kingdom.KingdomHEXC);
					var sentry = (serverAPI.World.GetEntityById(sentryUpdate.entityUID) as EntitySentry);
					sentry.cachedData.UpdateLoyalty(update.kingdomGUID, update.cultureGUID, update.leadersGUID);
					sentry.cachedData.UpdateEnemies(kingdom.EnemiesGUID.ToArray());
					sentry.cachedData.UpdateFriends(kingdom.FriendsGUID.ToArray());
					sentry.cachedData.UpdateOutlaws(kingdom.OutlawsGUID.ToArray());
				} catch (NullReferenceException error) {
					serverAPI.Logger.Error($"Error getting Kingdom information!\n{error}");
				}
			}
			if (sentryUpdate.cultureGUID != null && sentryUpdate.cultureGUID.Length > 0) {
				try {
					var culture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == sentryUpdate.cultureGUID);
					update.cultureGUID = new string(culture.CultureGUID);
					update.cultureNAME = new string(culture.CultureNAME);
				} catch (NullReferenceException error) {
					serverAPI.Logger.Error($"Error getting Culture guid or name!\n{error}");
				}
			}
			if (sentryUpdate.leadersGUID != null && sentryUpdate.leadersGUID.Length > 0) {
				try {
					var leaders = serverAPI.World.PlayerByUid(sentryUpdate?.leadersGUID) ?? null;
					update.leadersGUID = new string(leaders?.PlayerUID ?? null);
					update.leadersNAME = new string(leaders?.PlayerName ?? null);
				} catch (NullReferenceException error) {
					serverAPI.Logger.Error($"Error getting Leaders guid or name!\n{error}");
				}
			}
			// Try to fully sync entity packet to the sentry if possible.
			serverAPI.Network.BroadcastEntityPacket(sentryUpdate.entityUID, 1502, SerializerUtil.Serialize<SentryUpdateToEntity>(update));
		}

		private void OnSentryOrdered(IServerPlayer player, SentryOrdersToServer sentryOrders) {
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
			sentry.WatchedAttributes.SetBool("orderShifts", sentryOrders.shifttime ?? oldOrders[4]);
			sentry.WatchedAttributes.SetBool("orderPatrol", sentryOrders.patroling ?? oldOrders[5]);
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
				PlayerUpdateToServer newUpdate = new PlayerUpdateToServer() { followers = new long[] { sentry.EntityId }, operation = (newOrders[1] ? "add" : "del") };
				OnPlayerUpdated(player, newUpdate);
			}
		}

		private TextCommandResult OnKingdomsCommand(TextCommandCallingArgs args) {
			string fullargs = (string)args[1];
			string callerID = args.Caller.Player.PlayerUID;
			string langCode = args.LanguageCode;
			IPlayer thisPlayer = args.Caller.Player;
			IPlayer thatPlayer = serverAPI.World.PlayerByUid(serverAPI.PlayerData.GetPlayerDataByLastKnownName(fullargs)?.PlayerUID) ?? null;
			Kingdom thisKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thisPlayer.Entity.WatchedAttributes.GetString("kingdomGUID")) ?? null;
			Kingdom thatKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomNAME.ToLowerInvariant() == fullargs?.ToLowerInvariant()) ?? null;
			// Determine privillege role level and if they are allowed to make new kingdoms/cultures.
			bool inKingdom = thisKingdom != null && thisKingdom.KingdomGUID != commonerGUID;
			bool theLeader = inKingdom && thisKingdom.LeadersGUID == callerID;
			bool usingArgs = fullargs != null && fullargs != "" && fullargs != " ";
			bool canInvite = inKingdom && GetRolesPriv(thisKingdom.MembersROLE, GetMemberRole(thisKingdom.PlayersINFO, callerID))[5];
			bool adminPass = args.Caller.HasPrivilege(Privilege.controlserver);
			bool canCreate = args.Caller.GetRole(serverAPI).PrivilegeLevel >= serverAPI.World.Config.GetAsInt("MinCreateLevel", -1);
			bool maxCreate = maxKingdoms == -1 ? true : (kingdomList.Count + 1) < maxKingdoms;

			string[] keywords = {
				GetL(langCode, "entries-keyword-kingdom"),
				GetL(langCode, "entries-keyword-player"),
				GetL(langCode, "entries-keyword-players")
			};

			if (adminPass && inKingdom) {
				try { serverAPI.Logger.Debug(ListedAllData(serverAPI, thisKingdom.KingdomGUID)); } catch { }
			}

			switch ((string)args[0]) {
				// Creates new owned Kingdom.
				case "create":
					if (!usingArgs) {
						return TextCommandResult.Error(SetL(langCode, "command-error-argsnone", string.Concat(args.Command.ToString(), (string)args[0])));
					} else if (!kingdomList.NameAvailable(fullargs)) {
						return TextCommandResult.Error(SetL(langCode, "command-error-nametook", keywords[0]));
					} else if (kingdomList.PartOfKingdom(thisPlayer)) {
						return TextCommandResult.Error(SetL(langCode, "command-error-ismember", keywords[0]));
					} else if (!canCreate) {
						return TextCommandResult.Error(SetL(langCode, "command-error-badperms", (string)args[0]));
					} else if (!maxCreate) {
						return TextCommandResult.Error(SetL(langCode, "command-error-capacity", keywords[0]));
					}
					CreateKingdom(null, fullargs, thisPlayer.PlayerUID, !inKingdom);
					return TextCommandResult.Success(SetL(langCode, "command-success-create", fullargs));
				// Deletes the owned Kingdom.
				case "delete":
					if (usingArgs && adminPass && thatKingdom is not null) {
						DeleteKingdom(thatKingdom.KingdomGUID);
						return TextCommandResult.Success(SetL(langCode, "command-success-delete", fullargs));
					} else if (!usingArgs || thatKingdom is null) {
						return TextCommandResult.Error(SetL(langCode, "command-error-cantfind", keywords[0]));
					} else if (!adminPass && thatKingdom.LeadersGUID != callerID) {
						return TextCommandResult.Error(SetL(langCode, "command-error-badperms", (string)args[0]));
					}
					DeleteKingdom(thatKingdom.KingdomGUID);
					return TextCommandResult.Success(SetL(langCode, "command-success-delete", fullargs));
				// Updates and changes kingdom properties.
				case "update":
					if (!inKingdom) {
						return TextCommandResult.Error(SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (!usingArgs) {
						return TextCommandResult.Error(SetL(langCode, "command-error-argument", fullargs));
					} else if (!adminPass && thisKingdom.LeadersGUID != callerID) {
						return TextCommandResult.Error(SetL(langCode, "command-error-badperms", fullargs));
					}
					string[] fullset = { fullargs };
					try { fullset = fullargs.Split(' '); } catch { }
					string results = ChangeKingdom(langCode, thisKingdom.KingdomGUID, fullset[0], fullset[1], string.Join(' ', fullset.Skip(2)));
					thisPlayer.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/writing"), thisPlayer.Entity);
					return TextCommandResult.Success(results);
				// Invites player to join Kingdom.
				case "invite":
					/* TODO: THIS DOESN'T WANT TO PROPERLY SEND INVITES, DETERMINE IF THEY ARE GETTING THROUGH AND BEING SAVED. */
					if (!usingArgs && canInvite && thisPlayer.CurrentEntitySelection.Entity is EntityPlayer playerEnt) {
						thisKingdom.InvitesGUID.Add(playerEnt.PlayerUID);
						SaveKingdom();
						serverAPI.SendMessage(thatPlayer, 0, SetL((thatPlayer as IServerPlayer)?.LanguageCode ?? langCode, "command-message-invite", thisPlayer.PlayerName, thisKingdom.KingdomNAME), EnumChatType.Notification);
						return TextCommandResult.Success(SetL(langCode, "command-success-invite", playerEnt.Player.PlayerName));
					} else if (thatPlayer == null || !serverAPI.World.AllOnlinePlayers.Contains(thatPlayer)) {
						return TextCommandResult.Error(SetL(langCode, "command-error-noplayer", fullargs));
					} else if (thisKingdom.PlayersGUID.Contains(thatPlayer.PlayerUID)) {
						return TextCommandResult.Error(SetL(langCode, "command-error-playerin", thisKingdom.KingdomNAME));
					} else if (!canInvite) {
						return TextCommandResult.Error(SetL(langCode, "command-error-badperms", (string)args[0]));
					}
					thisKingdom.InvitesGUID.Add(thatPlayer.PlayerUID);
					SaveKingdom();
					serverAPI.SendMessage(thatPlayer, 0, SetL((thatPlayer as IServerPlayer)?.LanguageCode ?? langCode, "command-message-invite", thisPlayer.PlayerName, thisKingdom.KingdomNAME), EnumChatType.Notification);
					return TextCommandResult.Success(SetL(langCode, "command-success-invite", fullargs));
				// Accept invites and requests.
				case "accept":
					if (!usingArgs) {
						return TextCommandResult.Success(KingdomInvite(callerID, canInvite));
					} else if (thatKingdom != null && inKingdom && theLeader && thisKingdom.PeaceOffers.Contains(thatKingdom.KingdomGUID)) {
						serverAPI.BroadcastMessageToAllGroups(SetL(langCode, "command-message-peacea", thisKingdom.KingdomLONG, thatKingdom.KingdomLONG), EnumChatType.Notification);
						EndWarKingdom(thisKingdom.KingdomGUID, thatKingdom.KingdomGUID);
						return TextCommandResult.Success(SetL(langCode, "command-success-treaty", fullargs));
					} else if (PlayerIsOnline(fullargs) && inKingdom && thisKingdom.RequestGUID.Contains(thatPlayer.PlayerUID)) {
						if (!canInvite) {
							return TextCommandResult.Error(SetL(langCode, "command-error-badperms", string.Concat((string)args[0], thatPlayer.PlayerName)));
						}
						thisKingdom.RequestGUID.Remove(thatPlayer.PlayerUID);
						SwitchKingdom(thatPlayer as IServerPlayer, thisKingdom.KingdomGUID);
						return TextCommandResult.Success(SetL(langCode, "command-success-accept", thatPlayer.PlayerName));
					} else if (thatKingdom != null && thatKingdom.InvitesGUID.Contains(thisPlayer.PlayerUID)) {
						var leadPlayer = serverAPI.World.PlayerByUid(thatKingdom.LeadersGUID);
						serverAPI.SendMessage(leadPlayer, 0, SetL((leadPlayer as IServerPlayer)?.LanguageCode ?? langCode, "command-message-accept", thisPlayer.PlayerName, thatKingdom.KingdomLONG), EnumChatType.Notification);
						SwitchKingdom(thisPlayer as IServerPlayer, thatKingdom.KingdomGUID);
						return TextCommandResult.Success(SetL(langCode, "command-success-accept", thatKingdom.KingdomNAME));
					}
					return TextCommandResult.Error(SetL(langCode, "command-error-noinvite", keywords[0]));
				// Reject invites and requests.
				case "reject":
					if (!usingArgs) {
						return TextCommandResult.Success(KingdomInvite(callerID, canInvite));
					} else if (thatKingdom != null && inKingdom && theLeader && thisKingdom.PeaceOffers.Contains(thatKingdom.KingdomGUID)) {
						serverAPI.BroadcastMessageToAllGroups(SetL(langCode, "command-message-peacer", thisKingdom.KingdomLONG, thatKingdom.KingdomLONG), EnumChatType.Notification);
						kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thisKingdom.KingdomGUID).PeaceOffers.Remove(thatKingdom.KingdomGUID);
						SaveKingdom();
						return TextCommandResult.Success(SetL(langCode, "command-failure-treaty", fullargs));
					} else if (thatPlayer != null && inKingdom && thisKingdom.RequestGUID.Contains(thatPlayer.PlayerUID)) {
						if (!canInvite) {
							return TextCommandResult.Error(SetL(langCode, "command-error-badperms", string.Concat((string)args[0], thatPlayer.PlayerName)));
						}
						thisKingdom.RequestGUID.Remove(thatPlayer.PlayerUID);
						return TextCommandResult.Success(SetL(langCode, "command-success-reject", thatPlayer.PlayerName));
					} else if (thatKingdom != null && thatKingdom.InvitesGUID.Contains(thisPlayer.PlayerUID)) {
						thatKingdom.InvitesGUID.Remove(callerID);
						var leadPlayer = serverAPI.World.PlayerByUid(thatKingdom.LeadersGUID);
						serverAPI.SendMessage(leadPlayer, 0, SetL((leadPlayer as IServerPlayer)?.LanguageCode ?? langCode, "command-choices-reject", thisPlayer.PlayerName, thatKingdom.KingdomLONG), EnumChatType.Notification);
						return TextCommandResult.Success(SetL(langCode, "command-success-reject", thatKingdom.KingdomNAME));
					}
					return TextCommandResult.Error(SetL(langCode, "command-error-noinvite", keywords[0]));
				// Removes player from Kingdom.
				case "remove":
					if (thatPlayer == null || !serverAPI.World.AllOnlinePlayers.Contains(thatPlayer)) {
						return TextCommandResult.Error(SetL(langCode, "command-error-noplayer", fullargs));
					} else if (!thisKingdom.PlayersGUID.Contains(thatPlayer.PlayerUID)) {
						return TextCommandResult.Error(SetL(langCode, "command-error-nomember", thisKingdom.KingdomNAME));
					} else if (!adminPass && kingdomList.GetLeadersGUID(thisKingdom.KingdomGUID) != callerID) {
						return TextCommandResult.Error(SetL(langCode, "command-error-badperms", string.Concat((string)args[0], thatPlayer.PlayerName)));
					}
					/* TODO: ADD SPECIAL CIRCUMSTANCE BASED ON PRIVILEGE AND ELECTIONS */
					thisKingdom.PlayersINFO.Remove(GetMemberInfo(thisKingdom.PlayersINFO, thatPlayer.PlayerUID));
					thisKingdom.PlayersGUID.Remove(thatPlayer.PlayerUID);
					SaveKingdom();
					return TextCommandResult.Success(SetL(langCode, "command-success-remove", fullargs));
				// Requests access to Leader.
				case "become":
					if (!kingdomList.KingdomExists(kingdomList.GetKingdom(fullargs)?.KingdomGUID ?? null)) {
						return TextCommandResult.Error(SetL(langCode, "command-error-notexist", keywords[0]));
					} else if (kingdomList.GetKingdom(fullargs).PlayersGUID.Contains(thisPlayer.PlayerUID)) {
						return TextCommandResult.Error(SetL(langCode, "command-error-ismember", keywords[0]));
					}
					/* TODO: ADD REQUEST TO JOIN TO QUERY thatKingdom.RequestGUID */
					thatKingdom.RequestGUID.Add(thisPlayer.PlayerUID);
					SaveKingdom();
					serverAPI.SendMessage(serverAPI.World.PlayerByUid(thatKingdom.LeadersGUID), 0, Set("command-message-invite", thisPlayer.PlayerName, thisKingdom.KingdomNAME), EnumChatType.Notification);
					return TextCommandResult.Success(SetL(langCode, "command-success-become", fullargs));
				// Leaves current Kingdom.
				case "depart":
					if (!thisPlayer.Entity.HasBehavior<EntityBehaviorLoyalties>()) {
						return TextCommandResult.Error(SetL(langCode, "command-error-unknowns", (string)args[0]));
					} else if (!inKingdom) {
						return TextCommandResult.Error(SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (serverAPI.World.Claims.All.Any(landClaim => landClaim.OwnedByPlayerUid == thisPlayer.PlayerUID)) {
						return TextCommandResult.Error(SetL(langCode, "command-error-ownsland", keywords[0]));
					}
					string kingdomName = thisKingdom.KingdomNAME;
					SwitchKingdom(thisPlayer as IServerPlayer, commonerGUID);
					return TextCommandResult.Success(SetL(langCode, "command-success-depart", kingdomName));
				// Revolt against the Kingdom.
				case "revolt":
					if (!inKingdom) {
						return TextCommandResult.Error(SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (theLeader) {
						return TextCommandResult.Error(SetL(langCode, "command-error-isleader", (string)args[0]));
					}
					thisKingdom.OutlawsGUID.Add(callerID);
					SaveKingdom();
					return TextCommandResult.Success(SetL(langCode, "command-success-revolt", thisKingdom.KingdomNAME));
				// Rebels against the Kingdom.
				case "rebels":
					if (!inKingdom) {
						return TextCommandResult.Error(SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (theLeader) {
						return TextCommandResult.Error(SetL(langCode, "command-error-isleader", (string)args[0]));
					}
					thisKingdom.OutlawsGUID.Add(callerID);
					SwitchKingdom(thisPlayer as IServerPlayer, commonerGUID);
					return TextCommandResult.Success(SetL(langCode, "command-success-rebels", thisKingdom.KingdomNAME));
				// Declares war on Kingdom.
				case "attack":
					if (!kingdomList.KingdomExists(kingdomList.GetKingdom(fullargs)?.KingdomGUID ?? null)) {
						return TextCommandResult.Error(SetL(langCode, "command-error-notexist", fullargs));
					} else if (!inKingdom) {
						return TextCommandResult.Error(SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (kingdomList.GetLeadersGUID(thisKingdom.KingdomGUID) != callerID) {
						return TextCommandResult.Error(SetL(langCode, "command-error-badperms", (string)args[0]));
					} else if (thisKingdom.EnemiesGUID.Contains(thatKingdom.KingdomGUID)) {
						return TextCommandResult.Error(SetL(langCode, "command-error-atwarnow", thatKingdom.KingdomNAME));
					} else {
						SetWarKingdom(thisKingdom.KingdomGUID, thatKingdom.KingdomGUID);
						return TextCommandResult.Success(SetL(langCode, "command-success-attack", fullargs));
					}
				// Declares peace to Kingdom.
				case "treaty":
					if (!kingdomList.KingdomExists(kingdomList.GetKingdom(fullargs)?.KingdomGUID ?? null)) {
						return TextCommandResult.Error(SetL(langCode, "command-error-notexist", fullargs));
					} else if (!inKingdom) {
						return TextCommandResult.Error(SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (kingdomList.GetLeadersGUID(thisKingdom.KingdomGUID) != callerID) {
						return TextCommandResult.Error(SetL(langCode, "command-error-badperms", (string)args[0]));
					} else if (!thisKingdom.EnemiesGUID.Contains(thatKingdom.KingdomGUID)) {
						return TextCommandResult.Error(SetL(langCode, "command-error-notatwar", thatKingdom.KingdomNAME));
					} else if (thisKingdom.PeaceOffers.Contains(thatKingdom.KingdomGUID) || kingdomIDs.Contains(thatKingdom.KingdomGUID)) {
						EndWarKingdom(thisKingdom.KingdomGUID, thatKingdom.KingdomGUID);
						return TextCommandResult.Success(SetL(langCode, "command-success-treaty", fullargs));
					}
					kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thatKingdom.KingdomGUID).PeaceOffers.Add(thisKingdom.KingdomGUID);
					SaveKingdom();
					WriteToDisk();
					serverAPI.BroadcastMessageToAllGroups(SetL(langCode, "command-message-peaces", thisKingdom.KingdomLONG, thatKingdom.KingdomLONG), EnumChatType.Notification);
					return TextCommandResult.Success("");
				// Sets an enemy of the Kingdom.
				case "outlaw":
					if (!inKingdom) {
						return TextCommandResult.Error(SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (!PlayerIsOnline(fullargs)) {
						return TextCommandResult.Error(SetL(langCode, "command-error-noplayer", fullargs));
					} else if (!theLeader) {
						return TextCommandResult.Error(SetL(langCode, "command-error-badperms", (string)args[0]));
					}
					thisKingdom.OutlawsGUID.Add(thatPlayer.PlayerUID);
					SaveKingdom();
					WriteToDisk();
					return TextCommandResult.Success(SetL(langCode, "command-success-outlaw", thatPlayer.PlayerName));
				// Pardons an enemy of the Kingdom.
				case "pardon":
					if (!inKingdom) {
						return TextCommandResult.Error(SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (!PlayerIsOnline(fullargs)) {
						return TextCommandResult.Error(SetL(langCode, "command-error-noplayer", fullargs));
					} else if (!theLeader) {
						return TextCommandResult.Error(SetL(langCode, "command-error-badperms", (string)args[0]));
					}
					thisKingdom.OutlawsGUID.Remove(thatPlayer.PlayerUID);
					SaveKingdom();
					WriteToDisk();
					return TextCommandResult.Success(SetL(langCode, "command-success-pardon", thatPlayer.PlayerName));
				// Gets all enemies of the Kingdom.
				case "wanted":
					if (!inKingdom) {
						return TextCommandResult.Error(SetL(langCode, "command-error-nopartof", keywords[0]));
					}
					return TextCommandResult.Success(SetL(langCode, "command-success-wanted", keywords[2], thisKingdom.KingdomLONG, KingdomWanted(thisKingdom.OutlawsGUID)));
			}
			if ((string)args[0] == null || ((string)args[0]).Contains("help")) {
				return TextCommandResult.Success(CommandInfo(langCode, "kingdom", "help"));
			}
			if (((string)args[0]).Contains("desc")) {
				return TextCommandResult.Success(CommandInfo(langCode, "kingdom", "desc"));
			}
			return TextCommandResult.Error(GetL(langCode, "command-help-kingdom"));
		}

		private TextCommandResult OnCulturesCommand(TextCommandCallingArgs args) {
			string fullargs = (string)args[1];
			string callerID = args.Caller.Player.PlayerUID;
			string langCode = args.LanguageCode;
			IPlayer thisPlayer = args.Caller.Player;
			IPlayer thatPlayer = serverAPI.World.PlayerByUid(serverAPI.PlayerData.GetPlayerDataByLastKnownName(fullargs)?.PlayerUID) ?? null;
			Culture thisCulture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == thisPlayer.Entity.WatchedAttributes.GetString("cultureGUID")) ?? null;
			Culture thatCulture = cultureList.Find(cultureMatch => cultureMatch.CultureNAME.ToLowerInvariant() == fullargs?.ToLowerInvariant()) ?? null;
			// Determine privillege role level and if they are allowed to make new kingdoms/cultures.
			bool inCulture = thisCulture != null && thisCulture.CultureGUID != commonerGUID;
			bool usingArgs = fullargs != null && fullargs != "" && fullargs != " ";
			bool adminPass = args.Caller.HasPrivilege(Privilege.controlserver);
			bool canCreate = args.Caller.GetRole(serverAPI).PrivilegeLevel >= serverAPI.World.Config.GetAsInt("MinCreateLevel", -1);
			bool maxCreate = maxCultures == -1 ? true : (cultureList.Count + 1) < maxCultures;

			string[] keywords = {
				GetL(langCode, "entries-keyword-culture"),
				GetL(langCode, "entries-keyword-player"),
				GetL(langCode, "entries-keyword-players")
			};

			switch ((string)args[0]) {
				// Creates brand new Culture.
				case "create":
					if (!usingArgs) {
						return TextCommandResult.Error(SetL(langCode, "command-error-argsnone", (string)args[0]));
					} else if (!cultureList.NameAvailable(fullargs)) {
						return TextCommandResult.Error(SetL(langCode, "command-error-nametook", keywords[0]));
					} else if (!canCreate) {
						return TextCommandResult.Error(SetL(langCode, "command-error-badperms", (string)args[0]));
					} else if (!maxCreate) {
						return TextCommandResult.Error(SetL(langCode, "command-error-capacity", keywords[0]));
					}
					CreateCulture(null, fullargs.UcFirst(), callerID, !inCulture);
					return TextCommandResult.Success(SetL(langCode, "command-success-create", fullargs.UcFirst()));
				// Deletes existing culture.
				case "delete":
					if (!cultureList.Exists(cultureMatch => cultureMatch.CultureNAME.ToLowerInvariant() == fullargs?.ToLowerInvariant())) {
						return TextCommandResult.Error(SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (!adminPass) {
						return TextCommandResult.Error(SetL(langCode, "command-error-notadmin", (string)args[0]));
					}
					DeleteCulture(thatCulture.CultureGUID);
					return TextCommandResult.Success(SetL(langCode, "command-success-delete", fullargs));
				// Edits existing culture.
				case "update":
					if (!inCulture) {
						return TextCommandResult.Error(SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (!adminPass && thisCulture.FounderGUID != callerID) {
						return TextCommandResult.Error(SetL(langCode, "command-error-badperms", (string)args[0]));
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
						thisCulture.InvitesGUID.Add(thatPlayer.PlayerUID);
						SaveCulture();
						return TextCommandResult.Success(SetL(langCode, "command-success-invite", thatPlayer.PlayerName));
					} else if (!usingArgs) {
						return TextCommandResult.Error(SetL(langCode, "command-error-argsnone", keywords[1]));
					} else if (thatPlayer == null) {
						return TextCommandResult.Error(SetL(langCode, "command-error-noplayer", fullargs));
					}
					thisCulture.InvitesGUID.Add(thatPlayer.PlayerUID);
					SaveCulture();
					return TextCommandResult.Success(SetL(langCode, "command-success-invite", thatPlayer.PlayerName));
				// Accept invite to join culture.
				case "accept":
					if (!usingArgs) {
						return TextCommandResult.Success(CultureInvite(callerID));
					} else if (thatCulture != null && thatCulture.InvitesGUID.Contains(thisPlayer.PlayerUID)) {
						SwitchCulture(thisPlayer as IServerPlayer, thatCulture.CultureGUID);
						return TextCommandResult.Success(SetL(langCode, "command-success-accept", thatCulture.CultureNAME));
					}
					return TextCommandResult.Error(SetL(langCode, "command-error-noinvite", keywords[0]));
				// Reject invite to join culture.
				case "reject":
					if (!usingArgs) {
						return TextCommandResult.Success(CultureInvite(callerID));
					} else if (inCulture && thatPlayer != null && thisCulture.InvitesGUID.Contains(thatPlayer.PlayerUID)) {
						thisCulture.InvitesGUID.Remove(thatPlayer.PlayerUID);
						SaveCulture();
						return TextCommandResult.Success(SetL(langCode, "command-success-reject", thatPlayer.PlayerUID));
					} else if (thatCulture != null && thatCulture.InvitesGUID.Contains(thisPlayer.PlayerUID)) {
						thatCulture.InvitesGUID.Remove(thisPlayer.PlayerUID);
						SaveCulture();
						return TextCommandResult.Success(SetL(langCode, "command-success-reject", thatCulture.CultureNAME));
					}
					return TextCommandResult.Error(SetL(langCode, "command-error-noinvite", keywords[0]));
				case "remove":
					if (!adminPass) {
						return TextCommandResult.Error(SetL(langCode, "command-error-notadmin", (string)args[0]));
					} else if (!inCulture) {
						return TextCommandResult.Error(SetL(langCode, "command-error-nopartof", keywords[0]));
					} else if (thatPlayer == null) {
						return TextCommandResult.Error(SetL(langCode, "command-error-noplayer", fullargs));
					}
					thatPlayer.Entity.WatchedAttributes.SetString("cultureGUID", commonerGUID);
					return TextCommandResult.Success(SetL(langCode, "command-success-remove", thisCulture.CultureNAME));
				case "become":
					if (!adminPass) {
						return TextCommandResult.Error(SetL(langCode, "command-error-notadmin", (string)args[0]));
					} else if (thatCulture != null || !!cultureList.Exists(cultureMatch => cultureMatch.CultureNAME.ToLowerInvariant() == fullargs?.ToLowerInvariant())) {
						return TextCommandResult.Error(SetL(langCode, "command-error-notexist", keywords[0]));
					}
					SwitchCulture(thisPlayer as IServerPlayer, thatCulture.CultureGUID);
					return TextCommandResult.Success(SetL(langCode, "command-success-become", thisCulture.CultureNAME));
			}
			if (((string)args[0]).Contains("help")) {
				return TextCommandResult.Success(CommandInfo(langCode, "culture", "help"));
			}
			if (((string)args[0]).Contains("desc")) {
				return TextCommandResult.Success(CommandInfo(langCode, "culture", "desc"));
			}
			return TextCommandResult.Error(GetL(langCode, "command-help-culture"));
		}

		public void CreateKingdom(string newKingdomGUID, string newKingdomNAME, string founderGUID, bool autoJoin) {
			Kingdom newKingdom = new Kingdom();
			newKingdom.KingdomGUID = RandomGuid(newKingdomGUID, 8, kingdomList.GetKingdomGUIDs());
			newKingdom.KingdomTYPE = CorrectedType(newKingdomNAME);
			newKingdom.KingdomNAME = CorrectedName(newKingdom.KingdomTYPE, newKingdomNAME, true, false);
			newKingdom.KingdomLONG = CorrectedName(newKingdom.KingdomTYPE, newKingdomNAME, false, true);
			newKingdom.KingdomDESC = null;
			newKingdom.KingdomHEXA = RandomCode(000);
			newKingdom.KingdomHEXB = RandomCode(060);
			newKingdom.KingdomHEXC = RandomCode(120);
			newKingdom.LeadersGUID = null;
			newKingdom.LeadersNAME = CorrectedLead(newKingdom.KingdomTYPE, true, false, false);
			newKingdom.LeadersLONG = CorrectedLead(newKingdom.KingdomTYPE, false, true, false);
			newKingdom.LeadersDESC = CorrectedLead(newKingdom.KingdomTYPE, false, false, true);
			newKingdom.MembersROLE = CorrectedRole(newKingdom.KingdomTYPE);
			newKingdom.FoundedMETA = DateTime.Now.ToLongDateString();
			newKingdom.FoundedDATE = serverAPI.World.Calendar.PrettyDate();
			newKingdom.FoundedHOUR = serverAPI.World.Calendar.TotalHours;
			newKingdom.CurrentVOTE = null;
			if (autoJoin && founderGUID != null && serverAPI.World.PlayerByUid(founderGUID) is IPlayer founder) {
				founder.Entity.WatchedAttributes.SetString("kingdomGUID", newKingdom.KingdomGUID);
				newKingdom.LeadersGUID = founderGUID;
				newKingdom.PlayersGUID.Add(founderGUID);
				newKingdom.PlayersINFO.Add(PlayerDetails(founderGUID, newKingdom.MembersROLE, GetLeaderRole(newKingdom.MembersROLE)));
				founder.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/cashregister"), founder.Entity);
			}
			kingdomList.Add(newKingdom);
			SaveKingdom();
			if (autoJoin) {
				WriteToDisk();
				UpdateSentries(serverAPI.World.PlayerByUid(founderGUID).Entity.ServerPos.XYZ);
			}
		}

		public void CreateCulture(string newCultureGUID, string newCultureNAME, string founderGUID, bool autoJoin) {
			Culture newCulture = new Culture();
			newCulture.CultureGUID = RandomGuid(newCultureGUID, 8, cultureList.GetCultureGUIDs());
			newCulture.CultureNAME = CorrectedName(newCultureNAME);
			newCulture.CultureLONG = CorrectedLong(newCultureNAME);
			newCulture.CultureDESC = null;
			newCulture.FounderGUID = founderGUID;
			newCulture.FoundedMETA = DateTime.Now.ToLongDateString();
			newCulture.FoundedDATE = serverAPI.World.Calendar.PrettyDate();
			newCulture.FoundedHOUR = serverAPI.World.Calendar.TotalHours;
			newCulture.Predecessor = null;
			newCulture.MFirstNames = Open(serverAPI.World.Config.GetString("BasicMascNames")).ToHashSet<string>();
			newCulture.FFirstNames = Open(serverAPI.World.Config.GetString("BasicFemmNames")).ToHashSet<string>();
			newCulture.FamilyNames = Open(serverAPI.World.Config.GetString("BasicLastNames")).ToHashSet<string>();
			if (autoJoin && founderGUID != null && serverAPI.World.PlayerByUid(founderGUID) is IPlayer founder) {
				string oldCultureGUID = founder.Entity.WatchedAttributes.GetString("cultureGUID");
				newCulture.Predecessor = oldCultureGUID;
				if (oldCultureGUID != seraphimGUID && cultureList.Exists(cultureMatch => cultureMatch.CultureGUID == oldCultureGUID)) {
					Culture oldCulture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == oldCultureGUID);
					oldCulture.PlayersGUID.Remove(founderGUID);
					if (oldCulture.FounderGUID == founderGUID && oldCulture.PlayersGUID.Count > 0) {
						oldCulture.FounderGUID = oldCulture.PlayersGUID.ToArray()[0];
					}
					newCulture.MFirstNames = oldCulture.MFirstNames;
					newCulture.FFirstNames = oldCulture.FFirstNames;
					newCulture.FamilyNames = oldCulture.FamilyNames;
					newCulture.SkinColours = oldCulture.SkinColours;
					newCulture.EyesColours = oldCulture.EyesColours;
					newCulture.HairColours = oldCulture.HairColours;
					newCulture.HairsStyles = oldCulture.HairsStyles;
					newCulture.HairsExtras = oldCulture.HairsExtras;
					newCulture.FacesStyles = oldCulture.FacesStyles;
					newCulture.FacesBeards = oldCulture.FacesBeards;
					newCulture.WoodsBlocks = oldCulture.WoodsBlocks;
					newCulture.StoneBlocks = oldCulture.StoneBlocks;
				}
				founder.Entity.WatchedAttributes.SetString("cultureGUID", newCulture.CultureGUID);
				newCulture.PlayersGUID.Add(founderGUID);
				var AvailableSkinParts = founder.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
				foreach (var skinpart in AvailableSkinParts.AppliedSkinParts) {
					switch (skinpart.PartCode) {
						case "baseskin": newCulture.SkinColours.Add(skinpart.Code.ToString()); continue;
						case "eyecolor": newCulture.EyesColours.Add(skinpart.Code.ToString()); continue;
						case "haircolor": newCulture.HairColours.Add(skinpart.Code.ToString()); continue;
						case "hairbase": newCulture.HairsStyles.Add(skinpart.Code.ToString()); continue;
						case "hairextra": newCulture.HairsExtras.Add(skinpart.Code); continue;
						case "mustache": newCulture.FacesStyles.Add(skinpart.Code); continue;
						case "beard": newCulture.FacesBeards.Add(skinpart.Code); continue;
					}
				}
				founder.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/writing"), founder.Entity);
			}
			cultureList.Add(newCulture);
			SaveCulture();
			if (autoJoin) {
				WriteToDisk();
			}
		}

		public void DeleteKingdom(string kingdomGUID) {
			if (kingdomGUID == null || kingdomGUID == commonerGUID || kingdomGUID == banditryGUID || !kingdomList.KingdomExists(kingdomGUID)) { return; }
			Kingdom kingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID);
			foreach (string member in kingdomList.GetOnlinesGUIDs(kingdom.KingdomGUID, serverAPI)) {
				serverAPI.World.PlayerByUid(member)?.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/deepbell"), serverAPI.World.PlayerByUid(member)?.Entity);
				UpdateSentries(serverAPI.World.PlayerByUid(member)?.Entity.ServerPos.XYZ);
			}
			foreach (var entity in serverAPI.World.LoadedEntities.Values) {
				if (!entity.WatchedAttributes.HasAttribute("kingdomGUID")) {
					continue;
				} else if (entity.WatchedAttributes.GetString("kingdomGUID") == kingdomGUID) {
					entity.WatchedAttributes.SetString("kingdomGUID", commonerGUID);
				}
			}
			kingdomList.RemoveAll(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID);
			SaveKingdom();
			WriteToDisk();
		}

		public void DeleteCulture(string cultureGUID) {
			if (cultureGUID == seraphimGUID || cultureGUID == clockwinGUID) {
				return;
			}
			Culture culture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == cultureGUID);
			foreach (string member in cultureList.GetOnlinesGUIDs(culture.CultureGUID, serverAPI)) {
				serverAPI.World.PlayerByUid(member)?.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/deepbell"), serverAPI.World.PlayerByUid(member)?.Entity);
			}
			foreach (var entity in serverAPI.World.LoadedEntities.Values) {
				if (!entity.WatchedAttributes.HasAttribute("cultureGUID")) {
					continue;
				} else if (entity.WatchedAttributes.GetString("cultureGUID") == cultureGUID) {
					entity.WatchedAttributes.SetString("cultureGUID", seraphimGUID);
				}
			}
			foreach (var player in serverAPI.World.AllPlayers) {
				if (!player.Entity.WatchedAttributes.HasAttribute("cultureGUID")) {
					continue;
				} else if (player.Entity.WatchedAttributes.GetString("cultureGUID") == cultureGUID) {
					player.Entity.WatchedAttributes.SetString("cultureGUID", seraphimGUID);
				}
			}
			cultureList.RemoveAll(cultureMatch => cultureMatch.CultureGUID == cultureGUID);
			SaveCulture();
			WriteToDisk();
		}

		public void SwitchKingdom(IServerPlayer caller, string kingdomGUID, string specificROLE = null) {
			Kingdom oldKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == caller.Entity.WatchedAttributes.GetString("kingdomGUID", commonerGUID));
			Kingdom newKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID);
			oldKingdom.PlayersGUID.Remove(caller.PlayerUID);
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
			if (kingdomGUID != commonerGUID && kingdomGUID != banditryGUID) {
				newKingdom.PlayersGUID.Add(caller.PlayerUID);
				newKingdom.PlayersINFO.Add(PlayerDetails(caller.PlayerUID, newKingdom.MembersROLE, specificROLE));
			}
			caller.Entity.WatchedAttributes.SetString("kingdomGUID", kingdomGUID);
			if (oldKingdom.PlayersGUID.Count == 0 && kingdomGUID != commonerGUID && kingdomGUID != banditryGUID) {
				DeleteKingdom(oldKingdom.KingdomGUID);
			} else if (oldKingdom.LeadersGUID == caller.PlayerUID && oldKingdom.PlayersGUID.Count > 0) {
				/* TODO: Start Elections if Kingdom is REPUBLIC or assign by RANK! */
				oldKingdom.LeadersGUID = MostSeniority(serverAPI, oldKingdom.KingdomGUID);
			}
			UpdateSentries(caller.Entity.ServerPos.XYZ);
			SaveKingdom();
			WriteToDisk();
		}

		public void SwitchCulture(IServerPlayer caller, string cultureGUID) {
			Culture oldCulture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == caller.Entity.WatchedAttributes.GetString("cultureGUID", seraphimGUID));
			Culture newCulture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == cultureGUID);
			oldCulture.InvitesGUID.Remove(caller.PlayerUID);
			newCulture.InvitesGUID.Remove(caller.PlayerUID);
			oldCulture.PlayersGUID.Remove(caller.PlayerUID);
			caller.Entity.WatchedAttributes.SetString("cultureGUID", cultureGUID);
			caller.Entity.WatchedAttributes.SetString("cultureNAME", newCulture.CultureNAME);
			if (cultureGUID != seraphimGUID && cultureGUID != clockwinGUID) {
				newCulture.PlayersGUID.Add(caller.PlayerUID);
			}
			SaveCulture();
			WriteToDisk();
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
			string[] positives = { "t", "y", "true", "yes", "yep", "shi", "ja", "tak", "ye", "si", "sim", "oo", "hayir", "naeam" };
			string[] negatives = { "f", "n", "false", "no", "nope", "nyet", "bu", "nee", "nein", "nie", "aniyo", "nao", "hindi", "evet", "la" };
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
			WriteToDisk();
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
			if (!roleFnd) { return false; }
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
			WriteToDisk();
			foreach (var player in serverAPI.World.AllOnlinePlayers) {
				serverAPI.SendMessage(player, GlobalConstants.GeneralChatGroup, SetL((player as IServerPlayer).LanguageCode, "command-message-attack", kingdomONE.KingdomNAME, kingdomTWO.KingdomNAME), EnumChatType.Notification);
				UpdateSentries(player.Entity.ServerPos.XYZ);
			}
		}

		public void EndWarKingdom(string kingdomGUID1, string kingdomGUID2) {
			Kingdom kingdomONE = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID1);
			Kingdom kingdomTWO = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID2);
			kingdomONE?.EnemiesGUID.Remove(kingdomGUID2);
			kingdomTWO?.EnemiesGUID.Remove(kingdomGUID1);
			kingdomONE?.PeaceOffers.Remove(kingdomGUID2);
			kingdomTWO?.PeaceOffers.Remove(kingdomGUID1);
			SaveKingdom();
			WriteToDisk();
			foreach (var player in serverAPI.World.AllOnlinePlayers) {
				serverAPI.SendMessage(player, 0, SetL((player as IServerPlayer)?.LanguageCode, "command-message-treaty", kingdomONE.KingdomLONG, kingdomTWO.KingdomLONG), EnumChatType.Notification);
				UpdateSentries(player.Entity.ServerPos.XYZ);
			}
			if (kingdomONE.LeadersGUID == null || kingdomTWO.LeadersGUID == null) { return; }
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
			string invbox = SetL(langCode, "command-success-invbox", invites.Count.ToString(), string.Join("\n", invites));
			string reqbox = SetL(langCode, "command-success-reqbox", playerNames.Length.ToString(), string.Join("\n", playerNames));
			if (getRequests) { return invbox + "\n" + reqbox; }
			return invbox;
		}

		public string CultureInvite(string playersGUID) {
			string langCode = (serverAPI.World.PlayerByUid(playersGUID) as IServerPlayer)?.LanguageCode ?? "en";
			List<string> invites = new List<string>();
			foreach (Culture culture in cultureList) {
				if (culture.InvitesGUID.Contains(playersGUID)) {
					invites.Add(culture.CultureNAME);
				}
			}
			return SetL(langCode, "command-success-invbox", invites.Count.ToString(), string.Join("\n", invites));
		}

		public string ChangeKingdom(string langCode, string kingdomGUID, string subcomm, string subargs, string changes) {
			Kingdom kingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID);
			string[] keywords = { GetL(langCode, "entries-keyword-kingdom"), GetL(langCode, "entries-keyword-player"), GetL(langCode, "entries-keyword-players") };
			switch (subcomm.ToLower().Replace("color", "colour").Replace("add", "append").Replace("delete", "remove").Replace("recolor", "colour").Replace("change", "rename")) {
				case "append":
					switch (subargs) {
						case "roles": AddMemberRole(kingdomGUID, changes); break;
						default: return SetL(langCode, "command-help-update-kingdom-append", keywords[0]);
					}
					break;
				case "remove":
					switch (subargs) {
						case "roles": kingdom.MembersROLE = string.Join(":", kingdom.MembersROLE.Split(':').RemoveEntry(kingdom.MembersROLE.Replace("/T", "").Replace("/F", "").Split(':').IndexOf(changes))).TrimEnd(':');
							break;
						default:
							return SetL(langCode, "command-help-update-kingdom-remove", keywords[0]);
					}
					break;
				case "colour":
					switch (subargs.ToLower().Replace("second", "secnd").Replace("primary", "first").Replace("last", "third")) {
						case "first": kingdom.KingdomHEXA = GetHexCode(changes); break;
						case "secnd": kingdom.KingdomHEXB = GetHexCode(changes); break;
						case "third": kingdom.KingdomHEXC = GetHexCode(changes); break;
						default: return SetL(langCode, "command-help-update-kingdom-append", keywords[0]);
					}
					break;
				case "rename":
					switch (subargs) {
						case "title": kingdom.KingdomNAME = changes; break;
						case "longs": kingdom.KingdomLONG = changes; break;
						case "descs": kingdom.KingdomDESC = Mps(changes.Remove(changes.Length - 1).Remove(0).UcFirst()); break;
						case "ruler": kingdom.LeadersNAME = changes; break;
						case "names": kingdom.LeadersLONG = changes; break;
						case "short": kingdom.LeadersDESC = Mps(changes.Remove(changes.Length - 1).Remove(0).UcFirst()); break;
						default: return SetL(langCode, "command-help-update-kingdom-rename", keywords[0]);
					}
					break;
				case "player":
					switch (subargs) {
						case "roles": SetMemberRole(kingdomGUID, serverAPI.GetAPlayer(changes.Split(' ')[0]).PlayerUID ?? null, changes.Split(' ')[1]); break;
						default: return SetL(langCode, "command-help-update-kingdom-player", keywords[0]);
					}
					break;
				case "getall":
					switch (subargs) {
						case "basic": return ListedAllData(serverAPI, kingdomGUID);
						default: return SetL(langCode, "command-help-update-kingdom-getall", keywords[0]);
					}
				default: return $"{SetL(langCode, "command-failure-update", keywords[0])}\n{SetL(langCode, "command-help-update-kingdom", keywords[0])}";
			}
			SaveKingdom();
			WriteToDisk();
			return SetL(langCode, "command-success-update", keywords[0]);
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
						case "mascs": culture.MFirstNames.Add(changes.UcFirst()); break;
						case "femms": culture.FFirstNames.Add(changes.UcFirst()); break;
						case "names": culture.FamilyNames.Add(changes.UcFirst()); break;
						case "skins": culture.SkinColours.Add(changes); break;
						case "pupil": culture.EyesColours.Add(changes); break;
						case "hairs": culture.HairColours.Add(changes); break;
						case "style": culture.HairsStyles.Add(changes); break;
						case "extra": culture.HairsExtras.Add(changes); break;
						case "beard": culture.FacesBeards.Add(changes); break;
						default: return SetL(langCode, "command-help-update-culture-append", "culture");
					}
					break;
				case "remove":
					switch (subargs) {
						case "mascs": culture.MFirstNames.Remove(changes); break;
						case "femms": culture.FFirstNames.Remove(changes); break;
						case "names": culture.FamilyNames.Remove(changes); break;
						case "skins": culture.SkinColours.Remove(changes); break;
						case "pupil": culture.EyesColours.Remove(changes); break;
						case "hairs": culture.HairColours.Remove(changes); break;
						case "style": culture.HairsStyles.Remove(changes); break;
						case "extra": culture.HairsExtras.Remove(changes); break;
						case "beard": culture.FacesBeards.Remove(changes); break;
						default: return SetL(langCode, "command-help-update-culture-remove", "culture");
					}
					break;
				case "rename":
					switch (subargs) {
						case "title": culture.CultureNAME = changes; break;
						case "longs": culture.CultureLONG = changes; break;
						case "descs": culture.CultureDESC = Mps(changes.Remove(changes.Length - 1).Remove(0).UcFirst()); break;
						default: return SetL(langCode, "command-help-update-culture-rename", "culture");
					}
					break;
				case "getall":
					switch (subargs) {
						case "basic": return ListedCultures(serverAPI, cultureGUID);
						case "mascs": return Msg(culture.MFirstNames.ToArray());
						case "femms": return Msg(culture.FFirstNames.ToArray());
						case "names": return Msg(culture.FamilyNames.ToArray());
						case "skins": return Msg(culture.SkinColours.ToArray());
						case "pupil": return Msg(culture.EyesColours.ToArray());
						case "hairs": return Msg(culture.HairColours.ToArray());
						case "style": return Msg(culture.HairsStyles.ToArray());
						case "extra": return Msg(culture.HairsExtras.ToArray());
						case "beard": return Msg(culture.FacesBeards.ToArray());
						default: return SetL(langCode, "command-help-update-culture-getall", "culture");
					}
				default: return $"{SetL(langCode, "command-failure-update", "culture")}\n{SetL(langCode, "command-help-update", "culture")}";
			}
			SaveCulture();
			WriteToDisk();
			return SetL(langCode, "command-success-update", Get("entries-keyword-culture"));
		}

		private string CommandInfo(string langCode, string kindsof = "kingdom", string informs = "desc") {
			string[] commands = { "create", "delete", "update", "invite", "remove", "become", "depart", "revolt", "rebels", "attack", "treaty", "outlaw", "pardon", "wanted", "accept", "reject", "voting" };
			string langkeys = "command-" + informs + "-";
			string messages = "";
			foreach (string com in commands) {
				if (Lang.HasTranslation("vskingdom:" + langkeys + com)) {
					messages += "\n" + RefL(langCode, langkeys + com, kindsof, ("entries-keyword-" + kindsof), "entries-keyword-players", "entries-keyword-replies");
				}
			}
			return messages;
		}

		private bool PlayerIsOnline(string playerName) {
			if (serverAPI.PlayerData.GetPlayerDataByLastKnownName(playerName) != null) {
				string playerUID = serverAPI.PlayerData.GetPlayerDataByLastKnownName(playerName).PlayerUID;
				foreach (var player in serverAPI.World.AllOnlinePlayers) {
					if (player.PlayerUID == playerUID) {
						return true;
					}
				}
			}
			return false;
		}

		private string serverLang { get => serverAPI.World.Config.GetString("ServerLanguage", "en"); }
		private int maxCultures { get => serverAPI.World.Config.GetInt("MaxNewCultures", -1); }
		private int maxKingdoms { get => serverAPI.World.Config.GetInt("MaxNewKingdoms", -1); }
	}
}
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class PlayerUpdateToServer {
	public long[] followers;
	public string operation;
	public string kingdomID;
	public string cultureID;
}
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SentryUpdateToServer {
	public long entityUID;
	public string kingdomGUID;
	public string cultureGUID;
	public string leadersGUID;
}
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SentryUpdateToEntity {
	public string kingdomGUID;
	public string kingdomNAME;
	public string cultureGUID;
	public string cultureNAME;
	public string leadersGUID;
	public string leadersNAME;
	public string coloursHEXA;
	public string coloursHEXB;
	public string coloursHEXC;
}
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SentryOrdersToServer {
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
public class WeaponProps {
	public double ammoSpeed = 1.0d;
	public string ammoCodes = null;	
	public string idleAnims = "idle";
	public string walkAnims = "walk";
	public string moveAnims = "move";
	public string duckAnims = "duck";
	public string swimAnims = "swim";
	public string jumpAnims = "jump";
	public string drawAnims = "draw";
	public string fireAnims = "fire";
	public string loadAnims = "load";
	public string bashAnims = "bash";
	public string stabAnims = "bash";
	public AssetLocation[] drawAudio = { };
	public AssetLocation[] fireAudio = { };
	public string[] allCodes => new string[11] { idleAnims, walkAnims, moveAnims, duckAnims, swimAnims, jumpAnims, drawAnims, fireAnims, loadAnims, bashAnims, stabAnims };
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