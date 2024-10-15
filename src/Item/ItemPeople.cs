using System;
using System.Linq;
using System.Text;
using System.Drawing;
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
using static VSKingdom.Utilities.ColoursUtil;
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
			ItemStack itemstack = slot.Itemstack;
			JsonObject attributes = Attributes;
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
			if (attributes != null && attributes.IsTrue("setGuardedEntityAttribute")) {
				entity.WatchedAttributes.SetLong("guardedEntityId", byEntity.EntityId);
			}
			if (!player.Entity.WatchedAttributes.HasAttribute("followerEntityUids")) {
				player.Entity.WatchedAttributes.SetAttribute("followerEntityUids", new LongArrayAttribute(new long[] { }));
			}

			bool inheritedParts = itemstack?.Attributes.HasAttribute("appliedParts") ?? false;
			bool inheritedNames = itemstack?.Attributes.HasAttribute("nametagParts") ?? false;
			bool inheritedGears = itemstack?.Attributes.HasAttribute("entInventory") ?? false;
			bool didTransitions = (inheritedParts || inheritedNames || inheritedGears) && itemstack.Attributes.HasAttribute("gender") && itemstack.Attributes.GetString("gender") != entity.Code.EndVariant();

			if (api.Side == EnumAppSide.Server) {
				// Change variabels depending on if the entity is a bandit or not. Bandits have no leaders.
				bool _isBandit = properties.Attributes["baseSides"].AsString(CommonersID) == BanditrysID;
				bool _isCrouch = player.Entity.ServerControls.Sneak;
				string _entityClass = properties.Attributes["baseClass"].AsString("melee").ToLower();
				string _entityState = _isBandit ? EnlistedStatus.DESERTER.ToString() : EnlistedStatus.CIVILIAN.ToString();
				string _firstName = null;
				string _famlyName = null;
				string _kingdomGuid = _isBandit ? BanditrysID : byEntity.WatchedAttributes.GetString("kingdomGUID", CommonersID);
				string _cultureGuid = _isBandit && _isCrouch ? SeraphimsID : byEntity.WatchedAttributes.GetString("cultureGUID", SeraphimsID);
				string _leadersGuid = _isBandit ? null : player.PlayerUID;
				string _kingdomName = null;
				string _cultureName = null;
				string _leadersName = _leadersGuid != null ? player.PlayerName : null;
				double _outpostSize = 6;
				BlockPos _outpostXyzd = entity.ServerPos.AsBlockPos;
				string[] _kingdomCOLOURS = new string[] { "#ffffff", "#ffffff", "#ffffff" };
				string[] _mascNames = Open(api.World.Config.GetAsString("BasicMascNames"));
				string[] _femmNames = Open(api.World.Config.GetAsString("BasicFemmNames"));
				string[] _lastNames = Open(api.World.Config.GetAsString("BasicLastNames"));
				string[] _kingdomENEMIES = new string[] { };
				string[] _kingdomFRIENDS = new string[] { };
				string[] _kingdomOUTLAWS = new string[] { };

				// Grab culture data from server file.
				byte[] cultureData = (byEntity.Api as ICoreServerAPI)?.WorldManager.SaveGame.GetData("cultureData");
				List<Culture> cultureList = cultureData is null ? new List<Culture>() : SerializerUtil.Deserialize<List<Culture>>(cultureData);
				Culture byCulture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == _cultureGuid);

				if (byCulture != null) {
					_cultureName = byCulture.CultureNAME;
					_mascNames = byCulture.MFirstNames.Count > 0 ? byCulture.MFirstNames.ToArray() : _mascNames;
					_femmNames = byCulture.FFirstNames.Count > 0 ? byCulture.FFirstNames.ToArray() : _femmNames;
					_lastNames = byCulture.FamilyNames.Count > 0 ? byCulture.FamilyNames.ToArray() : _lastNames;
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
				if (inheritedParts || (byCulture != null && byCulture?.CultureGUID != SeraphimsID)) {
					// Editing the "skinConfig" tree here and changing it to what we want.
					var entitySkinParts = entity.WatchedAttributes.GetOrAddTreeAttribute("skinConfig").GetOrAddTreeAttribute("appliedParts");
					var congentialParts = inheritedParts ? itemstack.Attributes.GetTreeAttribute("appliedParts") : null;
					string[] skinColors = byCulture.SkinColours.ToArray<string>();
					string[] eyesColors = byCulture.EyesColours.ToArray<string>();
					string[] hairColors = byCulture.HairColours.ToArray<string>();
					string[] hairStyles = byCulture.HairsStyles.ToArray<string>();
					string[] hairExtras = byCulture.HairsExtras.ToArray<string>();
					string[] faceStyles = byCulture.FacesStyles.ToArray<string>();
					string[] faceBeards = byCulture.FacesBeards.ToArray<string>();
					string[] underwears = new string[] { "breeches", "leotard", "twopiece" };
					string[] expression = new string[] { "angry", "grin", "smirk", "kind", "upset", "neutral", "sad", "serious", "tired", "very-sad" };
					entitySkinParts.SetString("baseskin", inheritedParts ? congentialParts.GetString("baseskin") : skinColors[api.World.Rand.Next(0, skinColors.Length - 1)]);
					entitySkinParts.SetString("eyecolor", inheritedParts ? congentialParts.GetString("eyecolor") : eyesColors[api.World.Rand.Next(0, eyesColors.Length - 1)]);
					entitySkinParts.SetString("haircolor", inheritedParts ? congentialParts.GetString("haircolor") : hairColors[api.World.Rand.Next(0, hairColors.Length - 1)]);
					entitySkinParts.SetString("hairbase", inheritedParts && !didTransitions ? congentialParts.GetString("hairbase") : hairStyles[api.World.Rand.Next(0, hairStyles.Length - 1)]);
					entitySkinParts.SetString("hairextra", inheritedParts && !didTransitions ? congentialParts.GetString("hairextra") : hairExtras[api.World.Rand.Next(0, hairExtras.Length - 1)]);
					if (entity.Code.EndVariant() == "masc") {
						entitySkinParts.SetString("mustache", inheritedParts && !didTransitions ? congentialParts.GetString("mustache") : faceStyles[api.World.Rand.Next(0, faceStyles.Length - 1)]);
						entitySkinParts.SetString("beard", inheritedParts && !didTransitions ? congentialParts.GetString("beard") : faceBeards[api.World.Rand.Next(0, faceBeards.Length - 1)]);
						entitySkinParts.SetString("underwear", inheritedParts && !didTransitions ? congentialParts.GetString("underwear") : underwears[api.World.Rand.Next(0, 1)]);
					} else if (entity.Code.EndVariant() == "femm") {
						entitySkinParts.SetString("mustache", "none");
						entitySkinParts.SetString("beard", "none");
						entitySkinParts.SetString("underwear", underwears[api.World.Rand.Next(1, 2)]);
					}
					entitySkinParts.SetString("facialexpression", inheritedParts ? congentialParts.GetString("facialexpression") : expression[api.World.Rand.Next(1, expression.Length - 1)]);
				}

				// Setup outpost stuff!
				BlockEntityPost _outpost = null;
				if (_leadersGuid != null && api.World.BlockAccessor.GetBlock(blockSel.Position).EntityClass != null) {
					if (api.World.ClassRegistry.GetBlockEntity(api.World.BlockAccessor.GetBlock(blockSel.Position).EntityClass) == typeof(BlockEntityPost)) {
						_outpost = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityPost;
						if (_outpost.IsCapacity(entity.EntityId)) {
							(api as ICoreServerAPI)?.Network.SendBlockEntityPacket((player as IServerPlayer), _outpost.Pos.X, _outpost.Pos.Y, _outpost.Pos.Z, 7001, SerializerUtil.Serialize<long>(entity.EntityId));
							(api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(_outpost.Pos.X, _outpost.Pos.Y, _outpost.Pos.Z, 7001, SerializerUtil.Serialize<long>(entity.EntityId));
							_outpostXyzd = blockSel.Position;
							_outpostSize = _outpost.areasize;
							_outpost.IgnitePost();
						}
					}
				}

				// Setup corpses stuff!
				entity.WatchedAttributes.SetString("deathAnimation", GetRandom(properties.Attributes["deathAnim"].AsArray<string>(new string[] { "dies" })));
				entity.WatchedAttributes.SetString("deathSkeletons", GetRandom(properties.Attributes["deathBone"].AsArray<string>(new string[] { "humanoid1" })));

				// Setup nametag stuff!
				var nametagTree = new TreeAttribute();
				var congentialNames = inheritedParts ? itemstack.Attributes.GetTreeAttribute("appliedNames") : null;
				switch (entity.Code.EndVariant()) {
					case "masc": _firstName = inheritedNames && !didTransitions ? congentialNames?.GetString("name") : _mascNames[api.World.Rand.Next(0, _mascNames.Length - 1)]; break;
					case "femm": _firstName = inheritedNames && !didTransitions ? congentialNames?.GetString("name") : _femmNames[api.World.Rand.Next(0, _femmNames.Length - 1)]; break;
					default: _firstName = inheritedNames ? congentialNames?.GetString("name") :GetRandom(_mascNames.Concat(_femmNames).ToArray()); break;
				}
				_famlyName = inheritedNames ? congentialNames?.GetString("last") : _lastNames[api.World.Rand.Next(0, _lastNames.Length - 1)];

				nametagTree.SetString("name", _firstName);
				nametagTree.SetString("last", _famlyName);
				nametagTree.SetString("full", $"{_firstName} {_famlyName}");
				nametagTree.SetBool("showtagonlywhentargeted", api.World.Config.GetAsBool(SentryTagOn, true));
				nametagTree.SetInt("renderRange", (int)api.World.Config.GetLong(RenderTagDs, 500));
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
					moveSpeed = properties.Attributes["moveSpeed"].AsFloat(0.040f),
					walkSpeed = properties.Attributes["walkSpeed"].AsFloat(0.020f),
					postRange = properties.Attributes["postRange"].AsFloat(6.0f),
					weapRange = properties.Attributes["weapRange"].AsFloat(1.5f),
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

			// Setup health stuff!
			int durability = GetMaxDurability(itemstack);
			if (durability > 1 && (itemstack.Collectible.GetRemainingDurability(itemstack) * durability) != durability) {
				float health = entity.WatchedAttributes.GetOrAddTreeAttribute("health").GetFloat("basemaxhealth", 20) * GetRemainingDurability(slot.Itemstack);
				entity.WatchedAttributes.GetOrAddTreeAttribute("health").SetFloat("currenthealth", health);
				entity.WatchedAttributes.MarkPathDirty("health");
			}

			// SPAWNING ENTITY //
			byEntity.World.SpawnEntity(entity);

			// Load clothing onto entity!
			var savedInventory = inheritedGears ? itemstack.Attributes.GetTreeAttribute("entInventory") : null;
			for (int i = 0; i < GearsDressCodes.Length; i++) {
				string spawnCode = GearsDressCodes[i] + (inheritedGears ? "Stack" : "Spawn");
				if (inheritedGears && savedInventory.HasAttribute(spawnCode)) {
					try {
						sentry.GearInventory[i].Itemstack = savedInventory.GetItemstack(spawnCode)?.Clone();
						sentry.GearInvSlotModified(i);
					} catch { }
				} else if (properties.Attributes[spawnCode].Exists) {
					try {
						var _items = api.World.GetItem(new AssetLocation(GetRandom(properties.Attributes[spawnCode].AsArray<string>(null))));
						ItemStack _stack = new ItemStack(_items, 1);
						if (i == 18 && sentry.GearInventory[16].Itemstack.Item is ItemBow) {
							_stack = new ItemStack(_items, GetRandom(_items.MaxStackSize, 5));
						}
						var newstack = byEntity.World.SpawnItemEntity(_stack, entity.ServerPos.XYZ) as EntityItem;
						sentry.GearInventory[i].Itemstack = newstack?.Itemstack;
						newstack.Die(EnumDespawnReason.PickedUp, null);
						sentry.GearInvSlotModified(i);
					} catch { }
				}
			}

			handHandling = EnumHandHandling.PreventDefaultAction;
		}

		public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, GridRecipe byRecipe) {
			base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe);
			ItemStack itemstack = null;
			bool usingOldPeople = false;
			foreach (ItemSlot itemSlot in allInputslots) {
				if (itemSlot.Empty) {
					continue;
				}
				if (itemSlot.Itemstack?.Item is ItemPeople oldPeople) {
					usingOldPeople = true;
					itemstack = itemSlot.Itemstack;
				}
			}
			if (usingOldPeople && itemstack != null) {
				if (itemstack.Attributes.HasAttribute("appliedParts")) {
					var prvPartTree = itemstack.Attributes.GetTreeAttribute("appliedParts");
					var newPartTree = outputSlot.Itemstack.Attributes.GetOrAddTreeAttribute("appliedParts");
					foreach (var part in prvPartTree) {
						newPartTree.SetString(new string(part.Key), prvPartTree.GetString(part.Key));
					}
				}
				if (itemstack.Attributes.HasAttribute("appliedNames")) {
					var prvNameTree = itemstack.Attributes.GetTreeAttribute("appliedNames");
					var newNameTree = outputSlot.Itemstack.Attributes.GetOrAddTreeAttribute("appliedNames");
					foreach (var name in prvNameTree) {
						newNameTree.SetString(new string(name.Key), prvNameTree.GetString(name.Key));
					}
				}
				if (itemstack.Attributes.HasAttribute("entInventory")) {
					var prvGearTree = itemstack.Attributes.GetTreeAttribute("entInventory");
					var newGearTree = outputSlot.Itemstack.Attributes.GetOrAddTreeAttribute("entInventory");
					foreach (var gear in prvGearTree) {
						newGearTree.SetItemstack(new string(gear.Key), prvGearTree.GetItemstack(gear.Key));
					}
				}
				outputSlot.Itemstack.Item.Textures["skin"] = itemstack.Item.Textures["skin"].Clone();
			}
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
			ItemStack itemstack = inSlot.Itemstack;
			if (itemstack.Attributes.HasAttribute("appliedNames")) {
				var nametree = itemstack.Attributes.GetTreeAttribute("appliedNames");
				dsc.AppendLine($"{nametree.GetString("name")} {nametree.GetString("last")}");
			}
			int maxDurability = GetMaxDurability(itemstack);
			if (maxDurability > 1) {
				dsc.AppendLine($"<font color=\"#ff8888\">Health: {itemstack.Collectible.GetRemainingDurability(itemstack)}%</font>");
			}
			if (itemstack.Attributes.HasAttribute("entInventory")) {
				var geartree = itemstack.Attributes.GetTreeAttribute("entInventory");
				for (int i = 0; i < GearsDressCodes.Length; i++) {
					string stackCode = GearsDressCodes[i] + "Stack";
					if (geartree.HasAttribute(stackCode)) {
						/** Not important enough to cause a crash or for me to fix. **/
						try { dsc.AppendLine(GetInventoryGear(geartree, stackCode)); } catch { }
					}
				}
			}
		}

		private static string GetInventoryGear(ITreeAttribute inventory, string slotName) {
			if (inventory.HasAttribute(slotName) && inventory.GetItemstack(slotName) != null) {
				var itemstack = inventory.GetItemstack(slotName);
				int durability = itemstack.Attributes.HasAttribute("durability") ? itemstack.Item.GetRemainingDurability(itemstack) : 100;
				int itemAmount = itemstack.StackSize;
				string percentage = ColorTranslator.ToHtml(ColorFromHSV(Math.Clamp((double)durability, 0, 100), 0.4, 1));
				string itemEnding = "";
				if (durability < 100) {
					itemEnding += $" (<font color=\"{percentage}\">{durability}%</font>)";
				}
				if (itemAmount > 1) {
					itemEnding += $" ({itemAmount}x)";
				}
				return $"{Lang.Get(itemstack.Item?.Code?.Domain + ":item-" + itemstack.Item?.Code.Path) ?? "unknown"}{itemEnding}";
			}
			return "null";
		}
	}
}