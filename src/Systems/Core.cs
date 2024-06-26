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
			string[] kingdomCommands = { "create", "delete", "update", "invite", "become", "depart", "rename", "setent", "attack", "treaty", "getall", "setgov" };
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
			newKingdom.KingdomGUID = GuidUtility.RandomizeGUID(newKingdomGUID, 8, DataUtility.GetKingdomGUIDs());
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
			newCulture.CultureGUID = GuidUtility.RandomizeGUID(newCultureGUID, 8, DataUtility.GetCultureGUIDs());
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
			Kingdom kingdom = DataUtility.GetKingdom(kingdomGUID);
			foreach (string member in DataUtility.GetOnlinesGUIDs(kingdom.KingdomGUID, null)) {
				serverAPI.World.PlayerByUid(member)?.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/deepbell"), serverAPI.World.PlayerByUid(member)?.Entity);
			}
			foreach (var entity in serverAPI.World.LoadedEntities.Values) {
				if (!entity.HasBehavior<EntityBehaviorLoyalties>()) {
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
			foreach (string member in DataUtility.GetOnlinesGUIDs(null, culture.CultureGUID)) {
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
			Kingdom oldKingdom = DataUtility.GetKingdom(caller.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid"));
			Kingdom newKingdom = DataUtility.GetKingdom(kingdomGUID);
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
				oldKingdom.LeadersGUID = KingUtility.MostSeniority(oldKingdom.KingdomGUID);
			}
			newKingdom.PlayersGUID.Add(caller.PlayerUID);
			newKingdom.PlayersINFO.Add(KingUtility.PlayerDetails(caller.PlayerUID, newKingdom.MembersROLE, specificROLE));
			newKingdom.EntitiesALL.Add(caller.Entity.EntityId);
			caller.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.SetString("kingdom_guid", kingdomGUID);
			SaveKingdom();
		}

		public void SwitchCulture(IServerPlayer caller, string cultureGUID) {
			Culture oldCulture = DataUtility.GetCulture(caller.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("culture_guid"));
			Culture newCulture = DataUtility.GetCulture(cultureGUID);
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
			kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID1).CurrentWars.Add(kingdomGUID2);
			kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID2).CurrentWars.Add(kingdomGUID1);
			SaveKingdom();
			string kingdomONE = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID1).KingdomNAME;
			string kingdomTWO = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID2).KingdomNAME;
			foreach (var player in serverAPI.World.AllOnlinePlayers) {
				serverAPI.SendMessage(player, 0, LangUtility.Get("command-message-attack").Replace("[ENTRY1]", kingdomONE).Replace("[ENTRY2]", kingdomTWO), EnumChatType.OwnMessage);
			}
		}

		public void EndWarKingdom(string kingdomGUID1, string kingdomGUID2) {
			kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID1).CurrentWars.Remove(kingdomGUID2);
			kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID2).CurrentWars.Remove(kingdomGUID1);
			SaveKingdom();
			Kingdom kingdomONE = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID1);
			Kingdom kingdomTWO = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID2);
			foreach (var player in serverAPI.World.AllOnlinePlayers) {
				serverAPI.SendMessage(player, 0, LangUtility.Get("command-message-treaty").Replace("[ENTRY1]", kingdomONE.KingdomLONG).Replace("[ENTRY2]", kingdomTWO.KingdomLONG).UcFirst(), EnumChatType.OwnMessage);
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
		
		public string KingdomInvite(string playersGUID, bool getRequests) {
			string results;
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
			results = LangUtility.Get("command-success-invbox").Replace("[ENTRY1]", invites.Count.ToString()).Replace("[ENTRY2]", string.Join("\n", invites)) + "\n";
			if (getRequests) {
				return results + LangUtility.Get("command-success-reqbox").Replace("[ENTRY1]", playerNames.Length.ToString()).Replace("[ENTRY2]", string.Join("\n", playerNames));
			}
			return results;
		}

		public string CultureInvite(string playersGUID) {
			List<string> invites = new List<string>();
			foreach (Culture culture in cultureList) {
				if (culture.InviteGUID.Contains(playersGUID)) {
					invites.Add(culture.CultureNAME);
				}
			}
			return LangUtility.Get("command-success-invbox").Replace("[ENTRY1]", invites.Count.ToString()).Replace("[ENTRY2]", string.Join("\n", invites)) + "\n";
		}

		public string ChangeKingdom(string kingdomGUID, string subcomm, string subargs, string changes) {
			Kingdom kingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == kingdomGUID);
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
			Kingdom thisKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thisPlayer.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid")) ?? null;
			Kingdom thatKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomNAME.ToLowerInvariant() == fullargs?.ToLowerInvariant()) ?? null;
			// Determine privillege role level and if they are allowed to make new kingdoms/cultures.
			bool inKingdom = thisKingdom != null && thisKingdom.KingdomGUID != "00000000";
			bool usingArgs = fullargs != null && fullargs != "" && fullargs != " ";
			bool canInvite = inKingdom && KingUtility.GetRolesPRIV(thisKingdom.MembersROLE, KingUtility.GetMemberROLE(thisKingdom.PlayersINFO, callerID))[5];
			bool adminPass = args.Caller.HasPrivilege(Privilege.controlserver) || thisPlayer.PlayerName == "BadRabbit49";
			bool canCreate = args.Caller.GetRole(serverAPI).PrivilegeLevel >= serverAPI.World.Config.GetInt("MinCreateLevel", -1);
			bool maxCreate = serverAPI.World.Config.GetInt("MaxNewKingdoms", -1) != -1 || serverAPI.World.Config.GetInt("MaxNewKingdoms", -1) < (kingdomList.Count + 1);

			if (adminPass && inKingdom) {
				try { serverAPI.SendMessage(thisPlayer, 0, KingUtility.ListedAllData(thisKingdom.KingdomGUID), EnumChatType.OwnMessage); } catch { }
			}

			switch ((string)args[0]) {
				// Creates new owned Kingdom.
				case "create":
					if (!usingArgs) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create00", LangUtility.Get("entries-keyword-kingdom")));
					} else if (DataUtility.NameTaken(fullargs)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create01", LangUtility.Get("entries-keyword-kingdom")));
					} else if (DataUtility.InKingdom(callerID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create02", LangUtility.Get("entries-keyword-kingdom")));
					} else if (!canCreate) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create03", LangUtility.Get("entries-keyword-kingdom")));
					} else if (!maxCreate) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create04", LangUtility.Get("entries-keyword-kingdom")));
					}
					CreateKingdom(null, fullargs, thisPlayer.PlayerUID, !inKingdom);
					return TextCommandResult.Success(LangUtility.Set("command-success-create", fullargs));
				// Deletes the owned Kingdom.
				case "delete":
					if (!usingArgs || thatKingdom is null) {
						return TextCommandResult.Error(LangUtility.Set("command-error-delete00", LangUtility.Get("entries-keyword-kingdom")));
					} else if (!adminPass && thisKingdom.LeadersGUID != thisPlayer.PlayerUID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-delete01", LangUtility.Get("entries-keyword-kingdom")));
					}
					DeleteKingdom(thatKingdom.KingdomGUID);
					thisPlayer.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/deepbell"), thisPlayer.Entity);
					return TextCommandResult.Success(LangUtility.Set("command-success-delete", fullargs));
				// Updates and changes kingdom properties.
				case "update":
					if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.Set("command-error-update00", LangUtility.Get("entries-keyword-kingdom")));
					} else if (!adminPass && thisKingdom.LeadersGUID != callerID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-update01", LangUtility.Get("entries-keyword-kingdom")));
					}
					thisPlayer.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/writing"), thisPlayer.Entity);
					string[] fullset = { fullargs };
					try { fullset = fullargs.Split(' '); } catch { }
					string results = ChangeKingdom(thisKingdom.KingdomGUID, fullset[0], fullset[1], string.Join(' ', fullset.Skip(2)));
					return TextCommandResult.Success(results);
				// Invites player to join Kingdom.
				case "invite":
					/** TODO: THIS DOESN'T WANT TO PROPERLY SEND INVITES, DETERMINE IF THEY ARE GETTING THROUGH AND BEING SAVED. **/
					if (!usingArgs && canInvite && thisPlayer.CurrentEntitySelection.Entity is EntityPlayer playerEnt) {
						thisKingdom.InvitesGUID.Add(playerEnt.PlayerUID);
						SaveKingdom();
						serverAPI.SendMessage(thatPlayer, 0, LangUtility.Get("command-message-invite").Replace("[ENTRY1]", thisPlayer.PlayerName).Replace("[ENTRY2]", thisKingdom.KingdomNAME), EnumChatType.OwnMessage);
						return TextCommandResult.Success(LangUtility.Set("command-success-invite", playerEnt.Player.PlayerName));
					} else if (thatPlayer == null || !serverAPI.World.AllOnlinePlayers.Contains(thatPlayer)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-invite00", LangUtility.Get("entries-keyword-kingdom")));
					} else if (thisKingdom.PlayersGUID.Contains(thatPlayer.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-invite01", LangUtility.Get("entries-keyword-kingdom")));
					} else if (!canInvite) {
						return TextCommandResult.Error(LangUtility.Set("command-error-invite02", LangUtility.Get("entries-keyword-kingdom")));
					}
					thisKingdom.InvitesGUID.Add(thatPlayer.PlayerUID);
					SaveKingdom();
					serverAPI.SendMessage(thatPlayer, 0, LangUtility.Get("command-message-invite").Replace("[ENTRY1]", thisPlayer.PlayerName).Replace("[ENTRY2]", thisKingdom.KingdomNAME), EnumChatType.OwnMessage);
					return TextCommandResult.Success(LangUtility.Set("command-success-invite", fullargs));
				// Accept invites and requests.
				case "accept":
					if (!usingArgs) {
						return TextCommandResult.Success(KingdomInvite(callerID, canInvite));
					} else if (thatPlayer != null && inKingdom && thisKingdom.RequestGUID.Contains(thatPlayer.PlayerUID)) {
						if (!canInvite) {
							return TextCommandResult.Error(LangUtility.Set("command-error-accept01", thatPlayer.PlayerName));
						}
						SwitchKingdom(thatPlayer as IServerPlayer, thisKingdom.KingdomGUID);
						return TextCommandResult.Success(LangUtility.Set("command-success-accept", thatPlayer.PlayerName));
					} else if (thatKingdom != null && thatKingdom.InvitesGUID.Contains(thisPlayer.PlayerUID)) {
						SwitchKingdom(thisPlayer as IServerPlayer, thatKingdom.KingdomGUID);
						serverAPI.SendMessage(serverAPI.World.PlayerByUid(thatKingdom.LeadersGUID), 0, LangUtility.Get("command-message-accept").Replace("[ENTRY1]", thisPlayer.PlayerName).Replace("[ENTRY2]", thatKingdom.KingdomLONG), EnumChatType.OwnMessage);
						return TextCommandResult.Success(LangUtility.Set("command-success-accept", thatPlayer.PlayerName));
					}
					return TextCommandResult.Error(LangUtility.Set("command-error-accept00", LangUtility.Get("entries-keyword-kingdom")));
				// Reject invites and requests.
				case "reject":
					if (!usingArgs) {
						return TextCommandResult.Success(KingdomInvite(callerID, canInvite));
					} else if (thatPlayer != null && inKingdom && thisKingdom.RequestGUID.Contains(thatPlayer.PlayerUID)) {
						if (!canInvite) {
							return TextCommandResult.Error(LangUtility.Set("command-error-reject01", thatPlayer.PlayerName));
						}
						thisKingdom.RequestGUID.Remove(thatPlayer.PlayerUID);
						return TextCommandResult.Success(LangUtility.Set("command-success-reject", thatPlayer.PlayerName));
					} else if (thatKingdom != null && thatKingdom.InvitesGUID.Contains(thisPlayer.PlayerUID)) {
						thatKingdom.InvitesGUID.Remove(callerID);
						serverAPI.SendMessage(serverAPI.World.PlayerByUid(thatKingdom.LeadersGUID), 0, (thisPlayer.PlayerName + LangUtility.Set("command-choices-reject", thatKingdom.KingdomLONG)), EnumChatType.OwnMessage);
						return TextCommandResult.Success(LangUtility.Set("command-success-reject", thatPlayer.PlayerName));
					}
					return TextCommandResult.Error(LangUtility.Set("command-error-reject00", LangUtility.Get("entries-keyword-kingdom")));
				// Removes player from Kingdom.
				case "remove":
					if (thatPlayer == null) {
						return TextCommandResult.Error(LangUtility.Set("command-error-remove00", LangUtility.Get("entries-keyword-kingdom")));
					} else if (!thisKingdom.PlayersGUID.Contains(thatPlayer.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-remove01", LangUtility.Get("entries-keyword-kingdom")));
					} else if (!adminPass && DataUtility.GetLeadersGUID(null, callerID) != callerID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-remove02", LangUtility.Get("entries-keyword-kingdom")));
					} else {
						/** TODO: ADD SPECIAL CIRCUMSTANCE BASED ON PRIVILEGE AND ELECTIONS **/
						thisKingdom.PlayersINFO.Remove(KingUtility.GetMemberINFO(thisKingdom.PlayersINFO, thatPlayer.PlayerUID));
						thisKingdom.PlayersGUID.Remove(thatPlayer.PlayerUID);
						SaveKingdom();
						return TextCommandResult.Success(LangUtility.Set("command-success-remove", fullargs));
					}
				// Requests access to Leader.
				case "become":
					if (!DataUtility.KingdomExists(null, fullargs)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-become00", LangUtility.Get("entries-keyword-kingdom")));
					} else if (DataUtility.GetKingdom(null, fullargs).PlayersGUID.Contains(thisPlayer.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-become01", LangUtility.Get("entries-keyword-kingdom")));
					} else {
						/** TODO: ADD REQUEST TO JOIN TO QUERY thatKingdom.RequestGUID **/
						thatKingdom.RequestGUID.Add(thisPlayer.PlayerUID);
						SaveKingdom();
						serverAPI.SendMessage(serverAPI.World.PlayerByUid(thatKingdom.LeadersGUID), 0, LangUtility.Get("command-message-invite").Replace("[ENTRY1]", thisPlayer.PlayerName).Replace("[ENTRY2]", thisKingdom.KingdomNAME), EnumChatType.OwnMessage);
						return TextCommandResult.Success(LangUtility.Set("command-success-become", fullargs));
					}
				// Leaves current Kingdom.
				case "depart":
					if (!thisPlayer.Entity.HasBehavior<EntityBehaviorLoyalties>()) {
						return TextCommandResult.Error(LangUtility.Set("command-error-depart00", LangUtility.Get("entries-keyword-kingdom")));
					} else if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.Set("command-error-depart01", LangUtility.Get("entries-keyword-kingdom")));
					} else if (serverAPI.World.Claims.All.Any(landClaim => landClaim.OwnedByPlayerUid == thisPlayer.PlayerUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-depart02", LangUtility.Get("entries-keyword-kingdom")));
					} else {
						string kingdomName = thisKingdom.KingdomNAME;
						SwitchKingdom(thisPlayer as IServerPlayer, "00000000");
						return TextCommandResult.Success(LangUtility.Set("command-success-depart", kingdomName));
					}
				// Declares war on Kingdom.
				case "attack":
					if (!DataUtility.IsKingdom(null, fullargs)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-attack00", LangUtility.Get("entries-keyword-kingdom")));
					} else if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.Set("command-error-attack01", LangUtility.Get("entries-keyword-kingdom")));
					} else if (DataUtility.GetLeadersGUID(thisKingdom.KingdomGUID, callerID) != callerID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-attack02", LangUtility.Get("entries-keyword-kingdom")));
					} else if (thisKingdom.CurrentWars.Contains(thatKingdom.KingdomGUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-attack03", LangUtility.Get("entries-keyword-kingdom")));
					} else {
						SetWarKingdom(thisKingdom.KingdomGUID, thatKingdom.KingdomGUID);
						return TextCommandResult.Success(LangUtility.Set("command-success-attack", fullargs));
					}
				// Sets entity to Kingdom.
				case "treaty":
					if (!DataUtility.IsKingdom(null, fullargs)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-treaty00", LangUtility.Get("entries-keyword-kingdom")));
					} else if (!inKingdom) {
						return TextCommandResult.Error(LangUtility.Set("command-error-treaty01", LangUtility.Get("entries-keyword-kingdom")));
					} else if (DataUtility.GetLeadersGUID(thisKingdom.KingdomGUID, callerID) != callerID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-treaty02", LangUtility.Get("entries-keyword-kingdom")));
					} else if (!thisKingdom.CurrentWars.Contains(thatKingdom.KingdomGUID)) {
						return TextCommandResult.Error(LangUtility.Set("command-error-treaty03", LangUtility.Get("entries-keyword-kingdom")));
					}
					EndWarKingdom(thisKingdom.KingdomGUID, thatKingdom.KingdomGUID);
					return TextCommandResult.Success(LangUtility.Set("command-success-treaty", fullargs));
				// Fetch all stored Kingdom data.
				case "getall":
					if (thatKingdom != null && thatKingdom.KingdomGUID != "00000000") {
						return TextCommandResult.Success(KingUtility.ListedAllData(thatKingdom.KingdomGUID));
					} else if (thisKingdom != null && thisKingdom.KingdomGUID != "00000000") {
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
			IPlayer thisPlayer = args.Caller.Player;
			IPlayer thatPlayer = serverAPI.World.PlayerByUid(serverAPI.PlayerData.GetPlayerDataByLastKnownName(fullargs)?.PlayerUID) ?? null;
			Culture thisCulture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == thisPlayer.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("culture_guid")) ?? null;
			Culture thatCulture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == arguments[0]) ?? null;
			// Determine privillege role level and if they are allowed to make new kingdoms/cultures.
			bool inCulture = thisCulture != null && thisCulture.CultureGUID != "00000000";
			bool usingArgs = fullargs != null && fullargs != "" && fullargs != " ";
			bool adminPass = args.Caller.HasPrivilege(Privilege.controlserver) || thisPlayer.PlayerName == "BadRabbit49";
			bool canCreate = args.Caller.GetRole(serverAPI).PrivilegeLevel >= serverAPI.World.Config.GetInt("MinCreateLevel", -1);
			bool maxCreate = serverAPI.World.Config.GetInt("MaxNewCultures", -1) != -1 && serverAPI.World.Config.GetInt("MaxNewCultures", -1) < (kingdomList.Count + 1);
			bool hoursTime = (serverAPI.World.Calendar.TotalHours - thisCulture.FoundedHOUR) > (serverAPI.World.Calendar.TotalHours - serverAPI.World.Config.GetInt("MinCultureMake"));
			switch ((string)args[0]) {
				// Creates brand new Culture.
				case "create":
					if (!usingArgs) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create00", LangUtility.Get("entries-keyword-culture")));
					} else if (cultureList.Exists(cultureMatch => cultureMatch.CultureNAME.ToLowerInvariant() == string.Join(" ", arguments).ToLowerInvariant())) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create01", LangUtility.Get("entries-keyword-culture")));
					} else if (!canCreate && !adminPass) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create03", LangUtility.Get("entries-keyword-culture")));
					} else if (!maxCreate && !adminPass) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create04", LangUtility.Get("entries-keyword-culture")));
					} else if (!hoursTime && !adminPass) {
						return TextCommandResult.Error(LangUtility.Set("command-error-create05", LangUtility.Get("entries-keyword-culture")));
					}
					CreateCulture(null, string.Join(" ", arguments).UcFirst(), callerID, !inCulture);
					return TextCommandResult.Success(LangUtility.Set("command-success-create", arguments[0]));
				// Deletes existing culture.
				case "delete":
					if (!cultureList.Exists(cultureMatch => cultureMatch.CultureNAME.ToLowerInvariant() == string.Join(" ", arguments).ToLowerInvariant())) {
						return TextCommandResult.Error(LangUtility.Set("command-error-delete00", LangUtility.Get("entries-keyword-culture")));
					} else if (!adminPass) {
						return TextCommandResult.Error(LangUtility.Set("command-error-delete01", LangUtility.Get("entries-keyword-culture")));
					}
					DeleteCulture(thatCulture.CultureGUID);
					return TextCommandResult.Success(LangUtility.Set("command-success-delete", arguments[0]));
				// Edits existing culture.
				case "update":
					if (!inCulture) {
						return TextCommandResult.Error(LangUtility.Set("command-error-update00", LangUtility.Get("entries-keyword-culture")));
					} else if (!adminPass && thisCulture.FounderGUID != callerID) {
						return TextCommandResult.Error(LangUtility.Set("command-error-update01", LangUtility.Get("entries-keyword-culture")));
					}
					thisPlayer.Entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/writing"), thisPlayer.Entity);
					string results = ChangeCulture(thisCulture.CultureGUID, arguments[0]?.ToLower() ?? "", arguments[1]?.ToLower() ?? "", string.Join(" ", arguments.Skip(2)) ?? "");
					return TextCommandResult.Success(results);
				// Invite a player into culture.
				case "invite":
					if (!usingArgs && thisPlayer.CurrentEntitySelection.Entity is EntityPlayer) {
						thatPlayer = (thisPlayer.CurrentEntitySelection.Entity as EntityPlayer).Player;
						thisCulture.InviteGUID.Add(thatPlayer.PlayerUID);
						SaveCulture();
						return TextCommandResult.Success(LangUtility.Set("command-success-invite", thatPlayer.PlayerName));
					} else if (!usingArgs) {
						return TextCommandResult.Error(LangUtility.Set("command-error-invite00", LangUtility.Get("entries-keyword-culture")));
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
					return TextCommandResult.Error(LangUtility.Set("command-error-accept00", LangUtility.Get("entries-keyword-culture")));
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
					return TextCommandResult.Error(LangUtility.Set("command-error-reject00", LangUtility.Get("entries-keyword-culture")));
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
		public bool invites;
	}
}