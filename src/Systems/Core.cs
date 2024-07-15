using HarmonyLib;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace VSKingdom {
	public class VSKingdom : ModSystem {
		Harmony harmony = new Harmony("badrabbit49.vskingdom");
		public ICoreClientAPI clientAPI;
		public ICoreServerAPI serverAPI;
		
		public bool fsmEnabled;
		
		private List<Kingdom> kingdomList;
		private List<Culture> cultureList;

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
			AiTaskRegistry.Register<AiTaskSentryAttack>("SentryAttack");
			AiTaskRegistry.Register<AiTaskSentryEscape>("SentryEscape");
			AiTaskRegistry.Register<AiTaskSentryFollow>("SentryFollow");
			AiTaskRegistry.Register<AiTaskSentryHealth>("SentryHealth");
			AiTaskRegistry.Register<AiTaskSentryRanged>("SentryRanged");
			AiTaskRegistry.Register<AiTaskSentryReturn>("SentryReturn");
			AiTaskRegistry.Register<AiTaskSentrySearch>("SentrySearch");
			AiTaskRegistry.Register<AiTaskSentryWander>("SentryWander");
			AiTaskRegistry.Register<AiTaskSentryWaters>("SentryWaters");
			// Patch Everything.
			if (!Harmony.HasAnyPatches("badrabbit49.vskingdom")) {
				harmony.PatchAll();
			}
			// Check if maltiez FSMlib is enabled.
			fsmEnabled = api.ModLoader.IsModEnabled("fsmlib");
			// Create chat commands for creation, deletion, invitation, and so of kingdoms.
			api.ChatCommands.Create("kingdom")
				.WithAdditionalInformation(LangUtility.Get(""))
				.WithDescription(LangUtility.Get(""))
				.RequiresPrivilege(Privilege.chat)
				.RequiresPlayer()
				.WithArgs(api.ChatCommands.Parsers.Word("commands", new string[] { "create", "delete", "update", "invite", "remove", "become", "depart", "revolt", "rebels", "attack", "treaty", "outlaw", "pardon", "wanted", "accept", "reject", "ballot", "voting" }), api.ChatCommands.Parsers.OptionalAll("argument"))
				.HandleWith(new OnCommandDelegate(OnKingdomCommand));
			// Create chat commands for creation, deletion, invitation, and so of cultures.
			api.ChatCommands.Create("culture")
				.RequiresPrivilege(Privilege.chat)
				.RequiresPlayer()
				.WithArgs(api.ChatCommands.Parsers.Word("commands", new string[] { "create", "delete", "update", "invite", "accept", "reject" }), api.ChatCommands.Parsers.OptionalAll("argument"))
				.HandleWith(new OnCommandDelegate(OnCultureCommand));
		}

		public override void StartClientSide(ICoreClientAPI capi) {
			base.StartClientSide(capi);
			this.clientAPI = capi;
			capi.Event.LevelFinalize += () => LevelFinalize(capi);
		}

		public override void StartServerSide(ICoreServerAPI sapi) {
			base.StartServerSide(sapi);
			this.serverAPI = sapi;
			sapi.Event.SaveGameCreated += MakeAllData;
			sapi.Event.GameWorldSave += SaveAllData;
			sapi.Event.SaveGameLoaded += LoadAllData;
			sapi.Event.PlayerJoin += PlayerJoinsGame;
			sapi.Event.PlayerDisconnect += PlayerLeaveGame;
			sapi.Event.ServerRunPhase(EnumServerRunPhase.Shutdown, SaveAllData);
			sapi.Network.RegisterChannel("sentrynetwork")
				.RegisterMessageType<long>().SetMessageHandler<long>(OnSentryUpdated)
				.RegisterMessageType<SentryOrders>().SetMessageHandler<SentryOrders>(OnSentryOrdered);
		}

		public override void Dispose() {
			// Unload and Unpatch everything from the mod.
			harmony?.UnpatchAll(Mod.Info.ModID);
			base.Dispose();
		}

		private void MakeAllData() {
			byte[] kingdomData = serverAPI.WorldManager.SaveGame.GetData("kingdomData");
			byte[] cultureData = serverAPI.WorldManager.SaveGame.GetData("cultureData");
			kingdomList = kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);
			cultureList = cultureData is null ? new List<Culture>() : SerializerUtil.Deserialize<List<Culture>>(cultureData);
			if (!kingdomList.Exists(kingdomMatch => kingdomMatch.KingdomGUID.Contains("00000000"))) {
				CreateKingdom("00000000", LangUtility.Get("entries-keyword-common"), null, false);
				var commoners = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == "00000000");
				commoners.MembersROLE = "Commoner/T/T/T/T/F/F";
				commoners.EnemiesGUID.Add("xxxxxxxx");
			}
			if (!kingdomList.Exists(kingdomMatch => kingdomMatch.KingdomGUID.Contains("xxxxxxxx"))) {
				CreateKingdom("xxxxxxxx", LangUtility.Get("entries-keyword-bandit"), null, false);
				var banditrys = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == "xxxxxxxx");
				banditrys.MembersROLE = "Banditry/T/T/T/T/F/F";
				banditrys.EnemiesGUID.Add("00000000");
			}
			if (!cultureList.Exists(cultureMatch => cultureMatch.CultureGUID.Contains("00000000"))) {
				CreateCulture("00000000", LangUtility.Get("entries-keyword-seraph"), null, false);
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
				serverAPI.SendMessage(player, 0, LangUtility.Ref("command-error-cantfind", "entries-keyword-kingdom"), EnumChatType.Notification);
				player.Entity.WatchedAttributes.GetOrAddTreeAttribute("loyalties").SetString("kingdom_guid", "00000000");
			}
			if (cultureGUID is null || cultureGUID == "" || CultureExists(cultureGUID) == false) {
				serverAPI.SendMessage(player, 0, LangUtility.Ref("command-error-cantfind", "entries-keyword-culture"), EnumChatType.Notification);
				player.Entity.WatchedAttributes.GetOrAddTreeAttribute("loyalties").SetString("culture_guid", "00000000");
			}
			UpdateSentries(player.Entity.ServerPos.XYZ);
		}

		private void PlayerLeaveGame(IServerPlayer player) {
			try {
				if (player.Entity.WatchedAttributes.GetOrAddTreeAttribute("loyalties").HasAttribute("kingdom_guid")) {
					serverAPI.Logger.Notification($"{player.PlayerName} was member of {GetKingdomNAME(player.Entity.WatchedAttributes.GetTreeAttribute("loyalties").GetString("kingdom_guid"))}, unloading data.");
					kingdomList.Find(kingdomMatch => kingdomMatch.PlayersGUID.Contains(player.PlayerUID)).EntitiesALL.Remove(player.Entity.EntityId);
					UpdateSentries(player.Entity.ServerPos.XYZ);
				}
			} catch (NullReferenceException) {
				serverAPI.Logger.Error($"{player.PlayerName} didn't have a kingdomGUID string.");
			}
			SaveAllData();
		}

		private void UpdateSentries(Vec3d position) {
			var nearbyEnts = serverAPI.World.GetEntitiesAround(position, 120, 60, (ent => (ent is EntitySentry)));
			foreach (var sentry in nearbyEnts) {
				serverAPI.Network.BroadcastEntityPacket(sentry.EntityId, 1502, serverAPI.WorldManager.SaveGame.GetData("kingdomData"));
			}
		}

		private void OnSentryUpdated(IServerPlayer fromPlayer, long entityID) {
			EntitySentry sentry = serverAPI.World.GetEntityById(entityID) as EntitySentry;
			serverAPI.Network.BroadcastEntityPacket(sentry.EntityId, 1502, serverAPI.WorldManager.SaveGame.GetData("kingdomData"));
		}

		private void OnSentryOrdered(IServerPlayer fromPlayer, SentryOrders sentryOrders) {
			EntitySentry sentry = serverAPI.World.GetEntityById(sentryOrders.entityUID) as EntitySentry;
			serverAPI.Network.BroadcastEntityPacket(sentry.EntityId, 1503, SerializerUtil.Serialize<SentryOrders>(sentryOrders));
		}

		private TextCommandResult OnKingdomCommand(TextCommandCallingArgs args) {
			string fullargs = (string)args[1];
			string callerID = args.Caller.Player.PlayerUID;
			IPlayer thisPlayer = args.Caller.Player;
			IPlayer thatPlayer = serverAPI.World.PlayerByUid(serverAPI.PlayerData.GetPlayerDataByLastKnownName(fullargs)?.PlayerUID) ?? null;
			Kingdom thisKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thisPlayer.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid")) ?? null;
			Kingdom thatKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomNAME.ToLowerInvariant() == fullargs?.ToLowerInvariant()) ?? null;
			serverAPI.Logger.Notification(thisPlayer.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid"));
			// Determine privillege role level and if they are allowed to make new kingdoms/cultures.
			bool inKingdom = thisKingdom != null && thisKingdom.KingdomGUID != "00000000";
			bool theLeader = inKingdom && thisKingdom.LeadersGUID == callerID;
			bool usingArgs = fullargs != null && fullargs != "" && fullargs != " ";
			bool canInvite = inKingdom && KingUtility.GetRolesPRIV(thisKingdom.MembersROLE, KingUtility.GetMemberROLE(thisKingdom.PlayersINFO, callerID))[5];
			bool adminPass = args.Caller.HasPrivilege(Privilege.controlserver) || thisPlayer.PlayerName == "BadRabbit49";
			bool canCreate = args.Caller.GetRole(serverAPI).PrivilegeLevel >= serverAPI.World.Config.GetInt("MinCreateLevel", -1);
			bool maxCreate = serverAPI.World.Config.GetInt("MaxNewKingdoms", -1) != -1 || serverAPI.World.Config.GetInt("MaxNewKingdoms", -1) < (kingdomList.Count + 1);

			string[] keywords = { LangUtility.Get("entries-keyword-kingdom"), LangUtility.Get("entries-keyword-player"), LangUtility.Get("entries-keyword-players") };

			if (adminPass && inKingdom) {
				try { serverAPI.SendMessage(thisPlayer, 0, KingUtility.ListedAllData(serverAPI, thisKingdom.KingdomGUID), EnumChatType.OwnMessage); } catch { }
			}

			switch ((string)args[0]) {
				// Creates new owned Kingdom.
				case "create":
					if (!usingArgs) {
						return TextCommandResult.Error(LangUtility.Set("command-error-argsnone", string.Concat(args.Command.ToString(), (string)args[0])));
					} else if (NameTaken(fullargs)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-nametook", keywords[0]));
					} else if (InKingdom(callerID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-ismember", keywords[0]));
					} else if (!canCreate) {
						return TextCommandResult.Error(LangUtility.Set("command-error-badperms", (string)args[0]));
					} else if (!maxCreate) {
						return TextCommandResult.Error(LangUtility.Set("command-error-capacity", keywords[0]));
					}
					CreateKingdom(null, fullargs, thisPlayer.PlayerUID, !inKingdom);
					return TextCommandResult.Success(LangUtility.Set("command-success-create", fullargs));
				// Deletes the owned Kingdom.
				case "delete":
					if (usingArgs && adminPass && thatKingdom is not null) {
						DeleteKingdom(thatKingdom.KingdomGUID);
						return TextCommandResult.Success(LangUtility.Set("command-success-delete", fullargs));
					} else if (!usingArgs || thatKingdom is null) {
						return TextCommandResult.Error(LangUtility.Set("command-error-cantfind", keywords[0]));
					} else if (!adminPass && thatKingdom.LeadersGUID != callerID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-badperms", (string)args[0]));
					}
					DeleteKingdom(thatKingdom.KingdomGUID);
					return TextCommandResult.Success(LangUtility.Set("command-success-delete", fullargs));
				// Updates and changes kingdom properties.
				case "update":
					if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.Set("command-error-nopartof", keywords[0]));
					} else if (!usingArgs) {
						return TextCommandResult.Error(LangUtility.Set("command-error-argument", fullargs));
					} else if (!adminPass && thisKingdom.LeadersGUID != callerID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-badperms", fullargs));
					}
					string[] fullset = { fullargs };
					try { fullset = fullargs.Split(' '); } catch { }
					string results = ChangeKingdom(thisKingdom.KingdomGUID, fullset[0], fullset[1], string.Join(' ', fullset.Skip(2)));
					thisPlayer.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/writing"), thisPlayer.Entity);
					return TextCommandResult.Success(results);
				// Invites player to join Kingdom.
				case "invite":
					/** TODO: THIS DOESN'T WANT TO PROPERLY SEND INVITES, DETERMINE IF THEY ARE GETTING THROUGH AND BEING SAVED. **/
					if (!usingArgs && canInvite && thisPlayer.CurrentEntitySelection.Entity is EntityPlayer playerEnt) {
						thisKingdom.InvitesGUID.Add(playerEnt.PlayerUID);
						SaveKingdom();
						serverAPI.SendMessage(thatPlayer, 0, LangUtility.Set("command-message-invite", thisPlayer.PlayerName, thisKingdom.KingdomNAME), EnumChatType.OwnMessage);
						return TextCommandResult.Success(LangUtility.Set("command-success-invite", playerEnt.Player.PlayerName));
					} else if (thatPlayer == null || !serverAPI.World.AllOnlinePlayers.Contains(thatPlayer)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-noplayer", fullargs));
					} else if (thisKingdom.PlayersGUID.Contains(thatPlayer.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-playerin", thisKingdom.KingdomNAME));
					} else if (!canInvite) {
						return TextCommandResult.Error(LangUtility.Set("command-error-badperms", (string)args[0]));
					}
					thisKingdom.InvitesGUID.Add(thatPlayer.PlayerUID);
					SaveKingdom();
					serverAPI.SendMessage(thatPlayer, 0, LangUtility.Get("command-message-invite").Replace("[ENTRY1]", thisPlayer.PlayerName).Replace("[ENTRY2]", thisKingdom.KingdomNAME), EnumChatType.OwnMessage);
					return TextCommandResult.Success(LangUtility.Set("command-success-invite", fullargs));
				// Accept invites and requests.
				case "accept":
					if (!usingArgs) {
						return TextCommandResult.Success(KingdomInvite(callerID, canInvite));
					} else if (thatKingdom != null && inKingdom && theLeader && thisKingdom.PeaceOffers.Contains(thatKingdom.KingdomGUID)) {
						serverAPI.BroadcastMessageToAllGroups(LangUtility.Set("command-message-peacea", thisKingdom.KingdomLONG, thatKingdom.KingdomLONG), EnumChatType.OwnMessage);
						EndWarKingdom(thisKingdom.KingdomGUID, thatKingdom.KingdomGUID);
						return TextCommandResult.Success(LangUtility.Set("command-success-treaty", fullargs));
					} else if (thatPlayer != null && inKingdom && thisKingdom.RequestGUID.Contains(thatPlayer.PlayerUID)) {
						if (!canInvite) {
							return TextCommandResult.Error(LangUtility.Set("command-error-badperms", string.Concat((string)args[0], thatPlayer.PlayerName)));
						}
						SwitchKingdom(thatPlayer as IServerPlayer, thisKingdom.KingdomGUID);
						return TextCommandResult.Success(LangUtility.Set("command-success-accept", thatPlayer.PlayerName));
					} else if (thatKingdom != null && thatKingdom.InvitesGUID.Contains(thisPlayer.PlayerUID)) {
						SwitchKingdom(thisPlayer as IServerPlayer, thatKingdom.KingdomGUID);
						serverAPI.SendMessage(serverAPI.World.PlayerByUid(thatKingdom.LeadersGUID), 0, LangUtility.Set("command-message-accept", thisPlayer.PlayerName, thatKingdom.KingdomLONG), EnumChatType.OwnMessage);
						return TextCommandResult.Success(LangUtility.Set("command-success-accept", thatPlayer.PlayerName));
					}
					return TextCommandResult.Error(LangUtility.Set("command-error-noinvite", keywords[0]));
				// Reject invites and requests.
				case "reject":
					if (!usingArgs) {
						return TextCommandResult.Success(KingdomInvite(callerID, canInvite));
					} else if (thatKingdom != null && inKingdom && theLeader && thisKingdom.PeaceOffers.Contains(thatKingdom.KingdomGUID)) {
						serverAPI.BroadcastMessageToAllGroups(LangUtility.Set("command-message-peacer", thisKingdom.KingdomLONG, thatKingdom.KingdomLONG), EnumChatType.OwnMessage);
						kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thisKingdom.KingdomGUID).PeaceOffers.Remove(thatKingdom.KingdomGUID);
						SaveKingdom();
						return TextCommandResult.Success(LangUtility.Set("command-failure-treaty", fullargs));
					} else if (thatPlayer != null && inKingdom && thisKingdom.RequestGUID.Contains(thatPlayer.PlayerUID)) {
						if (!canInvite) {
							return TextCommandResult.Error(LangUtility.Set("command-error-badperms", string.Concat((string)args[0], thatPlayer.PlayerName)));
						}
						thisKingdom.RequestGUID.Remove(thatPlayer.PlayerUID);
						return TextCommandResult.Success(LangUtility.Set("command-success-reject", thatPlayer.PlayerName));
					} else if (thatKingdom != null && thatKingdom.InvitesGUID.Contains(thisPlayer.PlayerUID)) {
						thatKingdom.InvitesGUID.Remove(callerID);
						serverAPI.SendMessage(serverAPI.World.PlayerByUid(thatKingdom.LeadersGUID), 0, (thisPlayer.PlayerName + LangUtility.Set("command-choices-reject", thatKingdom.KingdomLONG)), EnumChatType.OwnMessage);
						return TextCommandResult.Success(LangUtility.Set("command-success-reject", thatPlayer.PlayerName));
					}
					return TextCommandResult.Error(LangUtility.Set("command-error-noinvite", keywords[0]));
				// Removes player from Kingdom.
				case "remove":
					if (thatPlayer == null || !serverAPI.World.AllOnlinePlayers.Contains(thatPlayer)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-noplayer", fullargs));
					} else if (!thisKingdom.PlayersGUID.Contains(thatPlayer.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-nomember", thisKingdom.KingdomNAME));
					} else if (!adminPass && GetLeadersGUID(null, callerID) != callerID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-badperms", string.Concat((string)args[0], thatPlayer.PlayerName)));
					}
					/** TODO: ADD SPECIAL CIRCUMSTANCE BASED ON PRIVILEGE AND ELECTIONS **/
					thisKingdom.PlayersINFO.Remove(KingUtility.GetMemberINFO(thisKingdom.PlayersINFO, thatPlayer.PlayerUID));
					thisKingdom.PlayersGUID.Remove(thatPlayer.PlayerUID);
					SaveKingdom();
					return TextCommandResult.Success(LangUtility.Set("command-success-remove", fullargs));
				// Requests access to Leader.
				case "become":
					if (!KingdomExists(null, fullargs)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-notexist", keywords[0]));
					} else if (GetKingdom(null, fullargs).PlayersGUID.Contains(thisPlayer.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-ismember", keywords[0]));
					}
					/** TODO: ADD REQUEST TO JOIN TO QUERY thatKingdom.RequestGUID **/
					thatKingdom.RequestGUID.Add(thisPlayer.PlayerUID);
					SaveKingdom();
					serverAPI.SendMessage(serverAPI.World.PlayerByUid(thatKingdom.LeadersGUID), 0, LangUtility.Set("command-message-invite", thisPlayer.PlayerName, thisKingdom.KingdomNAME), EnumChatType.OwnMessage);
					return TextCommandResult.Success(LangUtility.Set("command-success-become", fullargs));
				// Leaves current Kingdom.
				case "depart":
					if (!thisPlayer.Entity.HasBehavior<EntityBehaviorLoyalties>()) {
						return TextCommandResult.Error(LangUtility.Set("command-error-unknowns", (string)args[0]));
					} else if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.Set("command-error-nopartof", keywords[0]));
					} else if (serverAPI.World.Claims.All.Any(landClaim => landClaim.OwnedByPlayerUid == thisPlayer.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-ownsland", keywords[0]));
					}
					string kingdomName = thisKingdom.KingdomNAME;
					SwitchKingdom(thisPlayer as IServerPlayer, "00000000");
					return TextCommandResult.Success(LangUtility.Set("command-success-depart", kingdomName));
				// Revolt against the Kingdom.
				case "revolt":
					if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.Set("command-error-nopartof", keywords[0]));
					} else if (theLeader) {
						return TextCommandResult.Error(LangUtility.Set("command-error-isleader", (string)args[0]));
					}
					thisKingdom.OutlawsGUID.Add(callerID);
					SaveKingdom();
					return TextCommandResult.Success(LangUtility.Set("command-success-revolt", thisKingdom.KingdomNAME));
				// Rebels against the Kingdom.
				case "rebels":
					if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.Set("command-error-nopartof", keywords[0]));
					} else if (theLeader) {
						return TextCommandResult.Error(LangUtility.Set("command-error-isleader", (string)args[0]));
					}
					thisKingdom.OutlawsGUID.Add(callerID);
					SwitchKingdom(thisPlayer as IServerPlayer, "00000000");
					return TextCommandResult.Success(LangUtility.Set("command-success-rebels", thisKingdom.KingdomNAME));
				// Declares war on Kingdom.
				case "attack":
					if (!IsKingdom(null, fullargs)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-notexist", fullargs));
					} else if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.Set("command-error-nopartof", keywords[0]));
					} else if (GetLeadersGUID(thisKingdom.KingdomGUID, callerID) != callerID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-badperms", (string)args[0]));
					} else if (thisKingdom.EnemiesGUID.Contains(thatKingdom.KingdomGUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-atwarnow", thatKingdom.KingdomNAME));
					} else {
						SetWarKingdom(thisKingdom.KingdomGUID, thatKingdom.KingdomGUID);
						return TextCommandResult.Success(LangUtility.Set("command-success-attack", fullargs));
					}
				// Declares peace to Kingdom.
				case "treaty":
					if (!IsKingdom(null, fullargs)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-notexist", fullargs));
					} else if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.Set("command-error-nopartof", keywords[0]));
					} else if (GetLeadersGUID(thisKingdom.KingdomGUID, callerID) != callerID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-badperms", (string)args[0]));
					} else if (!thisKingdom.EnemiesGUID.Contains(thatKingdom.KingdomGUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-notatwar", thatKingdom.KingdomNAME));
					}
					if (thisKingdom.PeaceOffers.Contains(thatKingdom.KingdomGUID)) {
						EndWarKingdom(thisKingdom.KingdomGUID, thatKingdom.KingdomGUID);
					} else {
						kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thatKingdom.KingdomGUID).PeaceOffers.Add(thisKingdom.KingdomGUID);
						SaveKingdom();
						serverAPI.BroadcastMessageToAllGroups(LangUtility.Set("command-message-peaces", thisKingdom.KingdomLONG, thatKingdom.KingdomLONG), EnumChatType.OwnMessage);
					}
					return TextCommandResult.Success(LangUtility.Set("command-success-treaty", fullargs));
				// Sets an enemy of the Kingdom.
				case "outlaw":
					if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.Set("command-error-nopartof", keywords[0]));
					} else if (thatPlayer == null || !serverAPI.World.AllOnlinePlayers.Contains(thatPlayer)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-noplayer", fullargs));
					} else if (!theLeader) {
						return TextCommandResult.Error(LangUtility.Set("command-error-badperms", (string)args[0]));
					}
					thisKingdom.OutlawsGUID.Add(thatPlayer.PlayerUID);
					SaveKingdom();
					return TextCommandResult.Success(LangUtility.Set("command-success-outlaw", thatPlayer.PlayerName));
				// Pardons an enemy of the Kingdom.
				case "pardon":
					if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.Set("command-error-nopartof", keywords[0]));
					} else if (thatPlayer == null || !serverAPI.World.AllOnlinePlayers.Contains(thatPlayer)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-noplayer", fullargs));
					} else if (!theLeader) {
						return TextCommandResult.Error(LangUtility.Set("command-error-badperms", (string)args[0]));
					}
					thisKingdom.OutlawsGUID.Remove(thatPlayer.PlayerUID);
					SaveKingdom();
					return TextCommandResult.Success(LangUtility.Set("command-success-pardon", thatPlayer.PlayerName));
				// Gets all enemies of the Kingdom.
				case "wanted":
					if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.Set("command-error-nopartof", keywords[0]));
					}
					return TextCommandResult.Success(LangUtility.Set("command-success-wanted", keywords[2], thisKingdom.KingdomLONG, KingdomWanted(thisKingdom.OutlawsGUID)));
			}
			if ((string)args[0] == null || ((string)args[0]).Contains("help")) {
				return TextCommandResult.Success(CommandInfo("kingdom", "help"));
			}
			if (((string)args[0]).Contains("desc")) {
				return TextCommandResult.Success(CommandInfo("kingdom", "desc"));
			}
			return TextCommandResult.Error(LangUtility.Get("command-help-kingdom"));
		}

		private TextCommandResult OnCultureCommand(TextCommandCallingArgs args) {
			string fullargs = (string)args[1];
			string callerID = args.Caller.Player.PlayerUID;
			IPlayer thisPlayer = args.Caller.Player;
			IPlayer thatPlayer = serverAPI.World.PlayerByUid(serverAPI.PlayerData.GetPlayerDataByLastKnownName(fullargs)?.PlayerUID) ?? null;
			Culture thisCulture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == thisPlayer.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("culture_guid")) ?? null;
			Culture thatCulture = cultureList.Find(cultureMatch => cultureMatch.CultureNAME.ToLowerInvariant() == fullargs?.ToLowerInvariant()) ?? null;
			// Determine privillege role level and if they are allowed to make new kingdoms/cultures.
			bool inCulture = thisCulture != null && thisCulture.CultureGUID != "00000000";
			bool usingArgs = fullargs != null && fullargs != "" && fullargs != " ";
			bool adminPass = args.Caller.HasPrivilege(Privilege.controlserver) || thisPlayer.PlayerName == "BadRabbit49";
			bool canCreate = args.Caller.GetRole(serverAPI).PrivilegeLevel >= serverAPI.World.Config.GetInt("MinCreateLevel", -1);
			bool maxCreate = serverAPI.World.Config.GetInt("MaxNewCultures", -1) != -1 && serverAPI.World.Config.GetInt("MaxNewCultures", -1) < (kingdomList.Count + 1);
			bool hoursTime = (serverAPI.World.Calendar.TotalHours - thisCulture.FoundedHOUR) > (serverAPI.World.Calendar.TotalHours - serverAPI.World.Config.GetInt("MinCultureMake"));

			string[] keywords = { LangUtility.Get("entries-keyword-culture"), LangUtility.Get("entries-keyword-player"), LangUtility.Get("entries-keyword-players") };

			switch ((string)args[0]) {
				// Creates brand new Culture.
				case "create":
					if (!usingArgs) {
						return TextCommandResult.Error(LangUtility.Set("command-error-argsnone", (string)args[0]));
					} else if (cultureList.Exists(cultureMatch => cultureMatch.CultureNAME.ToLowerInvariant() == fullargs?.ToLowerInvariant())) {
						return TextCommandResult.Error(LangUtility.Set("command-error-nametook", keywords[0]));
					} else if (!canCreate && !adminPass) {
						return TextCommandResult.Error(LangUtility.Set("command-error-badperms", (string)args[0]));
					} else if (!maxCreate && !adminPass) {
						return TextCommandResult.Error(LangUtility.Set("command-error-capacity", keywords[0]));
					} else if (!hoursTime && !adminPass) {
						return TextCommandResult.Error(LangUtility.Set("command-error-tooearly", (thisCulture.FoundedHOUR - serverAPI.World.Config.GetInt("MinCultureMake")).ToString()));
					}
					CreateCulture(null, fullargs.UcFirst(), callerID, !inCulture);
					return TextCommandResult.Success(LangUtility.Set("command-success-create", fullargs.UcFirst()));
				// Deletes existing culture.
				case "delete":
					if (!cultureList.Exists(cultureMatch => cultureMatch.CultureNAME.ToLowerInvariant() == fullargs?.ToLowerInvariant())) {
						return TextCommandResult.Error(LangUtility.Set("command-error-nopartof", keywords[0]));
					} else if (!adminPass) {
						return TextCommandResult.Error(LangUtility.Set("command-error-notadmin", (string)args[0]));
					}
					DeleteCulture(thatCulture.CultureGUID);
					return TextCommandResult.Success(LangUtility.Set("command-success-delete", fullargs));
				// Edits existing culture.
				case "update":
					if (!inCulture) {
						return TextCommandResult.Error(LangUtility.Set("command-error-nopartof", keywords[0]));
					} else if (!adminPass && thisCulture.FounderGUID != callerID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-badperms", (string)args[0]));
					}
					thisPlayer.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/writing"), thisPlayer.Entity);
					string[] fullset = { fullargs };
					try { fullset = fullargs.Split(' '); } catch { }
					string results = ChangeCulture(thisCulture.CultureGUID, fullset[0], fullset[1], string.Join(' ', fullset.Skip(2)));
					return TextCommandResult.Success(results);
				// Invite a player into culture.
				case "invite":
					if (!usingArgs && thisPlayer.CurrentEntitySelection.Entity is EntityPlayer) {
						thatPlayer = (thisPlayer.CurrentEntitySelection.Entity as EntityPlayer).Player;
						thisCulture.InviteGUID.Add(thatPlayer.PlayerUID);
						SaveCulture();
						return TextCommandResult.Success(LangUtility.Set("command-success-invite", thatPlayer.PlayerName));
					} else if (!usingArgs) {
						return TextCommandResult.Error(LangUtility.Set("command-error-argsnone", keywords[1]));
					} else if (thatPlayer == null) {
						return TextCommandResult.Error(LangUtility.Set("command-error-noplayer", fullargs));
					}
					thisCulture.InviteGUID.Add(thatPlayer.PlayerUID);
					SaveCulture();
					return TextCommandResult.Success(LangUtility.Set("command-success-invite", thatPlayer.PlayerName));
				// Accept invite to join culture.
				case "accept":
					if (!usingArgs) {
						return TextCommandResult.Success(CultureInvite(callerID));
					} else if (thatCulture != null && thatCulture.InviteGUID.Contains(thisPlayer.PlayerUID)) {
						SwitchCulture(thisPlayer as IServerPlayer, thatCulture.CultureGUID);
						return TextCommandResult.Success(LangUtility.Set("command-success-accept", thatCulture.CultureNAME));
					}
					return TextCommandResult.Error(LangUtility.Set("command-error-noinvite", keywords[0]));
				// Reject invite to join culture.
				case "reject":
					if (!usingArgs) {
						return TextCommandResult.Success(CultureInvite(callerID));
					} else if (inCulture && thatPlayer != null && thisCulture.InviteGUID.Contains(thatPlayer.PlayerUID)) {
						thisCulture.InviteGUID.Remove(thatPlayer.PlayerUID);
						SaveCulture();
						return TextCommandResult.Success(LangUtility.Set("command-success-reject", thatPlayer.PlayerUID));
					} else if (thatCulture != null && thatCulture.InviteGUID.Contains(thisPlayer.PlayerUID)) {
						thatCulture.InviteGUID.Remove(thisPlayer.PlayerUID);
						SaveCulture();
						return TextCommandResult.Success(LangUtility.Set("command-success-reject", thatCulture.CultureNAME));
					}
					return TextCommandResult.Error(LangUtility.Set("command-error-noinvite", keywords[0]));
			}
			if (((string)args[0]).Contains("help")) {
				return TextCommandResult.Success(CommandInfo("culture", "help"));
			}
			if (((string)args[0]).Contains("desc")) {
				return TextCommandResult.Success(CommandInfo("culture", "desc"));
			}
			return TextCommandResult.Error(LangUtility.Get("command-help-culture"));
		}

		public void CreateKingdom(string newKingdomGUID, string newKingdomNAME, string founderGUID, bool autoJoin) {
			Kingdom newKingdom = new Kingdom();
			newKingdom.KingdomGUID = GuidUtility.RandomizeGUID(newKingdomGUID, 8, GetKingdomGUIDs());
			newKingdom.KingdomTYPE = KingUtility.CorrectedTYPE(newKingdomNAME);
			newKingdom.KingdomNAME = KingUtility.CorrectedNAME(newKingdom.KingdomTYPE, newKingdomNAME, true, false);
			newKingdom.KingdomLONG = KingUtility.CorrectedNAME(newKingdom.KingdomTYPE, newKingdomNAME, false, true);
			newKingdom.KingdomDESC = null;
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
					entity.WatchedAttributes.GetTreeAttribute("loyalties")?.SetString("kingdom_guid", "00000000");
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
					entity.WatchedAttributes.GetTreeAttribute("loyalties")?.SetString("culture_guid", "00000000");
				}
			}
			cultureList.Remove(culture);
			SaveCulture();
		}

		public void SwitchKingdom(IServerPlayer caller, string kingdomGUID, string specificROLE = null) {
			Kingdom oldKingdom = GetKingdom(caller.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid"));
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
			kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID1).EnemiesGUID.Add(kingdomGUID2);
			kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID2).EnemiesGUID.Add(kingdomGUID1);
			SaveKingdom();
			string kingdomONE = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID1).KingdomNAME;
			string kingdomTWO = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID2).KingdomNAME;
			foreach (var player in serverAPI.World.AllOnlinePlayers) {
				serverAPI.SendMessage(player, 0, LangUtility.Set("command-message-attack", kingdomONE, kingdomTWO), EnumChatType.OwnMessage);
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
				serverAPI.SendMessage(player, 0, LangUtility.Set("command-message-treaty", kingdomONE.KingdomLONG, kingdomTWO.KingdomLONG), EnumChatType.OwnMessage);
				UpdateSentries(player.Entity.ServerPos.XYZ);
			}
			IPlayer leadersONE = serverAPI.World.PlayerByUid(kingdomONE.LeadersGUID);
			IPlayer leadersTWO = serverAPI.World.PlayerByUid(kingdomTWO.LeadersGUID);
			if (serverAPI.World.AllOnlinePlayers.Contains(leadersONE) && serverAPI.World.AllOnlinePlayers.Contains(leadersTWO) && leadersONE.Entity.ServerPos.HorDistanceTo(leadersTWO.Entity.ServerPos) <= 10) {
				foreach (var itemSlot in leadersONE.Entity.GearInventory) {
					if (itemSlot.Itemstack?.Item is ItemBook && itemSlot.Itemstack?.Attributes.GetString("signedby") == null) {
						string composedTreaty = "On the day of " + serverAPI.World.Calendar.PrettyDate() + ", " + kingdomONE.KingdomLONG + " and " + kingdomTWO.KingdomLONG + " agree to formally end all hostilities.";
						itemSlot.Itemstack.Attributes.SetString("text", composedTreaty);
						itemSlot.Itemstack.Attributes.SetString("title", "Treaty of " + kingdomONE.KingdomNAME);
						itemSlot.Itemstack.Attributes.SetString("signedby", leadersONE.PlayerName + " & " + leadersTWO.PlayerName);
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
			string invbox = LangUtility.Set("command-success-invbox", invites.Count.ToString(), string.Join("\n", invites)) ;
			string reqbox = LangUtility.Set("command-success-reqbox", playerNames.Length.ToString(), string.Join("\n", playerNames));
			if (getRequests) {
				return invbox + "\n" + reqbox;
			}
			return invbox;
		}

		public string CultureInvite(string playersGUID) {
			List<string> invites = new List<string>();
			foreach (Culture culture in cultureList) {
				if (culture.InviteGUID.Contains(playersGUID)) {
					invites.Add(culture.CultureNAME);
				}
			}
			return LangUtility.Set("command-success-invbox", invites.Count.ToString(), string.Join("\n", invites));
		}

		public string ChangeKingdom(string kingdomGUID, string subcomm, string subargs, string changes) {
			Kingdom kingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID);
			string[] keywords = { LangUtility.Get("entries-keyword-kingdom"), LangUtility.Get("entries-keyword-player"), LangUtility.Get("entries-keyword-players") };
			switch (subcomm) {
				case "append":
					switch (subargs) {
						case "roles": AddMemberRole(kingdomGUID, changes); break;
						default: return LangUtility.Set("command-help-update-kingdom-append", keywords[0]);
					}
					break;
				case "remove":
					switch (subargs) {
						case "roles": kingdom.MembersROLE = string.Join(":", kingdom.MembersROLE.Split(':').RemoveEntry(kingdom.MembersROLE.Replace("/T", "").Replace("/F", "").Split(':').IndexOf(changes))).TrimEnd(':'); break;
						default: return LangUtility.Set("command-help-update-kingdom-remove", keywords[0]);
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
						default: return LangUtility.Set("command-help-update-kingdom-rename", keywords[0]);
					}
					break;
				case "player":
					switch (subargs) {
						case "roles": SetMemberRole(kingdomGUID, GetAPlayer(changes.Split(' ')[0]).PlayerUID ?? null, changes.Split(' ')[1]); break;
						default: return LangUtility.Set("command-help-update-kingdom-player", keywords[0]);
					}
					break;
				case "getall":
					switch (subargs) {
						case "basic": return KingUtility.ListedAllData(serverAPI, kingdomGUID);
						default: return LangUtility.Set("command-help-update-kingdom-getall", keywords[0]);
					}
				default: return LangUtility.Set("command-failure-update", keywords[0]) + "\n" + LangUtility.Set("command-help-update-kingdom", keywords[0]);
			}
			SaveKingdom();
			return LangUtility.Set("command-success-update", keywords[0]);
		}

		public string ChangeCulture(string cultureGUID, string subcomm, string subargs, string changes) {
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
						default: return LangUtility.Set("command-help-update-culture-append", "culture");
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
						default: return LangUtility.Set("command-help-update-culture-remove", "culture");
					}
					break;
				case "rename":
					switch (subargs) {
						case "title": culture.CultureNAME = changes; break;
						case "longs": culture.CultureLONG = changes; break;
						case "descs": culture.CultureDESC = LangUtility.Mps(changes.Remove(changes.Length - 1).Remove(0).UcFirst()); break;
						default: return LangUtility.Set("command-help-update-culture-rename", "culture");
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
						default: return LangUtility.Set("command-help-update-culture-getall", "culture");
					}
				default: return LangUtility.Set("command-failure-update", "culture") + "\n" + LangUtility.Set("command-help-update", "culture");
			}
			SaveCulture();
			return LangUtility.Set("command-success-update", LangUtility.Get("entries-keyword-culture"));
		}

		private string CommandInfo(string kindsof = "kingdom", string informs = "desc") {
			string[] commands = { "create", "delete", "update", "invite", "remove", "become", "depart", "revolt", "rebels", "attack", "treaty", "outlaw", "pardon", "wanted", "accept", "reject", "voting" };
			string langkeys = "command-" + informs + "-";
			string messages = "";
			foreach (string com in commands) {
				if (Lang.HasTranslation("vskingdom:" + langkeys + com)) {
					messages += "\n" + LangUtility.Ref(langkeys + com, kindsof, ("entries-keyword-" + kindsof), "entries-keyword-players", "entries-keyword-replies");
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
	public class SentryOrders {
		public long entityUID;
		public bool? wandering;
		public bool? following;
		public bool? attacking;
		public bool? pursueing;
		public bool? nighttime;
		public bool? shifttime;
		public bool? returning;
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
		public bool invites;
	}
}