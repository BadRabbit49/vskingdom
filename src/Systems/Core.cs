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
		private Dictionary<string, string> activeWars = new Dictionary<string, string>();

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
			if (api.ModLoader.IsModEnabled("fsmlib")) {
				api.Logger.Notification("FSMLib is loaded. Enabling content with fsmlib.");
				fsmEnabled = true;
			} else {
				api.Logger.Notification("FSMLib is not loaded. Disabling content with fsmlib.");
				fsmEnabled = false;
			}
			// Create chat commands for creation, deletion, invitation, and so of kingdoms.
			api.ChatCommands.Create("kingdom")
				.WithDescription(LangUtility.Get("command-kingdom-desc"))
				.WithAdditionalInformation(LangUtility.Get("command-kingdom-help"))
				.RequiresPrivilege(Privilege.chat)
				.RequiresPlayer()
				.WithArgs(api.ChatCommands.Parsers.Word("commands", new string[] { "create", "delete", "invite", "become", "depart", "rename", "setent", "attack", "getall", "setgov" }), api.ChatCommands.Parsers.OptionalWord("argument"))
				.HandleWith(new OnCommandDelegate(OnKingdomCommand));
			// Create chat commands for creation, deletion, invitation, and so of cultures.
			api.ChatCommands.Create("culture")
				.WithDescription(LangUtility.Get("command-culture-desc"))
				.WithAdditionalInformation(LangUtility.Get("command-culture-help"))
				.RequiresPrivilege(Privilege.chat)
				.RequiresPlayer()
				.WithArgs(api.ChatCommands.Parsers.Word("commands", new string[] { "create", "delete", "change", "rename", "blocks" }), api.ChatCommands.Parsers.OptionalWord("argument"))
				.HandleWith(new OnCommandDelegate(OnCultureCommand));
		}
		
		public override void StartClientSide(ICoreClientAPI capi) {
			base.StartClientSide(capi);
			clientAPI = capi;
			capi.Event.LevelFinalize += () => LevelFinalize(capi);
			capi.Network.RegisterChannel("kingdomnetwork").RegisterMessageType<KingdomCommand>().SetMessageHandler<KingdomCommand>(OnKingdomCommandClient);
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
			sapi.Network.RegisterChannel("kingdomnetwork").RegisterMessageType<KingdomCommand>().SetMessageHandler<KingdomCommand>(OnKingdomCommandServer);
		}

		public override void Dispose() {
			// Unload and Unpatch everything from the mod.
			harmony?.UnpatchAll(Mod.Info.ModID);
			base.Dispose();
		}

		private void OnKingdomCommandClient(KingdomCommand kingdomCommand) {
			// Send messages here.
		}

		private void OnKingdomCommandServer(IServerPlayer fromPlayer, KingdomCommand kingdomCommand) {
			switch (kingdomCommand.commands) {
				case "set_kingdom":
					kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomCommand.newGUIDs).EntityUIDs.Add(kingdomCommand.entityID);
					kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomCommand.oldGUIDs).EntityUIDs.Remove(kingdomCommand.entityID);
					if (serverAPI.World.GetEntityById(kingdomCommand.entityID) is EntityPlayer playerEnt) {
						kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomCommand.newGUIDs).PlayerUIDs.Add(playerEnt.PlayerUID);
						kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomCommand.oldGUIDs).PlayerUIDs.Remove(playerEnt.PlayerUID);
					}
					UpdateDicts();
					return;
				case "add_enemies":
					if (serverAPI.World.GetEntityById(kingdomCommand.entityID) is EntityPlayer) {
						kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomCommand.oldGUIDs).EnemieUIDs.Add((serverAPI.World.GetEntityById(kingdomCommand.entityID) as EntityPlayer).PlayerUID);
						SaveKingdom();
					}
					return;
				case "del_enemies":
					if (serverAPI.World.GetEntityById(kingdomCommand.entityID) is EntityPlayer) {
						kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomCommand.oldGUIDs).EnemieUIDs.Remove((serverAPI.World.GetEntityById(kingdomCommand.entityID) as EntityPlayer).PlayerUID);
						SaveKingdom();
					}
					return;
			}
		}

		public void CreateKingdom(string newKingdomGUID, string newKingdomNAME, string founderGUID, IServerPlayer founder, bool autoJoin) {
			Kingdom newKingdom = new Kingdom();
			newKingdom.KingdomGUID = KingUtility.CorrectKingdomGUID(newKingdomGUID);
			newKingdom.KingdomNAME = KingUtility.CorrectKingdomName(newKingdomNAME);
			newKingdom.KingdomLONG = KingUtility.CorrectKingdomName(newKingdomNAME);
			newKingdom.KingdomTYPE = KingUtility.DefaultKingdomType(newKingdomNAME);
			newKingdom.KingdomDESC = KingUtility.DefaultKingdomType(newKingdom.KingdomTYPE);
			newKingdom.LeadersGUID = null;
			newKingdom.LeadersNAME = KingUtility.DefaultLeadersName(newKingdom.KingdomTYPE);
			newKingdom.LeadersLONG = KingUtility.DefaultLeadersLong(newKingdom.KingdomTYPE);
			newKingdom.LeadersDESC = KingUtility.DefaultLeadersDesc(newKingdom.KingdomTYPE, LangUtility.Fix(newKingdomNAME));
			newKingdom.FoundedMETA = DateTime.Now.ToLongDateString();
			newKingdom.FoundedDATE = serverAPI.World.Calendar.PrettyDate();
			newKingdom.FoundedHOUR = serverAPI.World.Calendar.TotalHours;
			if (autoJoin) {
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
			UpdateDicts();
		}
		
		public void SetEntKingdom(Entity thisEnt, string kingdomUID) {
			thisEnt.GetBehavior<EntityBehaviorLoyalties>().SetKingdom(kingdomUID);
			UpdateDicts();
		}

		public void DeclareWarsOn(string kingdomUID1, string kingdomUID2) {
			kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomUID1).AtWarsUIDs.Add(kingdomUID2);
			kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomUID2).AtWarsUIDs.Add(kingdomUID1);
			activeWars.Add(kingdomUID1, kingdomUID2);
			UpdateDicts();
			serverAPI.Logger.Notification("Kingdom 1 is at war with: " + kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomUID2).AtWarsUIDs.First());
			serverAPI.Logger.Notification("Kingdom 2 is at war with: " + kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomUID1).AtWarsUIDs.First());
		}

		public void CreateCulture(string newCultureGUID, string newCultureNAME, string founderGUID, IServerPlayer founder, bool autoJoin) {
			Culture newCulture = new Culture();
			newCulture.CultureGUID = CultUtility.CorrectCultureGUID(newCultureGUID);
			newCulture.CultureNAME = CultUtility.CorrectCultureName(newCultureNAME);
			newCulture.CultureLONG = CultUtility.CorrectCultureLong(newCultureNAME);
			newCulture.FoundedDATE = serverAPI.World.Calendar.PrettyDate();
			newCulture.FoundedMETA = DateTime.Now.ToLongDateString();
			if (founder != null && DataUtility.CultureExists(founder.Entity.GetBehavior<EntityBehaviorLoyalties>()?.cultureGUID)) {
				Culture oldCulture = DataUtility.GetCulture(founder.Entity.GetBehavior<EntityBehaviorLoyalties>()?.cultureGUID);
				newCulture.Predecessor = oldCulture.CultureGUID;
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
			var AvailableSkinParts = founder.Entity.Properties.Attributes["skinnableParts"]?.AsObject<SkinnablePart[]>();
			foreach (var skinpart in AvailableSkinParts) {
				if (skinpart.Type == EnumSkinnableType.Texture) {
					switch (skinpart.Code) {
						case "baseskin": newCulture.SkinColors.AddToArray(skinpart.TextureTarget); break;
						case "eyecolor": newCulture.EyesColors.AddToArray(skinpart.TextureTarget); break;
						case "haircolor": newCulture.HairColors.AddToArray(skinpart.TextureTarget); break;
					}
				}
				if (skinpart.Type == EnumSkinnableType.Shape) {
					switch (skinpart.Code) {
						case "hairbase": newCulture.HairStyles.AddToArray(skinpart.ShapeTemplate.Base.ToString()); break;
						case "hairextra": newCulture.HairExtras.AddToArray(skinpart.ShapeTemplate.Base.ToString()); break;
						case "mustache": newCulture.FaceStyles.AddToArray(skinpart.ShapeTemplate.Base.ToString()); break;
						case "beard": newCulture.FaceBeards.AddToArray(skinpart.ShapeTemplate.Base.ToString()); break;
					}
				}
			}
			if (autoJoin) {
				founder.Entity.WatchedAttributes.GetTreeAttribute("loyalties").SetString("culture_guid", newCulture.CultureGUID);
			}
			cultureList.Add(newCulture);
			SaveCulture();
		}

		public void ChangeCulture(string cultureGUID, string cultureNAME = null, string cultureLONG = null, string cultureDESC = null, string[] mascNames = null, string[] femmNames = null, string[] lastNames = null) {
			Culture culture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == cultureGUID);
			if (cultureNAME != null) {
				culture.CultureNAME = cultureNAME;
			}
			if (cultureLONG != null) {
				culture.CultureLONG = cultureLONG;
			}
			if (cultureDESC != null) {
				culture.CultureDESC = cultureDESC;
			}
			if (mascNames != null) {
				culture.MascFNames = mascNames;
			}
			if (femmNames != null) {
				culture.FemmFNames = femmNames;
			}
			if (lastNames != null) {
				culture.CommLNames = lastNames;
			}
			SaveCulture();
		}

		private void UpdateDicts() {
			playerDict.Clear();
			entityDict.Clear();
			activeWars.Clear();
			foreach (Kingdom kingdom in kingdomList) {
				foreach (string playerUID in kingdom.PlayerUIDs) {
					playerDict.Add(playerUID, kingdom);
				}
				foreach (long entityUID in kingdom.EntityUIDs) {
					entityDict.Add(entityUID, kingdom);
				}
				foreach (string atwarsUID in kingdom.AtWarsUIDs) {
					if ((activeWars.ContainsKey(atwarsUID) && activeWars.ContainsValue(kingdom.KingdomGUID)) ||
						(activeWars.ContainsKey(kingdom.KingdomGUID) && activeWars.ContainsValue(atwarsUID))) {
						continue;
					} else {
						activeWars.Add(atwarsUID, kingdom.KingdomGUID);
					}
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
				CreateKingdom("00000000", "Common", null, null, false);
			}
			if (!cultureList.Exists(cultureMatch => cultureMatch.CultureGUID.Contains("00000000"))) {
				CreateCulture("00000000", "Seraph", null, null, false);
				string[] _mascNames = LangUtility.Open(serverAPI.World.Config.GetString("BasicMascNames"));
				string[] _femmNames = LangUtility.Open(serverAPI.World.Config.GetString("BasicFemmNames"));
				string[] _lastNames = LangUtility.Open(serverAPI.World.Config.GetString("BasicLastNames"));
				ChangeCulture("00000000", null, null, null, _mascNames, _femmNames, _lastNames);
			}
			SaveAllData();
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
			if (kingdomGUID is null || kingdomGUID == "" || DataUtility.IsKingdom(kingdomGUID, null) == false) {
				player.Entity.GetBehavior<EntityBehaviorLoyalties>()?.SetKingdom("00000000");
			} else {
				serverAPI.Logger.Notification(player.PlayerName + " is member of: " + DataUtility.GetKingdomNAME(player.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") + ", reloading data."));
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
				serverAPI.Logger.Error(player.PlayerName + " didn't have a kingdomUID string.");
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
		
		private string GetKingdomData() {
			string fullData = "\n--------------FULL EMPIRE DATA--------------";
			foreach (Kingdom kingdom in kingdomList) {
				try {
					fullData += "\nKingdom: " + LangUtility.Fix(kingdom.KingdomNAME) + "\nUID: " + kingdom.KingdomGUID;
					fullData += "\nLeaders: " + kingdom.LeadersNAME + "\nUID: " + kingdom.LeadersGUID;
					fullData += "\nFounded: " + kingdom.FoundedMETA + "\nGameDay: " + kingdom.FoundedDATE;
					long[] entList = kingdom.EntityUIDs.ToArray<long>();
					string[] warList = kingdom.AtWarsUIDs.ToArray<string>();
					for (int ent = 0; ent < entList.Length; ent++) {
						if (ent == 0) {
							fullData += "\nMembers: [";
						} else if (ent != entList.Length) {
							fullData += ", ";
						}
						fullData += entList[ent].ToString();
						if (ent == entList.Length) {
							fullData += "]";
						}
					}
					for (int war = 0; war < warList.Length; war++) {
						if (war == 0) {
							fullData += "\nSetWars: [";
						} else if (war != warList.Length) {
							fullData += ", ";
						}
						fullData += warList[war].ToString();
						if (war == warList.Length) {
							fullData += "]";
						}
					}
					fullData += "\n";
				} catch { }
			}
			fullData += "\n--------------------------------------------";
			return fullData;
		}

		private TextCommandResult OnKingdomCommand(TextCommandCallingArgs args) {
			string commands = args[0] as string;
			string argument = args[1] as string;
			var caller = args.Caller.Player as IServerPlayer;
			string callerUID = caller.PlayerUID;

			// Determine privillege role level and if they are allowed to make new kingdoms/cultures.
			bool arguments = argument != null && argument != "" && argument != " ";
			bool adminPass = args.Caller.HasPrivilege("controlserver") || caller.PlayerName == "BadRabbit49";
			bool canCreate = args.Caller.GetRole(serverAPI).PrivilegeLevel >= serverAPI.World.Config.GetInt("MinCreateLevel", -1);
			bool maxCreate = serverAPI.World.Config.GetInt("MaxNewKingdoms", -1) != -1 || serverAPI.World.Config.GetInt("MaxNewKingdoms", -1) < (kingdomList.Count + 1);

			Kingdom thisKingdom = null;
			Kingdom thatKingdom = null;
			
			try { thisKingdom = caller.Entity.GetBehavior<EntityBehaviorLoyalties>().cachedKingdom; } catch (NullReferenceException) { }
			try { thatKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomNAME.ToLowerInvariant() == argument.ToLowerInvariant()); } catch (NullReferenceException) { }
			
			if (adminPass) {
				serverAPI.Logger.Notification(GetKingdomData());
			}

			switch (commands) {
				// Creates new owned Kingdom.
				case "create":
					if (!arguments) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create00", "entries-keyword-kingdom"));
					}
					if (DataUtility.NameTaken(argument, null)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create01", "entries-keyword-kingdom"));
					}
					if (DataUtility.InKingdom(callerUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create02", "entries-keyword-kingdom"));
					}
					if (!canCreate) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create03", "entries-keyword-kingdom"));
					}
					if (!maxCreate) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create04", "entries-keyword-kingdom"));
					}
					CreateKingdom(null, argument, caller.PlayerUID, caller, (thisKingdom is null || thisKingdom.KingdomGUID == "00000000"));
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
						return TextCommandResult.Error(LangUtility.Set("command-error-delete00", "entries-keyword-kingdom"));
					}
					if (thisKingdom.LeadersGUID != caller.PlayerUID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-delete01", "entries-keyword-kingdom"));
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
					IServerPlayer foundPlayer = serverAPI.World.PlayerByUid(serverAPI.PlayerData.GetPlayerDataByLastKnownName(argument).PlayerUID) as IServerPlayer;
					if (!serverAPI.World.AllOnlinePlayers.Contains(foundPlayer)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-invite04", "entries-keyword-kingdom"));
					} else {
						SendRequestedDialog(foundPlayer, caller, caller.Entity.GetBehavior<EntityBehaviorLoyalties>().kingdomGUID, "invite");
						return TextCommandResult.Success(LangUtility.Set("command-success-invite", argument));
					}
				// Removes play from Kingdom.
				case "remove":
					//*** TODO: MAKE THIS WORK ***//
					return TextCommandResult.Success(LangUtility.Set("command-success-remove", argument));
				// Requests access to Leader.
				case "become":
					if (!DataUtility.KingdomExists(null, argument)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-become00", "entries-keyword-kingdom"));
					}
					if (DataUtility.GetKingdom(null, argument).PlayerUIDs.Contains(caller.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-become01", "entries-keyword-kingdom"));
					}
					// Send request to Kingdom leader!
					SendRequestedDialog(caller, DataUtility.GetLeaders(argument, null), DataUtility.GetKingdomGUID(argument), "become");
					return TextCommandResult.Success(LangUtility.Set("command-success-become", argument));
				// Leaves current Kingdom.
				case "depart":
					if (!caller.Entity.HasBehavior<EntityBehaviorLoyalties>()) {
						return TextCommandResult.Error(LangUtility.Set("command-error-depart00", "entries-keyword-kingdom"));
					}
					if (caller.Entity.GetBehavior<EntityBehaviorLoyalties>()?.kingdomGUID is null) {
						return TextCommandResult.Error(LangUtility.Set("command-error-depart01", "entries-keyword-kingdom"));
					}
					if (serverAPI.World.Claims.All.Any(landClaim => landClaim.OwnedByPlayerUid == caller.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-depart02", "entries-keyword-kingdom"));
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
						return TextCommandResult.Error(LangUtility.Get("command-error-attack00"));
					}
					if (thisKingdom is null || thisKingdom.KingdomGUID == "00000000") {
						return TextCommandResult.Error(LangUtility.Get("command-error-attack01"));
					}
					if (DataUtility.GetLeadersGUID(thisKingdom.KingdomGUID, callerUID) != callerUID) {
						return TextCommandResult.Error(LangUtility.Get("command-error-attack02"));
					}
					DeclareWarsOn(thisKingdom.KingdomGUID, thatKingdom.KingdomGUID);
					return TextCommandResult.Success("Successfully declared war on " + argument + "! Your kingdoms are now enemies.");
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
					return TextCommandResult.Success(GetKingdomData());
			}
			return TextCommandResult.Error(LangUtility.Get("command-kingdom-help-basics"));
		}

		private TextCommandResult OnCultureCommand(TextCommandCallingArgs args) {
			string commands = args[0] as string;
			string argument = args[1] as string;
			var caller = args.Caller.Player as IServerPlayer;
			string callerUID = caller.PlayerUID;
			Culture thisCulture = null;
			try { thisCulture = caller.Entity.GetBehavior<EntityBehaviorLoyalties>().cachedCulture; } catch (NullReferenceException) { }
			// Determine privillege role level and if they are allowed to make new kingdoms/cultures.
			bool arguments = argument != null && argument != "" && argument != " ";
			bool adminPass = args.Caller.HasPrivilege("controlserver") || caller.PlayerName == "BadRabbit49";
			bool canCreate = args.Caller.GetRole(serverAPI).PrivilegeLevel >= serverAPI.World.Config.GetInt("MinCreateLevel", -1);
			bool maxCreate = serverAPI.World.Config.GetInt("MaxNewCultures", -1) != -1 && serverAPI.World.Config.GetInt("MaxNewCultures", -1) < (kingdomList.Count + 1);
			bool hoursTime = (serverAPI.World.Calendar.TotalHours - thisCulture.FoundedHOUR) > (serverAPI.World.Calendar.TotalHours - serverAPI.World.Config.GetInt("MinCultureMake"));
			switch (commands) {
				// Creates brand new Culture.
				case "create":
					if (!arguments) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create00", "entries-keyword-culture"));
					}
					if (DataUtility.NameTaken(null, argument)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create01", "entries-keyword-culture"));
					}
					if (!canCreate) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create03", "entries-keyword-culture"));
					}
					if (!maxCreate) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create04", "entries-keyword-culture"));
					}
					if (!hoursTime) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create05", "entries-keyword-culture"));
					}
					CreateCulture(null, argument, caller.PlayerUID, caller, (thisCulture is null || thisCulture.CultureGUID == "00000000"));
					caller.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/writing"), caller.Entity);
					return TextCommandResult.Success(LangUtility.Set("command-success-create", LangUtility.Fix(argument)));
			}

			return TextCommandResult.Error(LangUtility.Get("command-culture-help-basics"));
		}
	}
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class KingdomCommand {
		public long entityID;
		public string commands;
		public string oldGUIDs;
		public string newGUIDs;
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
	public class WeaponStats {
		public AssetLocation itemCode;
		public float rangeDmg;
		public float meleeDmg;
		public float velocity;
		public float minRange;
		public float maxRange;
		public bool canMelee;
		public bool canThrow;
		public bool skirmish;
		public bool useSmoke;
	}
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class ArrowsStats {
		public AssetLocation itemCode;
		public float basicDmg;
		public float piercing;
		public float knocking;
	}
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class WeaponAmmos {
		public AssetLocation itemCode;
		public List<AssetLocation> ammoCode;
	}
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
	public class ClassProperties {
		public Priviliges roleType;
		public string roleName;
		public float priority;
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