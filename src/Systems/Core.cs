using HarmonyLib;
using ProtoBuf;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
			// Listed out commands for easier access for me.
			string[] kingdomCommands = { "create", "delete", "update", "invite", "become", "depart", "rename", "setent", "attack", "endwar", "getall", "setgov" };
			string[] cultureCommands = { "create", "delete", "update" };
			// Create chat commands for creation, deletion, invitation, and so of kingdoms.
			api.ChatCommands.Create("kingdom")
				.WithDescription(LangUtility.Get("command-desc"))
				.WithAdditionalInformation(LangUtility.Get("command-help"))
				.RequiresPrivilege(Privilege.chat)
				.RequiresPlayer()
				.WithArgs(api.ChatCommands.Parsers.Word("commands", kingdomCommands), api.ChatCommands.Parsers.OptionalAll("argument"))
				.HandleWith(new OnCommandDelegate(OnKingdomCommand));
			// Create chat commands for creation, deletion, invitation, and so of cultures.
			api.ChatCommands.Create("culture")
				.WithDescription(LangUtility.Get("command-desc"))
				.WithAdditionalInformation(LangUtility.Get("command-help"))
				.RequiresPrivilege(Privilege.chat)
				.RequiresPlayer()
				.WithArgs(api.ChatCommands.Parsers.Word("commands", cultureCommands), api.ChatCommands.Parsers.OptionalAll("argument"))
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
			newKingdom.MembersROLE = "Peasant/F/F/F/F/F/F:Citizen/T/T/T/F/F/T:Soldier/T/T/T/F/T/T:Royalty/T/T/T/T/T/T";
			newKingdom.FoundedMETA = DateTime.Now.ToLongDateString();
			newKingdom.FoundedDATE = serverAPI.World.Calendar.PrettyDate();
			newKingdom.FoundedHOUR = serverAPI.World.Calendar.TotalHours;
			if (autoJoin && founderGUID != null && serverAPI.World.PlayerByUid(founderGUID) is IServerPlayer founder) {
				founder.Entity.GetBehavior<EntityBehaviorLoyalties>().kingdomGUID = newKingdom.KingdomGUID;
				newKingdom.LeadersGUID = founderGUID;
				newKingdom.PlayersGUID.Add(founderGUID);
				newKingdom.PlayersINFO.Add(KingUtility.PlayerDetails(founderGUID, newKingdom.MembersROLE, "Royalty"));
				newKingdom.EntitiesALL.Add(founder.Entity.EntityId);
			}
			kingdomList.Add(newKingdom);
			SaveKingdom();
		}

		public void DeleteKingdom(string kingdomGUID) {
			Kingdom kingdom = DataUtility.GetKingdom(kingdomGUID);
			foreach (string member in DataUtility.GetOnlineGUIDs(kingdom.KingdomGUID)) {
				serverAPI.World.PlayerByUid(member)?.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/deepbell"), serverAPI.World.PlayerByUid(member)?.Entity);
			}
			foreach (var entity in serverAPI.World.LoadedEntities.Values) {
				if (entity is not EntityHumanoid) {
					continue;
				}
				if (entity.HasBehavior<EntityBehaviorLoyalties>() && entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") == kingdomGUID) {
					entity.WatchedAttributes.GetTreeAttribute("loyalties")?.SetString("kingdom_guid", "00000000");
				}
			}
			kingdomList.Remove(kingdom);
		}

		public void SwitchKingdom(IServerPlayer caller, string kingdomGUID) {
			Kingdom oldKingdom = DataUtility.GetKingdom(caller.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid"));
			Kingdom newKingdom = DataUtility.GetKingdom(kingdomGUID);
			oldKingdom.PlayersGUID.Remove(caller.PlayerUID);
			oldKingdom.EntitiesALL.Remove(caller.Entity.EntityId);
			oldKingdom.InvitesGUID.Remove(caller.PlayerUID);
			oldKingdom.RequestGUID.Remove(caller.PlayerUID);
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
				oldKingdom.LeadersGUID = KingUtility.MostSeniority(oldKingdom.KingdomGUID);
			}
			newKingdom.PlayersGUID.Add(caller.PlayerUID);
			newKingdom.PlayersINFO.Add(KingUtility.PlayerDetails(caller.PlayerUID, null, newKingdom.MembersROLE));
			newKingdom.EntitiesALL.Add(caller.Entity.EntityId);
			caller.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.SetString("kingdom_guid", kingdomGUID);
			SaveKingdom();
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
			kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID1).CurrentWars.Add(kingdomGUID2);
			kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID2).CurrentWars.Add(kingdomGUID1);
			SaveKingdom();
		}

		public void EndWarKingdom(string kingdomGUID1, string kingdomGUID2) {
			kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID1).CurrentWars.Remove(kingdomGUID2);
			kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID2).CurrentWars.Remove(kingdomGUID1);
			SaveKingdom();
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

		public string GetAllInvites(string playersGUID, bool getRequests) {
			string results;
			List<string> invites = new List<string>();
			foreach (Kingdom kingdom in kingdomList) {
				if (kingdom.InvitesGUID.Contains(playersGUID)) {
					invites.Add(kingdom.KingdomNAME);
				}
			}
			string[] entries = new string[] { invites.Count.ToString() };
			results = LangUtility.Sets("command-success-invbox", entries.Concat(invites.ToArray()).ToArray());
			if (getRequests) {
				List<string> requests = new List<string>();
				foreach (Kingdom kingdom in kingdomList) {
					if (kingdom.PlayersGUID.Contains(playersGUID)) {
						string[] playerGuids = kingdom.RequestGUID.ToArray();
						string[] playerNames = new string[kingdom.RequestGUID.Count];
						foreach (string playerGUID in playerGuids) {
							playerNames.AddToArray(serverAPI.World.PlayerByUid(playerGUID).PlayerName);
						}
						string[] reqEntries = new string[] { playerNames.Length.ToString() };
						return results + "\n\n" + LangUtility.Sets("command-success-reqbox", reqEntries.Concat(playerNames).ToArray());
					}
				}
			}
			return results;
		}

		public string ChangeKingdom(string kingdomGUID, string subcomm, string subargs, string changes) {
			Kingdom kingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID);
			string[] acceptedAppend = new string[] { "add", "addnew", "new" };
			string[] acceptedRemove = new string[] { "delete", "clear", "erase", "ban" };
			string[] acceptedRename = new string[] { "name", "change", "setname" };
			string[] acceptedGetall = new string[] { "getinfo", "info", "details" };
			if (acceptedAppend.Contains(subcomm)) {
				subcomm = "append";
			}
			if (acceptedRemove.Contains(subcomm)) {
				subcomm = "remove";
			}
			if (acceptedRename.Contains(subcomm)) {
				subcomm = "rename";
			}
			if (acceptedGetall.Contains(subcomm)) {
				subcomm = "getall";
			}
			switch (subcomm) {
				case "append":
					switch (subargs) {
						case "roles": AddMemberRole(kingdomGUID, changes); break;
						default: return LangUtility.Set("command-help-update-kingdom-append", "kingdom");
					}
					break;
				case "remove":
					switch (subargs) {
						case "roles": kingdom.MembersROLE = string.Join(":", kingdom.MembersROLE.Split(':').RemoveEntry(kingdom.MembersROLE.Replace("/T", "").Replace("/F", "").Split(':').IndexOf(changes))).TrimEnd(':'); break;
						default: return LangUtility.Set("command-help-update-kingdom-remove", "kingdom");
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
						default: return LangUtility.Set("command-help-update-kingdom-rename", "kingdom");
					}
					break;
				case "player":
					switch (subargs) {
						case "roles": SetMemberRole(kingdomGUID, DataUtility.GetAPlayer(changes.Split(' ')[0]).PlayerUID ?? null, changes.Split(' ')[1]); break;
						default: return LangUtility.Set("command-help-update-kingdom-player", "kingdom");
					}
					break;
				case "getall":
					switch (subargs) {
						case "basic": return KingUtility.ListedAllData(kingdomGUID);
						default: return LangUtility.Set("command-help-update-kingdom-getall", "kingdom");
					}
				default: return LangUtility.Set("command-failure-update", "kingdom") + "\n" + LangUtility.Set("command-help-update-kingdom", "kingdom");
			}
			SaveKingdom();
			return LangUtility.Set("command-success-update", LangUtility.Get("entries-keyword-kingdom"));
		}

		public string ChangeCulture(string cultureGUID, string subcomm, string subargs, string changes) {
			Culture culture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == cultureGUID);
			string[] acceptedAppend = new string[] { "add", "addnew", "new" };
			string[] acceptedRemove = new string[] { "delete", "clear", "erase", "ban" };
			string[] acceptedRename = new string[] { "name", "change", "setname" };
			string[] acceptedGetall = new string[] { "getinfo", "info", "details" };
			if (acceptedAppend.Contains(subcomm)) {
				subcomm = "append";
			}
			if (acceptedRemove.Contains(subcomm)) {
				subcomm = "remove";
			}
			if (acceptedRename.Contains(subcomm)) {
				subcomm = "rename";
			}
			if (acceptedGetall.Contains(subcomm)) {
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
						default: return LangUtility.Set("command-help-update-culture-getall", "culture");
					}
				default: return LangUtility.Set("command-failure-update", "culture") + "\n" + LangUtility.Set("command-help-update", "culture");
			}
			SaveCulture();
			return LangUtility.Set("command-success-update", LangUtility.Get("entries-keyword-culture"));
		}

		private void MakeAllData() {
			byte[] kingdomData = serverAPI.WorldManager.SaveGame.GetData("kingdomData");
			byte[] cultureData = serverAPI.WorldManager.SaveGame.GetData("cultureData");
			kingdomList = kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);
			cultureList = cultureData is null ? new List<Culture>() : SerializerUtil.Deserialize<List<Culture>>(cultureData);
			if (!kingdomList.Exists(kingdomMatch => kingdomMatch.KingdomGUID.Contains("00000000"))) {
				CreateKingdom("00000000", LangUtility.Get("entries-keyword-common"), null, false);
				kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == "00000000").MembersROLE = "Commoner/T/T/T/T/F/F";
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
			if (kingdomGUID is null || kingdomGUID == "") {
				player.Entity.GetBehavior<EntityBehaviorLoyalties>()?.SetKingdom("00000000");
			}
			if (cultureGUID is null || cultureGUID == "") {
				player.Entity.GetBehavior<EntityBehaviorLoyalties>()?.SetCulture("00000000");
			}
		}

		private void PlayerLeaveGame(IServerPlayer player) {
			try {
				if (player.Entity.WatchedAttributes.GetOrAddTreeAttribute("loyalties").HasAttribute("kingdom_guid")) {
					serverAPI.Logger.Notification(player.PlayerName + " was member of: " + DataUtility.GetKingdomNAME(player.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") + ", unloading data."));
					kingdomList.Find(kingdomMatch => kingdomMatch.PlayersGUID.Contains(player.PlayerUID)).EntitiesALL.Remove(player.Entity.EntityId);
					SaveAllData();
				}
			} catch (NullReferenceException) {
				serverAPI.Logger.Error(player.PlayerName + " didn't have a kingdomGUID string.");
			}
		}

		private TextCommandResult OnKingdomCommand(TextCommandCallingArgs args) {
			string fullargs = (string)args[1];
			string callerID = args.Caller.Player.PlayerUID;
			IPlayer thisPlayer = args.Caller.Player;
			IPlayer thatPlayer = serverAPI.World.PlayerByUid(serverAPI.PlayerData.GetPlayerDataByLastKnownName(fullargs)?.PlayerUID) ?? null;
			Kingdom thisKingdom = thisPlayer.Entity.GetBehavior<EntityBehaviorLoyalties>()?.cachedKingdom ?? DataUtility.GetKingdom(thisPlayer.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid")) ?? null;
			Kingdom thatKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomNAME.ToLowerInvariant() == fullargs.ToLowerInvariant()) ?? null;
			
			// Determine privillege role level and if they are allowed to make new kingdoms/cultures.
			bool inKingdom = thisKingdom != null && thisKingdom.KingdomGUID != "00000000";
			bool usingArgs = fullargs != null && fullargs != "" && fullargs != " ";
			bool canInvite = inKingdom && KingUtility.GetRolePrivs(thisKingdom.MembersROLE, KingUtility.GetMemberRole(thisKingdom.KingdomGUID, callerID))[5];
			bool adminPass = args.Caller.HasPrivilege(Privilege.controlserver) || thisPlayer.PlayerName == "BadRabbit49";
			bool canCreate = args.Caller.GetRole(serverAPI).PrivilegeLevel >= serverAPI.World.Config.GetInt("MinCreateLevel", -1);
			bool maxCreate = serverAPI.World.Config.GetInt("MaxNewKingdoms", -1) != -1 || serverAPI.World.Config.GetInt("MaxNewKingdoms", -1) < (kingdomList.Count + 1);

			if (adminPass && inKingdom) {
				serverAPI.Logger.Notification(KingUtility.ListedAllData(thisKingdom.KingdomGUID));
			}

			switch ((string)args[0]) {
				// Creates new owned Kingdom.
				case "create":
					if (!usingArgs) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create00", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (DataUtility.NameTaken(fullargs)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create01", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (DataUtility.InKingdom(callerID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create02", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (!canCreate) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create03", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (!maxCreate) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create04", LangUtility.Get("entries-keyword-kingdom")));
					}
					CreateKingdom(null, fullargs, thisPlayer.PlayerUID, !inKingdom);
					thisPlayer.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/cashregister"), thisPlayer.Entity);
					return TextCommandResult.Success(LangUtility.Set("command-success-create", LangUtility.Fix(fullargs)));
				// Deletes the owned Kingdom.
				case "delete":
					if (!usingArgs || thatKingdom is null) {
						return TextCommandResult.Error(LangUtility.Set("command-error-delete00", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (!adminPass && thisKingdom.LeadersGUID != thisPlayer.PlayerUID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-delete01", LangUtility.Get("entries-keyword-kingdom")));
					}
					DeleteKingdom(thatKingdom.KingdomGUID);
					thisPlayer.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/deepbell"), thisPlayer.Entity);
					return TextCommandResult.Success(LangUtility.Set("command-success-delete", LangUtility.Fix(fullargs)));
				// Updates and changes kingdom properties.
				case "update":
					if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.Set("command-error-update00", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (!adminPass && thisKingdom.LeadersGUID != callerID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-update01", LangUtility.Get("entries-keyword-kingdom")));
					}
					thisPlayer.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/writing"), thisPlayer.Entity);
					string[] fullset = { fullargs };
					try { fullset = fullargs.Split(' '); } catch { }
					string results = ChangeKingdom(thisKingdom.KingdomGUID, fullset[0], fullset[1], string.Join(' ', fullset.Skip(2)));
					return TextCommandResult.Success(results);
				// Invites player to join Kingdom.
				case "invite":
					if (!usingArgs && canInvite && thisPlayer.CurrentEntitySelection.Entity is EntityPlayer playerEnt) {
						thisKingdom.InvitesGUID.Add(playerEnt.PlayerUID);
						serverAPI.SendMessage(playerEnt.Player, 0, (thisPlayer.PlayerName + LangUtility.Set("command-choices-invite", thisKingdom.KingdomNAME)), EnumChatType.OwnMessage);
						return TextCommandResult.Success(LangUtility.Set("command-success-invite", playerEnt.Player.PlayerName));
					}
					if (thatPlayer == null || !serverAPI.World.AllOnlinePlayers.Contains(thatPlayer)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-invite00", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (thisKingdom.PlayersGUID.Contains(thatPlayer.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-invite01", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (!canInvite) {
						return TextCommandResult.Error(LangUtility.Set("command-error-invite02", LangUtility.Get("entries-keyword-kingdom")));
					}
					thisKingdom.InvitesGUID.Add(thatPlayer.PlayerUID);
					serverAPI.SendMessage(thatPlayer, 0, (thisPlayer.PlayerName + LangUtility.Set("command-choices-invite", thisKingdom.KingdomNAME)), EnumChatType.OwnMessage);
					return TextCommandResult.Success(LangUtility.Set("command-success-invite", fullargs));
				// Accept invites and requests.
				case "accept":
					if (!usingArgs) {
						return TextCommandResult.Success(GetAllInvites(callerID, canInvite));
					}
					// Accept request to join kingdom.
					if (thatPlayer != null && inKingdom && thisKingdom.RequestGUID.Contains(thatPlayer.PlayerUID)) {
						if (!canInvite) {
							return TextCommandResult.Error(LangUtility.Set("command-error-accept01", thatPlayer.PlayerName));
						}
						SwitchKingdom(thatPlayer as IServerPlayer, thisKingdom.KingdomGUID);
						return TextCommandResult.Success(LangUtility.Set("command-success-accept", thatPlayer.PlayerName));
					}
					// Accept invitation to join kingdom.
					if (thatKingdom != null && thatKingdom.InvitesGUID.Contains(thisPlayer.PlayerUID)) {
						SwitchKingdom(thisPlayer as IServerPlayer, thatKingdom.KingdomGUID);
						serverAPI.SendMessage(serverAPI.World.PlayerByUid(thatKingdom.LeadersGUID), 0, (thisPlayer.PlayerName + LangUtility.Set("command-choices-accept", thatKingdom.KingdomLONG)), EnumChatType.OwnMessage);
						return TextCommandResult.Success(LangUtility.Set("command-success-accept", thatPlayer.PlayerName));
					}
					return TextCommandResult.Error(LangUtility.Set("command-error-accept00", LangUtility.Get("entries-keyword-kingdom")));
				// Reject invites and requests.
				case "reject":
					if (!usingArgs) {
						return TextCommandResult.Success(GetAllInvites(callerID, canInvite));
					}
					// Reject request to join kingdom.
					if (thatPlayer != null && inKingdom && thisKingdom.RequestGUID.Contains(thatPlayer.PlayerUID)) {
						if (!canInvite) {
							return TextCommandResult.Error(LangUtility.Set("command-error-reject01", thatPlayer.PlayerName));
						}
						thisKingdom.RequestGUID.Remove(thatPlayer.PlayerUID);
						return TextCommandResult.Success(LangUtility.Set("command-success-reject", thatPlayer.PlayerName));
					}
					// Reject invitation to join kingdom.
					if (thatKingdom != null && thatKingdom.InvitesGUID.Contains(thisPlayer.PlayerUID)) {
						thatKingdom.InvitesGUID.Remove(callerID);
						serverAPI.SendMessage(serverAPI.World.PlayerByUid(thatKingdom.LeadersGUID), 0, (thisPlayer.PlayerName + LangUtility.Set("command-choices-reject", thatKingdom.KingdomLONG)), EnumChatType.OwnMessage);
						return TextCommandResult.Success(LangUtility.Set("command-success-reject", thatPlayer.PlayerName));
					}
					return TextCommandResult.Error(LangUtility.Set("command-error-reject00", LangUtility.Get("entries-keyword-kingdom")));
				// Removes player from Kingdom.
				case "remove":
					if (thatPlayer == null) {
						return TextCommandResult.Error(LangUtility.Set("command-error-remove00", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (thisKingdom.PlayersGUID.Contains(thatPlayer.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-remove01", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (!adminPass && DataUtility.GetLeadersGUID(null, callerID) != callerID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-remove02", LangUtility.Get("entries-keyword-kingdom")));
					}
					return TextCommandResult.Success(LangUtility.Set("command-success-remove", fullargs));
				// Requests access to Leader.
				case "become":
					if (!DataUtility.KingdomExists(null, fullargs)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-become00", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (DataUtility.GetKingdom(null, fullargs).PlayersGUID.Contains(thisPlayer.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-become01", LangUtility.Get("entries-keyword-kingdom")));
					}
					/** TODO: ADD REQUEST TO JOIN TO QUERY thatKingdom.RequestGUID **/
					return TextCommandResult.Success(LangUtility.Set("command-success-become", fullargs));
				// Leaves current Kingdom.
				case "depart":
					if (!thisPlayer.Entity.HasBehavior<EntityBehaviorLoyalties>()) {
						return TextCommandResult.Error(LangUtility.Set("command-error-depart00", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.Set("command-error-depart01", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (serverAPI.World.Claims.All.Any(landClaim => landClaim.OwnedByPlayerUid == thisPlayer.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-depart02", LangUtility.Get("entries-keyword-kingdom")));
					}
					string kingdomName = thisKingdom.KingdomNAME;
					SwitchKingdom(thisPlayer as IServerPlayer, "00000000");
					return TextCommandResult.Success(LangUtility.Set("command-success-depart", LangUtility.Fix(kingdomName)));
				// Rename the Kingdom.
				case "rename":
					if (!usingArgs) {
						return TextCommandResult.Error("No argument. Provide a kingdom name.");
					}
					if (!inKingdom) {
						return TextCommandResult.Error("You are not part of a kingdom.");
					}
					if (DataUtility.GetLeadersGUID(null, callerID) != callerID) {
						return TextCommandResult.Error("You are not the leader of your kingdom.");
					}
					string oldName = thisKingdom.KingdomNAME;
					string newName = KingUtility.CorrectedNAME(fullargs);
					thisKingdom.KingdomNAME = newName;
					thisKingdom.KingdomLONG = newName;
					return TextCommandResult.Success(LangUtility.Set("command-success-rename", oldName + " to " + newName));
				// Declares war on Kingdom.
				case "attack":
					if (!DataUtility.IsKingdom(null, fullargs)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-attack00", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (thisKingdom is null || thisKingdom.KingdomGUID == "00000000") {
						return TextCommandResult.Error(LangUtility.Set("command-error-attack01", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (DataUtility.GetLeadersGUID(thisKingdom.KingdomGUID, callerID) != callerID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-attack02", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (thisKingdom.CurrentWars.Contains(thatKingdom.KingdomGUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-attack03", LangUtility.Get("entries-keyword-kingdom")));
					}
					SetWarKingdom(thisKingdom.KingdomGUID, thatKingdom.KingdomGUID);
					return TextCommandResult.Success(LangUtility.Set("command-success-attack", LangUtility.Fix(fullargs)));
				// Sets entity to Kingdom.
				case "endwar":
					if (!DataUtility.IsKingdom(null, fullargs)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-endwar00", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (thisKingdom is null || thisKingdom.KingdomGUID == "00000000") {
						return TextCommandResult.Error(LangUtility.Set("command-error-endwar01", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (DataUtility.GetLeadersGUID(thisKingdom.KingdomGUID, callerID) != callerID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-endwar02", LangUtility.Get("entries-keyword-kingdom")));
					}
					if (!thisKingdom.CurrentWars.Contains(thatKingdom.KingdomGUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-endwar03", LangUtility.Get("entries-keyword-kingdom")));
					}
					EndWarKingdom(thisKingdom.KingdomGUID, thatKingdom.KingdomGUID);
					return TextCommandResult.Success(LangUtility.Set("command-success-endwar", LangUtility.Fix(fullargs)));
				// Sets entity to Kingdom.
				case "setent":
					if (thisPlayer.CurrentEntitySelection is null) {
						return TextCommandResult.Error("Nothing selected, please look at an entity.");
					}
					if (!thisPlayer.CurrentEntitySelection.Entity.HasBehavior<EntityBehaviorLoyalties>()) {
						return TextCommandResult.Error("Entity does not have Loyalties behavior.");
					}
					if (fullargs is null) {
						return TextCommandResult.Error("No argument. Provide a kingdom name.");
					}
					thisPlayer.CurrentEntitySelection.Entity.GetBehavior<EntityBehaviorLoyalties>().SetKingdom(DataUtility.GetKingdomGUID(fullargs));
					return TextCommandResult.Success("Entity's kingdom set to: " + fullargs + "!");
				// Fetch all stored Kingdom data.
				case "getall":
					if (thatKingdom != null && thatKingdom.KingdomGUID != "00000000") {
						return TextCommandResult.Success(KingUtility.ListedAllData(thatKingdom.KingdomGUID));
					}
					if (thisKingdom != null && thisKingdom.KingdomGUID != "00000000") {
						return TextCommandResult.Success(KingUtility.ListedAllData(thisKingdom.KingdomGUID));
					}
					return TextCommandResult.Error(LangUtility.Set("command-error-getall00", LangUtility.Get("entries-keyword-kingdom")));
			}
			return TextCommandResult.Error(LangUtility.Get("command-help-kingdom"));
		}

		private TextCommandResult OnCultureCommand(TextCommandCallingArgs args) {
			string[] arguments = ((string)args[1]).Split(' ');
			string fullargs = (string)args[1];
			string callerID = args.Caller.Player.PlayerUID;
			IPlayer caller = args.Caller.Player;
			Culture thisCulture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == caller.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("culture_guid")) ?? null;
			// Determine privillege role level and if they are allowed to make new kingdoms/cultures.
			bool inCulture = thisCulture != null && thisCulture.CultureGUID != "00000000";
			bool usingArgs = fullargs != null && fullargs != "" && fullargs != " ";
			bool adminPass = args.Caller.HasPrivilege(Privilege.controlserver) || caller.PlayerName == "BadRabbit49";
			bool canCreate = args.Caller.GetRole(serverAPI).PrivilegeLevel >= serverAPI.World.Config.GetInt("MinCreateLevel", -1);
			bool maxCreate = serverAPI.World.Config.GetInt("MaxNewCultures", -1) != -1 && serverAPI.World.Config.GetInt("MaxNewCultures", -1) < (kingdomList.Count + 1);
			bool hoursTime = (serverAPI.World.Calendar.TotalHours - thisCulture.FoundedHOUR) > (serverAPI.World.Calendar.TotalHours - serverAPI.World.Config.GetInt("MinCultureMake"));
			switch ((string)args[0]) {
				// Creates brand new Culture.
				case "create":
					if (!usingArgs) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create00", LangUtility.Get("entries-keyword-culture")));
					}
					if (cultureList.Exists(cultureMatch => cultureMatch.CultureNAME.ToLowerInvariant() == string.Join(" ", arguments).ToLowerInvariant())) {
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
					CreateCulture(null, string.Join(" ", arguments).UcFirst(), callerID, !inCulture);
					caller.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/writing"), caller.Entity);
					return TextCommandResult.Success(LangUtility.Set("command-success-create", LangUtility.Fix(arguments[0].ToLower())));
				// Edits existing culture.
				case "update":
					if (thisCulture == null) {
						return TextCommandResult.Error(LangUtility.Set("command-error-update00", LangUtility.Get("entries-keyword-culture")));
					}
					if (!adminPass && thisCulture.FounderGUID != callerID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-update01", LangUtility.Get("entries-keyword-culture")));
					}
					caller.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/writing"), caller.Entity);
					string results = ChangeCulture(thisCulture.CultureGUID, arguments[0]?.ToLower() ?? "", arguments[1]?.ToLower() ?? "", string.Join(" ", arguments.Skip(2)) ?? "");
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
		public bool invitePlayers;
	}
}