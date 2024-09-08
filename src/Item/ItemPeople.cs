using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using static VSKingdom.Utilities.GenericUtil;
using static VSKingdom.Utilities.ReadingUtil;

namespace VSKingdom {
	public class ItemPeople : Item {
		public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity) {
			return null;
		}

		public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling) {
			if (blockSel is null) {
				return;
			}
			IPlayer player = byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID);
			if (!byEntity.World.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak)) {
				return;
			}
			if (!(byEntity is EntityPlayer) || player.WorldData.CurrentGameMode != EnumGameMode.Creative) {
				slot.TakeOut(1);
				slot.MarkDirty();
			}
			AssetLocation assetLocation = new AssetLocation(Code.Domain, CodeEndWithoutParts(1));
			EntityProperties properties = byEntity.World.GetEntityType(assetLocation);
			if (properties is null) {
				byEntity.World.Logger.Error("ItemPeople: No such entity - {0}", assetLocation);
				if (api.World.Side == EnumAppSide.Client) {
					(api as ICoreClientAPI)?.TriggerIngameError(this, "nosuchentity", $"No such entity loaded - '{assetLocation}'.");
				}
				return;
			}
			if (api.World.Side == EnumAppSide.Client) {
				byEntity.World.Logger.Notification("Creating a new entity with code: " + CodeEndWithoutParts(1));
			}
			Entity entity = byEntity.World.ClassRegistry.CreateEntity(properties);
			EntitySentry sentry = entity as EntitySentry;
			if (entity is null) {
				return;
			}
			entity.ServerPos.X = (float)(blockSel.Position.X + ((!blockSel.DidOffset) ? blockSel.Face.Normali.X : 0)) + 0.5f;
			entity.ServerPos.Y = blockSel.Position.Y + ((!blockSel.DidOffset) ? blockSel.Face.Normali.Y : 0);
			entity.ServerPos.Z = (float)(blockSel.Position.Z + ((!blockSel.DidOffset) ? blockSel.Face.Normali.Z : 0)) + 0.5f;
			entity.ServerPos.Yaw = byEntity.Pos.Yaw + MathF.PI + MathF.PI / 2f;
			entity.Pos.SetFrom(entity.ServerPos);
			entity.PositionBeforeFalling.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
			entity.Attributes.SetString("origin", "playerplaced");
			JsonObject attributes = Attributes;
			if (attributes != null && attributes.IsTrue("setGuardedEntityAttribute")) {
				entity.WatchedAttributes.SetLong("guardedEntityId", byEntity.EntityId);
			}
			if (!player.Entity.WatchedAttributes.HasAttribute("followerEntityUids")) {
				player.Entity.WatchedAttributes.SetAttribute("followerEntityUids", new LongArrayAttribute(new long[] { }));
			}
			if (api.Side == EnumAppSide.Server) {
				// Change variabels depending on if the entity is a bandit or not. Bandits have no leaders.
				bool isBandit = properties.Attributes["baseSides"].AsString(commonerGUID) == banditryGUID;
				bool isCrouch = player.Entity.ServerControls.Sneak;
				string _entityClass = properties.Attributes["baseClass"].AsString("melee").ToLower();
				string _entityState = isBandit ? EnlistedStatus.DESERTER.ToString() : EnlistedStatus.CIVILIAN.ToString();
				string firstName = null;
				string famlyName = null;
				string _kingdomGuid = isBandit ? banditryGUID : byEntity.WatchedAttributes.GetString("kingdomGUID", commonerGUID);
				string _cultureGuid = isBandit && isCrouch ? seraphimGUID : byEntity.WatchedAttributes.GetString("cultureGUID", seraphimGUID);
				string _leadersGuid = isBandit ? null : player.PlayerUID;
				string _kingdomName = null;
				string _cultureName = null;
				string _leadersName = _leadersGuid != null ? player.PlayerName : null;
				double _outpostSize = 6;
				BlockPos _outpostXyzd = entity.ServerPos.AsBlockPos;
				string[] _kingdomCOLOURS = new string[] { "#ffffff", "#ffffff", "#ffffff" };
				string[] mascNames = Open(api.World.Config.GetAsString("BasicMascNames"));
				string[] femmNames = Open(api.World.Config.GetAsString("BasicFemmNames"));
				string[] lastNames = Open(api.World.Config.GetAsString("BasicLastNames"));
				string[] _kingdomENEMIES = new string[] { };
				string[] _kingdomFRIENDS = new string[] { };
				string[] _kingdomOUTLAWS = new string[] { };

				// Grab culture data from server file.
				byte[] cultureData = (byEntity.Api as ICoreServerAPI)?.WorldManager.SaveGame.GetData("cultureData");
				List<Culture> cultureList = cultureData is null ? new List<Culture>() : SerializerUtil.Deserialize<List<Culture>>(cultureData);
				Culture byCulture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == _cultureGuid);

				if (byCulture != null) {
					_cultureName = byCulture.CultureNAME;
					mascNames = byCulture.MFirstNames.Count > 0 ? byCulture.MFirstNames.ToArray() : mascNames;
					femmNames = byCulture.FFirstNames.Count > 0 ? byCulture.FFirstNames.ToArray() : femmNames;
					lastNames = byCulture.FamilyNames.Count > 0 ? byCulture.FamilyNames.ToArray() : lastNames;
				}

				// Grab kingdom data from server file.
				byte[] kingdomData = (byEntity.Api as ICoreServerAPI)?.WorldManager.SaveGame.GetData("kingdomData");
				List<Kingdom> kingdomList = kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);
				Kingdom byKingdom = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == _kingdomGuid);

				if (byKingdom != null) {
					_kingdomName = byKingdom.KingdomNAME;
					_kingdomCOLOURS = new string[3] { byKingdom.KingdomHEXA, byKingdom.KingdomHEXB, byKingdom.KingdomHEXC };
					_kingdomENEMIES = byKingdom.EnemiesGUID.ToArray();
					_kingdomFRIENDS = byKingdom.FriendsGUID.ToArray();
					_kingdomOUTLAWS = byKingdom.OutlawsGUID.ToArray();
				}

				// Setup skins stuff!
				if (byCulture != null && byCulture?.CultureGUID != seraphimGUID) {
					// Editing the "skinConfig" tree here and changing it to what we want.
					var entitySkinParts = entity.WatchedAttributes.GetOrAddTreeAttribute("skinConfig").GetOrAddTreeAttribute("appliedParts");
					string[] skinColors = byCulture.SkinColours.ToArray<string>();
					string[] eyesColors = byCulture.EyesColours.ToArray<string>();
					string[] hairColors = byCulture.HairColours.ToArray<string>();
					string[] hairStyles = byCulture.HairsStyles.ToArray<string>();
					string[] hairExtras = byCulture.HairsExtras.ToArray<string>();
					string[] faceStyles = byCulture.FacesStyles.ToArray<string>();
					string[] faceBeards = byCulture.FacesBeards.ToArray<string>();
					string[] underwears = new string[] { "breeches", "leotard", "twopiece" };
					string[] expression = new string[] { "angry", "grin", "smirk", "kind", "upset", "neutral", "sad", "serious", "tired", "very-sad" };
					entitySkinParts.SetString("baseskin", skinColors[api.World.Rand.Next(0, skinColors.Length - 1)]);
					entitySkinParts.SetString("eyecolor", eyesColors[api.World.Rand.Next(0, eyesColors.Length - 1)]);
					entitySkinParts.SetString("haircolor", hairColors[api.World.Rand.Next(0, hairColors.Length - 1)]);
					entitySkinParts.SetString("hairbase", hairStyles[api.World.Rand.Next(0, hairStyles.Length - 1)]);
					entitySkinParts.SetString("hairextra", hairExtras[api.World.Rand.Next(0, hairExtras.Length - 1)]);
					if (entity.Code.EndVariant() == "masc") {
						entitySkinParts.SetString("mustache", faceStyles[api.World.Rand.Next(0, faceStyles.Length - 1)]);
						entitySkinParts.SetString("beard", faceBeards[api.World.Rand.Next(0, faceBeards.Length - 1)]);
						entitySkinParts.SetString("underwear", underwears[api.World.Rand.Next(0, 1)]);
					} else if (entity.Code.EndVariant() == "femm") {
						entitySkinParts.SetString("mustache", "none");
						entitySkinParts.SetString("beard", "none");
						entitySkinParts.SetString("underwear", underwears[api.World.Rand.Next(1, 2)]);
					}
					entitySkinParts.SetString("facialexpression", expression[api.World.Rand.Next(1, expression.Length - 1)]);
				}

				// Setup outpost stuff!
				BlockEntityPost _outpost = null;
				if (_leadersGuid != null && api.World.BlockAccessor.GetBlock(blockSel.Position).EntityClass != null) {
					if (api.World.ClassRegistry.GetBlockEntity(api.World.BlockAccessor.GetBlock(blockSel.Position).EntityClass) == typeof(BlockEntityPost)) {
						_outpost = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityPost;
						if (_outpost.IsCapacity(entity.EntityId)) {
							(api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(_outpost.Pos.X, _outpost.Pos.Y, _outpost.Pos.Z, 7001, SerializerUtil.Serialize<long>(entity.EntityId));
							_outpostXyzd = blockSel.Position;
							_outpostSize = _outpost.areasize;
							_outpost.IgnitePost();
						}
					}
				}

				// Setup corpses stuff!
				entity.WatchedAttributes.SetString("deathAnimation", "dies");
				entity.WatchedAttributes.SetString("deathSkeletons", "humanoid1");

				// Setup nametag stuff!
				switch (entity.Code.EndVariant()) {
					case "masc": firstName = mascNames[api.World.Rand.Next(0, mascNames.Length - 1)]; break;
					case "femm": firstName = femmNames[api.World.Rand.Next(0, femmNames.Length - 1)]; break;
					default: firstName = mascNames[api.World.Rand.Next(0, mascNames.Length - 1)]; break;
				}
				famlyName = lastNames[api.World.Rand.Next(0, lastNames.Length - 1)];
				ITreeAttribute nametagTree = new TreeAttribute();
				nametagTree.SetString("name", firstName);
				nametagTree.SetString("last", famlyName);
				nametagTree.SetString("full", $"{firstName} {famlyName}");
				nametagTree.SetBool("showtagonlywhentargeted", api.World.Config.GetBool("HideAllNames", true));
				nametagTree.SetInt("renderRange", api.World.Config.GetInt("NameRenderDist", 500));
				entity.WatchedAttributes.SetAttribute("nametag", nametagTree);

				// Setup loyalty stuff!
				entity.WatchedAttributes.SetString("kingdomGUID", _kingdomGuid);
				entity.WatchedAttributes.SetString("cultureGUID", _cultureGuid);
				entity.WatchedAttributes.SetString("leadersGUID", _leadersGuid);

				// Setup outpost info!
				entity.WatchedAttributes.SetDouble("postRange", _outpostSize);
				entity.WatchedAttributes.SetBlockPos("postBlock", _outpostXyzd.Copy());

				// Setup orders stuff!
				entity.WatchedAttributes.SetBool("orderWander", true);
				entity.WatchedAttributes.SetBool("orderFollow", false);
				entity.WatchedAttributes.SetBool("orderEngage", true);
				entity.WatchedAttributes.SetBool("orderPursue", true);
				entity.WatchedAttributes.SetBool("orderShifts", false);
				entity.WatchedAttributes.SetBool("orderPatrol", false);
				entity.WatchedAttributes.SetBool("orderReturn", false);
				entity.WatchedAttributes.SetFloat("wanderRange", (float)_outpostSize);
				entity.WatchedAttributes.SetFloat("followRange", 2f);
				entity.WatchedAttributes.SetFloat("engageRange", 16f);
				entity.WatchedAttributes.SetFloat("pursueRange", 16f);
				entity.WatchedAttributes.SetFloat("shiftStarts", 0f);
				entity.WatchedAttributes.SetFloat("shiftEnding", 24f);
				entity.WatchedAttributes.SetVec3is("patrolVec3i", new Vec3i[] { new Vec3i(blockSel.Position) });

				// Wander:0 / Follow:1 / Engage:2 / Pursue:3 / Shifts:4 / Patrol:5 / Return:6 //
				sentry.ruleOrder = new bool[7] { true, false, true, true, false, false, false };

				// Setup sentry dataCache!
				sentry.cachedData = new SentryDataCache() {
					moveSpeed = properties.Attributes["moveSpeed"].AsFloat(0.030f),
					walkSpeed = properties.Attributes["walkSpeed"].AsFloat(0.015f),
					postRange = properties.Attributes["postRange"].AsFloat(6.0f),
					weapRange = properties.Attributes["weapRange"].AsFloat(1.5f),
					idleAnims = properties.Attributes["idleAnims"].AsString("idle").ToLower(),
					walkAnims = properties.Attributes["walkAnims"].AsString("walk").ToLower(),
					moveAnims = properties.Attributes["moveAnims"].AsString("move").ToLower(),
					duckAnims = properties.Attributes["duckAnims"].AsString("duck").ToLower(),
					swimAnims = properties.Attributes["swimAnims"].AsString("swim").ToLower(),
					jumpAnims = properties.Attributes["jumpAnims"].AsString("jump").ToLower(),
					postBlock = _outpostXyzd.ToVec3d(),
					kingdomGUID = _kingdomGuid,
					cultureGUID = _cultureGuid,
					leadersGUID = _leadersGuid,
					recruitINFO = _entityState,
					enemiesLIST = _kingdomENEMIES,
					friendsLIST = _kingdomFRIENDS,
					outlawsLIST = _kingdomOUTLAWS
				};
			}

			// SPAWNING ENTITY //
			byEntity.World.SpawnEntity(entity);

			for (int i = 0; i < dressCodes.Length; i++) {
				string code = dressCodes[i] + "Spawn";
				if (!properties.Attributes[code].Exists) { continue; }
				try {
					var item = api.World.GetItem(new AssetLocation(GetRandom(properties.Attributes[code].AsArray<string>(null))));
					ItemStack itemstack = new ItemStack(item, 1);
					if (i == 18 && sentry.GearInventory[16].Itemstack.Item is ItemBow) {
						itemstack = new ItemStack(item, GetRandom(item.MaxStackSize, 5));
					}
					var newstack = byEntity.World.SpawnItemEntity(itemstack, entity.ServerPos.XYZ) as EntityItem;
					sentry.GearInventory[i].Itemstack = newstack?.Itemstack;
					newstack.Die(EnumDespawnReason.PickedUp, null);
					sentry.GearInvSlotModified(i);
				} catch { }
			}

			handHandling = EnumHandHandling.PreventDefaultAction;
		}

		public override string GetHeldTpIdleAnimation(ItemSlot activeHotbarSlot, Entity byEntity, EnumHand hand) {
			EntityProperties entityType = byEntity.World.GetEntityType(new AssetLocation(Code.Domain, CodeEndWithoutParts(1)));
			if (entityType is null) {
				return base.GetHeldTpIdleAnimation(activeHotbarSlot, byEntity, hand);
			}
			if (Math.Max(entityType.CollisionBoxSize.X, entityType.CollisionBoxSize.Y) > 1f) {
				return "holdunderarm";
			}
			return "holdbothhands";
		}

		public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot) {
			return new WorldInteraction[1] { new WorldInteraction { ActionLangCode = "heldhelp-place", MouseButton = EnumMouseButton.Right } }.Append(base.GetHeldInteractionHelp(inSlot));
		}

		public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo) {
			base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
		}
	}
}