using HarmonyLib;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class VSKingdom : ModSystem {
		Harmony harmony = new Harmony("badrabbit49.vskingdom");
		public static ICoreServerAPI serverAPI;
		public static ICoreClientAPI clientAPI;
		public bool fsmEnabled;

		private List<Kingdom> kingdomList;
		private List<Culture> cultureList;

		private Dictionary<string, Kingdom> playerDict = new Dictionary<string, Kingdom>();
		private Dictionary<long, Kingdom> entityDict = new Dictionary<long, Kingdom>();

		public override void Start(ICoreAPI api) {
			base.Start(api);
			// Block Classes
			api.RegisterBlockClass("BlockBody", typeof(BlockBody));
			api.RegisterBlockClass("BlockPost", typeof(BlockPost));
			// Block Behaviors
			api.RegisterBlockEntityBehaviorClass("Resupply", typeof(BlockBehaviorResupply));
			// Block Entities
			api.RegisterBlockEntityClass("BlockEntityBody", typeof(BlockEntityBody));
			api.RegisterBlockEntityClass("BlockEntityPost", typeof(BlockEntityPost));
			// Items
			api.RegisterItemClass("ItemBanner", typeof(ItemBanner));
			api.RegisterItemClass("ItemPeople", typeof(ItemPeople));
			// Entities
			api.RegisterEntity("EntitySentry", typeof(EntitySentry));
			// Entity Behaviors
			api.RegisterEntityBehaviorClass("KingdomFullNames", typeof(EntityBehaviorFullNames));
			api.RegisterEntityBehaviorClass("KingdomLoyalties", typeof(EntityBehaviorLoyalties));
			api.RegisterEntityBehaviorClass("SoldierDecayBody", typeof(EntityBehaviorDecayBody));
			// AITasks
			AiTaskRegistry.Register<AiTaskSentryFollow>("SentryFollow");
			AiTaskRegistry.Register<AiTaskSentryWaters>("SentryWaters");
			AiTaskRegistry.Register<AiTaskSentryReturn>("SentryReturn");
			AiTaskRegistry.Register<AiTaskSentryEscape>("SentryEscape");
			AiTaskRegistry.Register<AiTaskSentryHealth>("SentryHealth");
			AiTaskRegistry.Register<AiTaskSentryAttack>("SentryAttack");
			AiTaskRegistry.Register<AiTaskSentryRanged>("SentryRanged");
			AiTaskRegistry.Register<AiTaskSentrySearch>("SentrySearch");
			AiTaskRegistry.Register<AiTaskSentryWander>("SentryWander");
			// Patch Everything.
			if (!Harmony.HasAnyPatches("badrabbit49.vskingdom")) {
				harmony.PatchAll();
			}
			// Check if maltiez FSMlib is enabled.
			fsmEnabled = api.ModLoader.IsModEnabled("fsmlib");
			api.Logger.Notification("FSMLib is loaded? " + fsmEnabled + " Enabling content with fsmlib.");
			// Create chat commands for creation, deletion, invitation, and so of kingdoms.
			api.ChatCommands.Create("kingdom")
				.WithDescription(LangUtility.Get("command-desc"))
				.WithAdditionalInformation(LangUtility.Get("command-help"))
				.RequiresPrivilege(Privilege.chat)
				.RequiresPlayer()
				.WithArgs(api.ChatCommands.Parsers.Word("commands", new string[] { "create", "delete", "update", "invite", "become", "depart", "rename", "setent", "attack", "endwar", "getall", "setgov" }), api.ChatCommands.Parsers.OptionalWord("argument"))
				.HandleWith(new OnCommandDelegate(OnKingdomCommand));
			// Create chat commands for creation, deletion, invitation, and so of cultures.
			api.ChatCommands.Create("culture")
				.WithDescription(LangUtility.Get("command-desc"))
				.WithAdditionalInformation(LangUtility.Get("command-help"))
				.RequiresPrivilege(Privilege.chat)
				.RequiresPlayer()
				.WithArgs(api.ChatCommands.Parsers.Word("commands", new string[] { "create", "delete", "update", "rename" }), api.ChatCommands.Parsers.OptionalWord("argument"), api.ChatCommands.Parsers.OptionalWord("operations"))
				.HandleWith(new OnCommandDelegate(OnCultureCommand));
		}
		
		public override void StartClientSide(ICoreClientAPI capi) {
			base.StartClientSide(capi);
			clientAPI = capi;
			capi.Event.LevelFinalize += () => LevelFinalize(capi);
		}

		public override void StartServerSide(ICoreServerAPI sapi) {
			base.StartServerSide(sapi);
			serverAPI = sapi;
			sapi.Event.SaveGameCreated += MakeAllData;
			sapi.Event.GameWorldSave += SaveAllData;
			sapi.Event.SaveGameLoaded += LoadAllData;
			sapi.Event.PlayerJoin += PlayerJoinsGame;
			sapi.Event.PlayerDisconnect += PlayerLeaveGame;
			sapi.Event.ServerRunPhase(EnumServerRunPhase.Shutdown, SaveAllData);
		}

		public override void Dispose() {
			// Unload and Unpatch everything from the mod.
			harmony?.UnpatchAll(Mod.Info.ModID);
			base.Dispose();
		}

		public void CreateKingdom(string newKingdomGUID, string newKingdomNAME, string founderGUID, bool autoJoin) {
			Kingdom newKingdom = new Kingdom();
			newKingdom.KingdomGUID = GuidUtility.RandomizeGUID(newKingdomGUID);
			newKingdom.KingdomNAME = KingUtility.CorrectedNAME(newKingdomNAME);
			newKingdom.KingdomLONG = KingUtility.CorrectedNAME(newKingdomNAME);
			newKingdom.KingdomTYPE = KingUtility.CorrectedTYPE(newKingdomNAME);
			newKingdom.KingdomDESC = KingUtility.CorrectedTYPE(newKingdom.KingdomTYPE);
			newKingdom.LeadersGUID = null;
			newKingdom.LeadersNAME = null;
			newKingdom.LeadersLONG = KingUtility.CorrectedLONG(newKingdom.KingdomTYPE);
			newKingdom.LeadersDESC = null;
			newKingdom.FoundedMETA = DateTime.Now.ToLongDateString();
			newKingdom.FoundedDATE = serverAPI.World.Calendar.PrettyDate();
			newKingdom.FoundedHOUR = serverAPI.World.Calendar.TotalHours;
			if (autoJoin && founderGUID != null && serverAPI.World.PlayerByUid(founderGUID) is IServerPlayer founder) {
				founder.Entity.GetBehavior<EntityBehaviorLoyalties>().kingdomGUID = newKingdom.KingdomGUID;
				newKingdom.LeadersGUID = founderGUID;
				newKingdom.PlayerUIDs.Add(founderGUID);
				newKingdom.EntityUIDs.Add(founder.Entity.EntityId);
			}
			kingdomList.Add(newKingdom);
			SaveKingdom();
		}

		public void DeleteKingdom(Kingdom kingdom) {
			string kingdomGUID = kingdom.KingdomGUID;
			foreach (var entity in serverAPI.World.LoadedEntities.Values) {
				if (entity is not EntityHumanoid) {
					continue;
				}
				if (entity.HasBehavior<EntityBehaviorLoyalties>() && entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") == kingdomGUID) {
					entity.WatchedAttributes.GetTreeAttribute("loyalties")?.SetString("kingdom_guid", "00000000");
				}
			}
			kingdomList.Remove(kingdom);
			UpdateDicts();
		}

		public void DepartKingdom(IServerPlayer caller) {
			kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == caller.Entity?.GetBehavior<EntityBehaviorLoyalties>()?.kingdomGUID).PlayerUIDs.Remove(caller.PlayerUID);
			kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == caller.Entity?.GetBehavior<EntityBehaviorLoyalties>()?.kingdomGUID).EntityUIDs.Remove(caller.Entity.EntityId);
			caller.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.SetString("kingdom_guid", "00000000");
			UpdateDicts();
		}
		
		public void SetEntKingdom(Entity thisEnt, string kingdomUID) {
			thisEnt.GetBehavior<EntityBehaviorLoyalties>().SetKingdom(kingdomUID);
			UpdateDicts();
		}

		public void SetWarKingdom(string kingdomGUID1, string kingdomGUID2) {
			kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID1).AtWarsUIDs.Add(kingdomGUID2);
			kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID2).AtWarsUIDs.Add(kingdomGUID1);
		}

		public void EndWarKingdom(string kingdomGUID1, string kingdomGUID2) {
			kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID1).AtWarsUIDs.Remove(kingdomGUID2);
			kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID2).AtWarsUIDs.Remove(kingdomGUID1);
		}

		public void CreateCulture(string newCultureGUID, string newCultureNAME, string founderGUID, bool autoJoin) {
			Culture newCulture = new Culture();
			newCulture.CultureGUID = GuidUtility.RandomizeGUID(newCultureGUID);
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
			if (autoJoin && founderGUID != null && serverAPI.World.PlayerByUid(founderGUID) is IServerPlayer founder) {
				string oldCultureGUID = founder.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("culture_guid");
				newCulture.Predecessor = oldCultureGUID;
				if (oldCultureGUID != "00000000" && cultureList.Exists(cultureMatch => cultureMatch.CultureGUID == oldCultureGUID)) {
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
					newCulture.RockBlocks =	oldCulture.RockBlocks;
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
			}
			cultureList.Add(newCulture);
			SaveCulture();
		}

		public string ChangeCulture(string cultureGUID, string arguments, string changes) {
			Culture culture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == cultureGUID);
			string[] acceptedAppend = new string[] { "add", "addnew", "new" };
			string[] acceptedRemove = new string[] { "delete", "clear", "erase", "ban" };
			string[] acceptedRename = new string[] { "name", "change" };
			string[] acceptedGetall = new string[] { "getinfo", "info", "details" };
			string[] operation = arguments.Split('_');
			string cultureWord = LangUtility.Get("entries-keyword-culture");
			if (acceptedAppend.Contains(operation[0])) {
				operation[0] = "append";
			}
			if (acceptedRemove.Contains(operation[0])) {
				operation[0] = "remove";
			}
			if (acceptedRename.Contains(operation[0])) {
				operation[0] = "rename";
			}
			if (acceptedGetall.Contains(operation[0])) {
				operation[0] = "getall";
			}
			switch (operation[0]) {
				case "append":
					switch (operation[1]) {
						case "mascs": culture.MascFNames.Add(changes); break;
						case "femms": culture.FemmFNames.Add(changes); break;
						case "names": culture.CommLNames.Add(changes); break;
						case "skins": culture.SkinColors.Add(changes); break;
						case "pupil": culture.EyesColors.Add(changes); break;
						case "hairs": culture.HairColors.Add(changes); break;
						case "style": culture.HairStyles.Add(changes); break;
						case "extra": culture.HairExtras.Add(changes); break;
						case "beard": culture.FaceBeards.Add(changes); break;
						default: return LangUtility.Set("command-help-update-append", cultureWord);
					}
					break;
				case "remove":
					switch (operation[1]) {
						case "mascs": culture.MascFNames.Remove(changes); break;
						case "femms": culture.FemmFNames.Remove(changes); break;
						case "names": culture.CommLNames.Remove(changes); break;
						case "skins": culture.SkinColors.Remove(changes); break;
						case "pupil": culture.EyesColors.Remove(changes); break;
						case "hairs": culture.HairColors.Remove(changes); break;
						case "style": culture.HairStyles.Remove(changes); break;
						case "extra": culture.HairExtras.Remove(changes); break;
						case "beard": culture.FaceBeards.Remove(changes); break;
						default: return LangUtility.Set("command-help-update-remove", cultureWord);
					}
					break;
				case "rename":
					switch (operation[1]) {
						case "title": culture.CultureNAME = changes; break;
						case "longs": culture.CultureLONG = changes; break;
						case "short": culture.CultureDESC = changes; break;
						default: return LangUtility.Set("command-help-update-rename", cultureWord);
					}
					break;
				case "getall":
					switch (operation[1]) {
						case "basic": return CultUtility.ListedAllData(cultureGUID);
						case "mascs": return LangUtility.Msg(culture.MascFNames.ToArray());
						case "femms": return LangUtility.Msg(culture.FemmFNames.ToArray());
						case "names": return LangUtility.Msg(culture.CommLNames.ToArray());
						case "skins": return LangUtility.Msg(culture.SkinColors.ToArray());
						case "pupil": return LangUtility.Msg(culture.EyesColors.ToArray());
						case "hairs": return LangUtility.Msg(culture.HairColors.ToArray());
						case "style": return LangUtility.Msg(culture.HairStyles.ToArray());
						case "extra": return LangUtility.Msg(culture.HairExtras.ToArray());
						case "beard": return LangUtility.Msg(culture.FaceBeards.ToArray());
						default: return LangUtility.Set("command-help-update-getall", cultureWord);
					}
				default: return LangUtility.Set("command-failure-update", cultureWord) + "\n" + LangUtility.Set("command-help-update", cultureWord);
			}
			SaveCulture();
			return LangUtility.Set("command-success-update", LangUtility.Get("entries-keyword-culture"));
		}

		private void UpdateDicts() {
			playerDict.Clear();
			entityDict.Clear();
			foreach (Kingdom kingdom in kingdomList) {
				foreach (string playerUID in kingdom.PlayerUIDs) {
					playerDict.Add(playerUID, kingdom);
				}
				foreach (long entityUID in kingdom.EntityUIDs) {
					entityDict.Add(entityUID, kingdom);
				}
			}
			SaveAllData();
		}

		private void MakeAllData() {
			byte[] kingdomData = serverAPI.WorldManager.SaveGame.GetData("kingdomData");
			byte[] cultureData = serverAPI.WorldManager.SaveGame.GetData("cultureData");
			kingdomList = kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);
			cultureList = cultureData is null ? new List<Culture>() : SerializerUtil.Deserialize<List<Culture>>(cultureData);
			if (!kingdomList.Exists(kingdomMatch => kingdomMatch.KingdomGUID.Contains("00000000"))) {
				CreateKingdom("00000000", "Common", null, false);
			}
			if (!cultureList.Exists(cultureMatch => cultureMatch.CultureGUID.Contains("00000000"))) {
				CreateCulture("00000000", "Seraph", null, false);
			}
			UpdateDicts();
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
			UpdateDicts();
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
			if (kingdomGUID is null || kingdomGUID == "") {
				player.Entity.GetBehavior<EntityBehaviorLoyalties>()?.SetKingdom("00000000");
			}
			serverAPI.Logger.Error("Kingdom found: " + (DataUtility.IsKingdom(kingdomGUID) == false));
			serverAPI.Logger.Error(serverAPI.WorldManager.SaveGame.GetData("cultureData").ToString());
			serverAPI.Logger.Error(SerializerUtil.Deserialize<List<Culture>>(serverAPI.WorldManager.SaveGame.GetData("cultureData")).Count.ToString());
			serverAPI.Logger.Error(cultureList.Find(cultureMatch => cultureMatch.CultureGUID == "00000000").CultureNAME);
			if (cultureGUID is null || cultureGUID == "") {
				player.Entity.GetBehavior<EntityBehaviorLoyalties>()?.SetCulture("00000000");
			}
			UpdateDicts();
		}

		private void PlayerLeaveGame(IServerPlayer player) {
			try {
				if (player.Entity.WatchedAttributes.GetOrAddTreeAttribute("loyalties").HasAttribute("kingdom_guid")) {
					serverAPI.Logger.Notification(player.PlayerName + " was member of: " + DataUtility.GetKingdomNAME(player.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") + ", unloading data."));
					kingdomList.Find(kingdomMatch => kingdomMatch.PlayerUIDs.Contains(player.PlayerUID)).EntityUIDs.Remove(player.Entity.EntityId);
					SaveAllData();
				}
			} catch (NullReferenceException) {
				serverAPI.Logger.Error(player.PlayerName + " didn't have a kingdomGUID string.");
			}
			UpdateDicts();
		}

		private void SendRequestedDialog(IServerPlayer player, IServerPlayer sender, string kingdomUID, string message) {
			// Send a request notification to this player.
			if (!player.Entity.HasBehavior<EntityBehaviorLoyalties>()) {
				serverAPI.World.Logger.Error("Player does not have EntityBehaviorLoyalties so fix that.");
				return;
			}
			serverAPI.World.Logger.Notification("Sending dialog over to player.");
			player.Entity.GetBehavior<EntityBehaviorLoyalties>()?.GetRequestedDialog(sender, kingdomUID, message);
		}

		private TextCommandResult OnKingdomCommand(TextCommandCallingArgs args) {
			string commands = args[0] as string;
			string argument = args[1] as string;
			var caller = args.Caller.Player as IServerPlayer;
			string callerUID = caller.PlayerUID;

			Kingdom thisKingdom = caller.Entity.GetBehavior<EntityBehaviorLoyalties>()?.cachedKingdom ?? DataUtility.GetKingdom(caller.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid")) ?? null;
			Kingdom thatKingdom = null;
			IPlayer foundPlayer = serverAPI.World.PlayerByUid(serverAPI.PlayerData.GetPlayerDataByLastKnownName(argument)?.PlayerUID) ?? null;

			// Determine privillege role level and if they are allowed to make new kingdoms/cultures.
			bool arguments = argument != null && argument != "" && argument != " ";
			bool adminPass = args.Caller.HasPrivilege(Privilege.controlserver) || caller.PlayerName == "BadRabbit49";
			bool canCreate = args.Caller.GetRole(serverAPI).PrivilegeLevel >= serverAPI.World.Config.GetInt("MinCreateLevel", -1);
			bool maxCreate = serverAPI.World.Config.GetInt("MaxNewKingdoms", -1) != -1 || serverAPI.World.Config.GetInt("MaxNewKingdoms", -1) < (kingdomList.Count + 1);

			try { thatKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomNAME.ToLowerInvariant() == argument.ToLowerInvariant()); } catch (NullReferenceException) { }
			
			if (adminPass && thisKingdom.KingdomGUID != "00000000") {
				serverAPI.Logger.Notification(KingUtility.ListedAllData(thisKingdom.KingdomGUID));
			}

			switch (commands) {
				// Creates new owned Kingdom.
				case "create":
					if (!arguments) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create00", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (DataUtility.NameTaken(argument)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create01", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (DataUtility.InKingdom(callerUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create02", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (!canCreate) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create03", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (!maxCreate) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create04", LangUtility.Get("entries-keyword-kingdom")));
					}
					CreateKingdom(null, argument, caller.PlayerUID, (thisKingdom is null || thisKingdom.KingdomGUID == "00000000"));
					caller.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/cashregister"), caller.Entity);
					return TextCommandResult.Success(LangUtility.Set("command-success-create", LangUtility.Fix(argument)));
				// Deletes the owned Kingdom.
				case "delete":
					if (adminPass) {
						try {
							DeleteKingdom(thatKingdom);
							return TextCommandResult.Success(LangUtility.Set("command-success-delete", LangUtility.Fix(argument)));
						} catch {
							return TextCommandResult.Error(LangUtility.Set("command-error-delete00", "entries-keyword-kingdom"));
						}
					}
					if (!arguments) {
						return TextCommandResult.Error(LangUtility.Set("command-error-delete00", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (thisKingdom.LeadersGUID != caller.PlayerUID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-delete01", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (thisKingdom.LeadersGUID == caller.PlayerUID) {
						foreach (string member in DataUtility.GetOnlineGUIDs(thisKingdom.KingdomGUID)) {
							serverAPI.World.PlayerByUid(member)?.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/deepbell"), serverAPI.World.PlayerByUid(member)?.Entity);
						}
						DeleteKingdom(thatKingdom);
						return TextCommandResult.Success(LangUtility.Set("command-success-delete", LangUtility.Fix(argument)));
					}
					return TextCommandResult.Error(LangUtility.Set("command-error-unknown0", "entries-keyword-kingdom"));
				// Invites player to join Kingdom.
				case "invite":
					
					if (!serverAPI.World.AllOnlinePlayers.Contains(foundPlayer)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-invite04", LangUtility.Get("entries-keyword-kingdom")));
					} else {
						SendRequestedDialog(foundPlayer as IServerPlayer, caller, caller.Entity.GetBehavior<EntityBehaviorLoyalties>().kingdomGUID, "invite");
						return TextCommandResult.Success(LangUtility.Set("command-success-invite", argument));
					}
				// Removes player from Kingdom.
				case "remove":
					//*** TODO: MAKE THIS WORK ***//
					if (foundPlayer == null) {
						return TextCommandResult.Error(LangUtility.Set("command-error-remove00", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (thisKingdom.PlayerUIDs.Contains(foundPlayer.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-remove01", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (!adminPass && DataUtility.GetLeadersGUID(null, callerUID) != callerUID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-remove02", LangUtility.Get("entries-keyword-kingdom")));
					}
					return TextCommandResult.Success(LangUtility.Set("command-success-remove", argument));
				// Requests access to Leader.
				case "become":
					if (!DataUtility.KingdomExists(null, argument)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-become00", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (DataUtility.GetKingdom(null, argument).PlayerUIDs.Contains(caller.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-become01", LangUtility.Get("entries-keyword-kingdom")));
					}
					// Send request to Kingdom leader!
					SendRequestedDialog(caller, DataUtility.GetLeaders(argument, null), DataUtility.GetKingdomGUID(argument), "become");
					return TextCommandResult.Success(LangUtility.Set("command-success-become", argument));
				// Leaves current Kingdom.
				case "depart":
					if (!caller.Entity.HasBehavior<EntityBehaviorLoyalties>()) {
						return TextCommandResult.Error(LangUtility.Set("command-error-depart00", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (caller.Entity.GetBehavior<EntityBehaviorLoyalties>()?.kingdomGUID is null) {
						return TextCommandResult.Error(LangUtility.Set("command-error-depart01", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (serverAPI.World.Claims.All.Any(landClaim => landClaim.OwnedByPlayerUid == caller.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-depart02", LangUtility.Get("entries-keyword-kingdom")));
					}
					string kingdomName = thisKingdom.KingdomNAME;
					DepartKingdom(caller);
					return TextCommandResult.Success(LangUtility.Set("command-success-depart", LangUtility.Fix(kingdomName)));
				// Rename the Kingdom.
				case "rename":
					if (!arguments) {
						return TextCommandResult.Error("No argument. Provide a kingdom name.");
					}
					if (thisKingdom is null || thisKingdom.KingdomGUID == "00000000") {
						return TextCommandResult.Error("You are not part of a kingdom.");
					}
					if (DataUtility.GetLeadersGUID(null, callerUID) != callerUID) {
						return TextCommandResult.Error("You are not the leader of your kingdom.");
					}
					string oldName = LangUtility.Fix(thisKingdom.KingdomNAME);
					string newName = LangUtility.Low(argument);
					thisKingdom.KingdomNAME = newName;
					thisKingdom.KingdomLONG = newName;
					return TextCommandResult.Success("Successfully renamed " + oldName + " to " + LangUtility.Fix(newName));
				// Declares war on Kingdom.
				case "attack":
					if (!DataUtility.IsKingdom(null, argument)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-attack00", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (thisKingdom is null || thisKingdom.KingdomGUID == "00000000") {
						return TextCommandResult.Error(LangUtility.Set("command-error-attack01", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (DataUtility.GetLeadersGUID(thisKingdom.KingdomGUID, callerUID) != callerUID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-attack02", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (thisKingdom.AtWarsUIDs.Contains(thatKingdom.KingdomGUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-attack03", LangUtility.Get("entries-keyword-kingdom")));
					}
					SetWarKingdom(thisKingdom.KingdomGUID, thatKingdom.KingdomGUID);
					return TextCommandResult.Success(LangUtility.Set("command-success-attack", LangUtility.Fix(argument)));
				// Sets entity to Kingdom.
				case "endwar":
					if (!DataUtility.IsKingdom(null, argument)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-endwar00", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (thisKingdom is null || thisKingdom.KingdomGUID == "00000000") {
						return TextCommandResult.Error(LangUtility.Set("command-error-endwar01", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (DataUtility.GetLeadersGUID(thisKingdom.KingdomGUID, callerUID) != callerUID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-endwar02", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (!thisKingdom.AtWarsUIDs.Contains(thatKingdom.KingdomGUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-endwar03", LangUtility.Get("entries-keyword-kingdom")));
					}
					EndWarKingdom(thisKingdom.KingdomGUID, thatKingdom.KingdomGUID);
					return TextCommandResult.Success(LangUtility.Set("command-success-endwar", LangUtility.Fix(argument)));
				// Sets entity to Kingdom.
				case "setent":
					if (caller.CurrentEntitySelection is null) {
						return TextCommandResult.Error("Nothing selected, please look at an entity.");
					}
					if (!caller.CurrentEntitySelection.Entity.HasBehavior<EntityBehaviorLoyalties>()) {
						return TextCommandResult.Error("Entity does not have Loyalties behavior.");
					}
					if (argument is null) {
						return TextCommandResult.Error("No argument. Provide a kingdom name.");
					}
					SetEntKingdom(caller.CurrentEntitySelection.Entity, DataUtility.GetKingdomGUID(argument));
					return TextCommandResult.Success("Entity's kingdom set to: " + argument + "!");
				// Fetch all stored Kingdom data.
				case "getall":
					if (thatKingdom != null && thatKingdom.KingdomGUID != "00000000") {
						return TextCommandResult.Success(KingUtility.ListedAllData(thatKingdom.KingdomGUID));
					}
					if (thisKingdom != null && thisKingdom.KingdomGUID != "00000000") {
						return TextCommandResult.Success(KingUtility.ListedAllData(thisKingdom.KingdomGUID));
					}
					return TextCommandResult.Success(LangUtility.Set("command-error-getall00", LangUtility.Get("entries-keyword-kingdom")));
			}
			return TextCommandResult.Error(LangUtility.Get("command-help-kingdom"));
		}

		private TextCommandResult OnCultureCommand(TextCommandCallingArgs args) {
			string commands = args[0] as string;
			string argument = args[1] as string;
			string strinput = args[2] as string;
			var caller = args.Caller.Player as IServerPlayer;
			string callerUID = caller.PlayerUID;
			Culture thisCulture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == caller.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("culture_guid")) ?? null;

			serverAPI.Logger.Notification(thisCulture.CultureNAME);
			// Determine privillege role level and if they are allowed to make new kingdoms/cultures.
			bool arguments = argument != null && argument != "" && argument != " ";
			bool adminPass = args.Caller.HasPrivilege(Privilege.controlserver) || caller.PlayerName == "BadRabbit49";
			bool canCreate = args.Caller.GetRole(serverAPI).PrivilegeLevel >= serverAPI.World.Config.GetInt("MinCreateLevel", -1);
			bool maxCreate = serverAPI.World.Config.GetInt("MaxNewCultures", -1) != -1 && serverAPI.World.Config.GetInt("MaxNewCultures", -1) < (kingdomList.Count + 1);
			bool hoursTime = (serverAPI.World.Calendar.TotalHours - thisCulture.FoundedHOUR) > (serverAPI.World.Calendar.TotalHours - serverAPI.World.Config.GetInt("MinCultureMake"));
			switch (commands) {
				// Creates brand new Culture.
				case "create":
					if (!arguments) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create00", LangUtility.Get("entries-keyword-culture")));
					}
					if (cultureList.Exists(cultureMatch => cultureMatch.CultureNAME.ToLowerInvariant() == argument.ToLowerInvariant())) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create01", LangUtility.Get("entries-keyword-culture")));
					}
					if (!canCreate && !adminPass) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create03", LangUtility.Get("entries-keyword-culture")));
					}
					if (!maxCreate && !adminPass) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create04", LangUtility.Get("entries-keyword-culture")));
					}
					if (!hoursTime && !adminPass) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create05", LangUtility.Get("entries-keyword-culture")));
					}
					CreateCulture(null, argument, callerUID, (thisCulture == null || thisCulture.CultureGUID == "00000000"));
					caller.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/writing"), caller.Entity);
					return TextCommandResult.Success(LangUtility.Set("command-success-create", LangUtility.Fix(argument)));
				// Edits existing culture.
				case "update":
					if (thisCulture == null) {
						return TextCommandResult.Error(LangUtility.Set("command-error-update00", LangUtility.Get("entries-keyword-culture")));
					}
					if (!adminPass && thisCulture.FounderGUID != callerUID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-update01", LangUtility.Get("entries-keyword-culture")));
					}
					/**NOT WORKING HERE. FOR SOME REASON IT NEEDS WORK IDK**/
					/**if (!(strinput.StartsWith('"') && strinput.EndsWith('"'))) {
						return TextCommandResult.Error(LangUtility.Set("command-error-update02", LangUtility.Get("entries-keyword-culture")));
					}**/
					caller.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/writing"), caller.Entity);
					
					serverAPI.Logger.Notification("\nargument: " + argument + "\nstrinput: " + (strinput?.Replace("\"", "") ?? "" ));
					string results = ChangeCulture(thisCulture.CultureGUID, argument, (strinput?.Replace("\"", "") ?? ""));
					serverAPI.Logger.Notification(results);
					return TextCommandResult.Success(results);
				}
			return TextCommandResult.Error(LangUtility.Get("command-help-culture"));
		}
	}
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class WeaponAnims {
		public AssetLocation itemCode;
		public string idleAnim;
		public float idleTime;
		public string walkAnim;
		public float walkTime;
		public string moveAnim;
		public float moveTime;
		public string drawAnim;
		public float drawTime;
		public string fireAnim;
		public float fireTime;
		public string loadAnim;
		public float loadTime;
		public string bashAnim;
		public float bashTime;
		public string stabAnim;
		public float stabTime;
		public string hit1Anim;
		public float hit1Time;
		public string hit2Anim;
		public float hit2Time;
	}
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class ClassPrivileges {
		public bool ownLand;
		public bool ownArms;
		public bool canVote;
		public bool canLock;
		public bool canKill;
	}
}