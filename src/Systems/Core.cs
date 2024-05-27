using System.Collections.Generic;
using System.Linq;
using System;
using ProtoBuf;
using HarmonyLib;
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
			// Block Behaviors
			api.RegisterBlockEntityBehaviorClass("Resupply", typeof(BlockBehaviorResupply));
			// Block Entities
			api.RegisterBlockEntityClass("BlockEntityBody", typeof(BlockEntityBody));
			api.RegisterBlockEntityClass("BlockEntityPost", typeof(BlockEntityPost));
			// Items
			api.RegisterItemClass("ItemBanner", typeof(ItemBanner));
			api.RegisterItemClass("ItemPeople", typeof(ItemPeople));
			// Entities
			api.RegisterEntity("EntityArcher", typeof(EntityArcher));
			api.RegisterEntity("EntityKnight", typeof(EntityKnight));
			// Entity Behaviors
			api.RegisterEntityBehaviorClass("KingdomFullNames", typeof(EntityBehaviorFullNames));
			api.RegisterEntityBehaviorClass("KingdomLoyalties", typeof(EntityBehaviorLoyalties));
			api.RegisterEntityBehaviorClass("SoldierDecayBody", typeof(EntityBehaviorDecayBody));
			api.RegisterEntityBehaviorClass("SoldierTraverser", typeof(EntityBehaviorTraverser));
			// AITasks
			AiTaskRegistry.Register<AiTaskFollowEntityLeader>("FollowEntityLeader");
			AiTaskRegistry.Register<AiTaskSoldierReturningTo>("SoldierReturningTo");
			AiTaskRegistry.Register<AiTaskSoldierRespawnPost>("SoldierRespawnPost");
			AiTaskRegistry.Register<AiTaskSoldierFleesEntity>("SoldierFleesEntity");
			AiTaskRegistry.Register<AiTaskSoldierGuardingPos>("SoldierGuardingPos");
			AiTaskRegistry.Register<AiTaskSoldierHealingSelf>("SoldierHealingSelf");
			AiTaskRegistry.Register<AiTaskSoldierMeleeAttack>("SoldierMeleeAttack");
			AiTaskRegistry.Register<AiTaskSoldierRangeAttack>("SoldierRangeAttack");
			AiTaskRegistry.Register<AiTaskSoldierSeeksEntity>("SoldierSeeksEntity");
			AiTaskRegistry.Register<AiTaskSoldierTargetables>("SoldierTargetables");
			AiTaskRegistry.Register<AiTaskSoldierTravelToPos>("SoldierTravelToPos");
			AiTaskRegistry.Register<AiTaskSoldierWanderAbout>("SoldierWanderAbout");
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
				.WithArgs(api.ChatCommands.Parsers.Word("commands", new string[] { "create", "delete", "invite", "become", "depart", "rename", "setent", "setwar", "getall", "setgov" }), api.ChatCommands.Parsers.OptionalWord("argument"))
				.HandleWith(new OnCommandDelegate(OnKingdomCommand));
			// Create chat commands for creation, deletion, invitation, and so of cultures.
			api.ChatCommands.Create("culture")
				.WithDescription(LangUtility.Get("command-culture-desc"))
				.WithAdditionalInformation(LangUtility.Get("command-culture-help"))
				.RequiresPrivilege(Privilege.chat)
				.RequiresPlayer()
				.WithArgs(api.ChatCommands.Parsers.Word("commands", new string[] { "create", "delete", "change", "rename", "blocks" }), api.ChatCommands.Parsers.OptionalWord("argument"))
				.HandleWith(new OnCommandDelegate(OnCultureCommand));
			// Create chat commands for editing properties of soldier entities.
			api.ChatCommands.Create("entedit")
				.WithDescription("Commands meant to edit entity properties.")
				.WithAdditionalInformation("Look at an entity and try: namehim, nameher, kingdom, entguid, leaders")
				.RequiresPrivilege(Privilege.chat)
				.RequiresPlayer()
				.WithArgs(api.ChatCommands.Parsers.Word("commands", new string[] { "namehim", "nameher", "kingdom", "entguid", "leaders" }), api.ChatCommands.Parsers.OptionalWord("argument"))
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

		public void CreateKingdom(string kingdomName, string kingdomGuid, string founderUID, IServerPlayer founder, bool autoJoin) {
			Kingdom newKingdom = new Kingdom();
			newKingdom.KingdomGUID = KingUtility.CorrectKingdomGUID(kingdomGuid);
			newKingdom.KingdomName = KingUtility.CorrectKingdomName(kingdomName);
			newKingdom.KingdomLong = KingUtility.CorrectKingdomName(kingdomName);
			newKingdom.KingdomType = KingUtility.DefaultKingdomType(kingdomName);
			newKingdom.KingdomDesc = KingUtility.DefaultKingdomType(newKingdom.KingdomType);
			newKingdom.LeadersGUID = null;
			newKingdom.LeadersName = KingUtility.DefaultLeadersName(newKingdom.KingdomType);
			newKingdom.LeadersLong = KingUtility.DefaultLeadersLong(newKingdom.KingdomType);
			newKingdom.LeadersDesc = KingUtility.DefaultLeadersDesc(newKingdom.KingdomType, LangUtility.Fix(kingdomName));
			newKingdom.FoundingIGT = serverAPI.World.Calendar.PrettyDate();
			newKingdom.FoundingIRL = DateTime.Now.ToLongDateString();
			if (autoJoin) {
				founder.Entity.GetBehavior<EntityBehaviorLoyalties>().kingdomUID = newKingdom.KingdomGUID;
				newKingdom.LeadersGUID = founderUID;
				newKingdom.PlayerUIDs.Add(founderUID);
				newKingdom.EntityUIDs.Add(founder.Entity.EntityId);
			}
			kingdomList.Add(newKingdom);
			SaveKingdom();
		}

		public void DeleteKingdom(Kingdom kingdom) {
			kingdomList.Remove(kingdom);
			UpdateDicts();
		}

		public void DepartKingdom(IServerPlayer caller) {
			kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == caller.Entity?.GetBehavior<EntityBehaviorLoyalties>()?.kingdomUID).PlayerUIDs.Remove(caller.PlayerUID);
			kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == caller.Entity?.GetBehavior<EntityBehaviorLoyalties>()?.kingdomUID).EntityUIDs.Remove(caller.Entity.EntityId);
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

		public void CreateCulture(string cultureName, string cultureGuid, IServerPlayer founder, bool autoJoin) {
			Culture newCulture = new Culture();
			newCulture.CultureGUID = CultUtility.CorrectCultureGUID(cultureGuid);
			newCulture.CultureName = CultUtility.CorrectCultureName(cultureName);
			newCulture.CultureLong = CultUtility.CorrectCultureLong(cultureName);
			newCulture.FoundingIGT = serverAPI.World.Calendar.PrettyDate();
			newCulture.FoundingIRL = DateTime.Now.ToLongDateString();
			cultureList.Add(newCulture);
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
				CreateKingdom("Commoners", "00000000", "", null, false);
			}
			if (!cultureList.Exists(cultureMatch => cultureMatch.CultureGUID.Contains("00000000"))) {
				CreateCulture("Seraph", "00000000", null, false);
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
			capi.Gui.Icons.CustomIcons["bascinet"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/bascinet.svg"));
			capi.Gui.Icons.CustomIcons["beltloop"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/beltloop.svg"));
			capi.Gui.Icons.CustomIcons["breeches"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/breeches.svg"));
			capi.Gui.Icons.CustomIcons["chapeaus"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/chapeaus.svg"));
			capi.Gui.Icons.CustomIcons["clothing"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/clothing.svg"));
			capi.Gui.Icons.CustomIcons["cuirasse"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/cuirasse.svg"));
			capi.Gui.Icons.CustomIcons["footwear"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/footwear.svg"));
			capi.Gui.Icons.CustomIcons["gauntlet"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/gauntlet.svg"));
			capi.Gui.Icons.CustomIcons["leggings"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/leggings.svg"));
			capi.Gui.Icons.CustomIcons["mozzetta"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/mozzetta.svg"));
			capi.Gui.Icons.CustomIcons["munition"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/munition.svg"));
			capi.Gui.Icons.CustomIcons["overcoat"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/overcoat.svg"));
			capi.Gui.Icons.CustomIcons["shieldry"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/shieldry.svg"));
			capi.Gui.Icons.CustomIcons["weaponry"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/weaponry.svg"));
			capi.Gui.Icons.CustomIcons["longbows"] = capi.Gui.Icons.SvgIconSource(new AssetLocation("vskingdom:textures/icons/character/longbows.svg"));
		}

		private void PlayerJoinsGame(IServerPlayer player) {
			try {
				if (player.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID") is not null) {
					serverAPI.Logger.Notification(player.PlayerName + " is member of: " + DataUtility.GetKingdomName(player.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID") + ", reloading data."));
					kingdomList.Find(kingdomMatch => kingdomMatch.PlayerUIDs.Contains(player.PlayerUID)).EntityUIDs.Append(player.Entity.EntityId);
					SaveAllData();
				}
			} catch (NullReferenceException) {
				serverAPI.Logger.Notification(player.PlayerName + " does not have kingdomUID string.");
			}
			UpdateDicts();
		}

		private void PlayerLeaveGame(IServerPlayer player) {
			try {
				if (player.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID") is not null) {
					serverAPI.Logger.Notification(player.PlayerName + " was member of: " + DataUtility.GetKingdomName(player.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID") + ", unloading data."));
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
					fullData += "\nKingdom: " + LangUtility.Fix(kingdom.KingdomName) + "\nUID: " + kingdom.KingdomGUID;
					fullData += "\nLeaders: " + kingdom.LeadersName + "\nUID: " + kingdom.LeadersGUID;
					fullData += "\nFounded: " + kingdom.FoundingIRL + "\nGameDay: " + kingdom.FoundingIGT;
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
			bool adminBypass = args.Caller.HasPrivilege("admin") || caller.PlayerName == "BadRabbit49";
			Kingdom thisKingdom = null;
			Kingdom thatKingdom = null;

			try {
				thisKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.PlayerUIDs.Contains(callerUID));
				// If it still is null then try this.
				if (thisKingdom == null) {
					serverAPI.Logger.Notification("Method 1 failed, trying again...");
					thisKingdom = caller.Entity.GetBehavior<EntityBehaviorLoyalties>().cachedKingdom;
				}
				if (thisKingdom == null) {
					serverAPI.Logger.Notification("Method 2 failed, trying again...");
					thisKingdom = playerDict.Get(callerUID);
				}
				if (thisKingdom == null) {
					serverAPI.Logger.Notification("Method 3 failed, bruh...");
				}
			} catch (NullReferenceException) { }
			try {
				thatKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomName.ToLowerInvariant() == argument.ToLowerInvariant());
			} catch (NullReferenceException) { }
			
			if (adminBypass) {
				serverAPI.Logger.Notification(GetKingdomData());
			}

			switch (commands) {
				// Creates new owned Kingdom.
				case "create":
					foreach (Kingdom eachEmp in kingdomList) {
						if (argument is null) {
							return TextCommandResult.Error(LangUtility.Get("command-error-create00"));
						}
						if (argument.ToLower() == eachEmp.KingdomName.ToLower()) {
							return TextCommandResult.Error(LangUtility.Get("command-error-create01"));
						}
						if (eachEmp.PlayerUIDs.Contains(caller.PlayerUID) && !adminBypass) {
							return TextCommandResult.Error(LangUtility.Get("command-error-create02"));
						}
					}
					CreateKingdom(argument, null, caller.PlayerUID, caller, (adminBypass && (thisKingdom is null || thisKingdom.KingdomGUID == "00000000")));
					caller.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/cashregister"), caller.Entity);
					return TextCommandResult.Success(LangUtility.Get("command-success-create") + LangUtility.Fix(argument) + "!");
				// Deletes the owned Kingdom.
				case "delete":
					if (adminBypass) {
						try {
							DeleteKingdom(thatKingdom);
							return TextCommandResult.Success(LangUtility.Get("command-success-delete") + argument + "!");
						} catch {
							return TextCommandResult.Error(LangUtility.Get("command-error-delete00"));
						}
					}
					if (thisKingdom is null || thisKingdom.KingdomGUID == "00000000") {
						return TextCommandResult.Error(LangUtility.Get("command-error-delete00"));
					}
					if (thisKingdom.LeadersGUID != caller.PlayerUID) {
						return TextCommandResult.Error(LangUtility.Get("command-error-delete01"));
					}
					if (thisKingdom.LeadersGUID == caller.PlayerUID) {
						foreach (string member in DataUtility.GetOnlineUIDs(thisKingdom.KingdomGUID)) {
							serverAPI.World.PlayerByUid(member)?.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/deepbell"), serverAPI.World.PlayerByUid(member)?.Entity);
						}
						DeleteKingdom(thatKingdom);
						return TextCommandResult.Success(LangUtility.Get("command-success-delete") + argument + "!");
					}
					return TextCommandResult.Error(LangUtility.Get("command-error-unknown0"));
				// Invites player to join Kingdom.
				case "invite":
					IServerPlayer foundPlayer = serverAPI.World.PlayerByUid(serverAPI.PlayerData.GetPlayerDataByLastKnownName(argument).PlayerUID) as IServerPlayer;
					if (!serverAPI.World.AllOnlinePlayers.Contains(foundPlayer)) {
						return TextCommandResult.Error(LangUtility.Get("command-error-invite04"));
					} else {
						SendRequestedDialog(foundPlayer, caller, caller.Entity.GetBehavior<EntityBehaviorLoyalties>().kingdomUID, "invite");
						return TextCommandResult.Success(LangUtility.Get("command-success-invite") + argument);
					}
				// Removes play from Kingdom.
				case "remove":
					// TODO: MAKE THIS WORK.
					return TextCommandResult.Success(LangUtility.Get("command-success-remove") + argument);
				// Requests access to Leader.
				case "become":
					if (!kingdomList.Contains(DataUtility.GetKingdom(null, argument))) {
						return TextCommandResult.Error(LangUtility.Get("command-error-become00"));
					}
					if (DataUtility.GetKingdom(null, argument).PlayerUIDs.Contains(caller.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.Get("command-error-become01"));
					}
					// Send request to Kingdom leader!
					SendRequestedDialog(caller, DataUtility.GetLeaders(argument, null), DataUtility.GetKingdomGUID(argument), "become");
					return TextCommandResult.Success(LangUtility.Get("command-success-become") + argument + "!");
				// Leaves current Kingdom.
				case "depart":
					if (!caller.Entity.HasBehavior<EntityBehaviorLoyalties>()) {
						return TextCommandResult.Error(LangUtility.Get("command-error-depart00"));
					}
					if (caller.Entity.GetBehavior<EntityBehaviorLoyalties>()?.kingdomUID is null) {
						return TextCommandResult.Error(LangUtility.Get("command-error-depart01"));
					}
					if (serverAPI.World.Claims.All.Any(landClaim => landClaim.OwnedByPlayerUid == caller.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.Get("command-error-depart02"));
					}
					DepartKingdom(caller);
					return TextCommandResult.Success(LangUtility.Get("command-success-depart"));
				// Rename the Kingdom.
				case "rename":
					if (argument is null) {
						return TextCommandResult.Error("No argument. Provide a kingdom name.");
					}
					if (thisKingdom is null || thisKingdom.KingdomGUID == "00000000") {
						return TextCommandResult.Error("You are not part of a kingdom.");
					}
					if (DataUtility.GetLeadersUID(null, callerUID) != callerUID) {
						return TextCommandResult.Error("You are not the leader of your kingdom.");
					}
					string oldName = LangUtility.Fix(thisKingdom.KingdomName);
					string newName = LangUtility.Low(argument);
					thisKingdom.KingdomName = newName;
					thisKingdom.KingdomLong = newName;
					return TextCommandResult.Success("Successfully renamed " + oldName + " to " + LangUtility.Fix(newName));
				// Declares war on Kingdom.
				case "setwar":
					if (argument is null) {
						return TextCommandResult.Error("No argument. Provide a kingdom name.");
					}
					if (thisKingdom is null || thisKingdom.KingdomGUID == "00000000") {
						return TextCommandResult.Error("You are not part of a kingdom.");
					}
					if (DataUtility.GetLeadersUID(thisKingdom.KingdomGUID, callerUID) != callerUID) {
						return TextCommandResult.Error("You are not the leader of your kingdom.");
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
			bool adminBypass = args.Caller.HasPrivilege("admin");

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