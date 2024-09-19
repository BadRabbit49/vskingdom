using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Vintagestory.API.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class BlockEntityPost : BlockEntity, IHeatSource, IPointOfInterest {
		public BlockEntityPost() { }
		public bool cPrvBurn;
		public bool isActive;
		public long listener;
		public long respawns;
		public double postTier;
		public double burnFuel;
		public double totalDmg;
		public string ownerUID;
		public Int32 capacity => (Int32)(postTier * 1);
		public Int32 healPass => (Int32)(postTier * 2);
		public Int32 maxpawns => (Int32)(postTier * 3);
		public Int32 areasize => (Int32)(postTier * 4);
		public Int32 offsetMs => (Int32)(msAnHour / 4);
		public Int32 burnTime => (Int32)((postTier * msAMonth) / 1000);
		public bool fireLive { get => isActive; }
		public bool hasSmoke { get => fireLive && contents.Collectible.Code.FirstCodePart() != "gear"; }
		public bool hasEnemy { get => enemyIDs.Count > 0; }
		public bool fillAmmo { get => postTier >= 4; }
		public bool coldTemp { get => Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues).Temperature < 10; }
		public bool darkHere { get => Api.World.BlockAccessor.GetLightLevel(Pos, EnumLightLevelType.TimeOfDaySunLight) < 10; }
		public long msAnHour { get => (long)(1000 * 60 * Api.World.Calendar.SpeedOfTime); }
		public long msPerDay { get => (long)(msAnHour * Api.World.Calendar.HoursPerDay); }
		public long msAMonth { get => (long)(msPerDay * Api.World.Calendar.DaysPerMonth); }
		public string[] usedFuel = new string[] { };
		public string[] usedItem = new string[] { };
		public List<long> entGUIDS = new List<long>();
		public List<string> enemyIDs = new List<string>();
		public List<Entity> entities = new List<Entity>();
		public Vec3d Position => Pos.ToVec3d();
		public string Type => "downtime";
		public string DialogTitle { get => Lang.Get("Brazier"); }
		public PostFuelRenderer renderer;
		public ItemStack contents;
		public ILoadedSound ambientSound { get; set; }

		public override void Initialize(ICoreAPI api) {
			base.Initialize(api);
			postTier = Block.Attributes["metalTier"].AsInt(1);
			usedFuel = Block.Attributes["usedFuels"].AsArray<string>(new string[] { "firewood", "agedfirewood", "charcoal", "coke", "ore-lignite", "ore-bituminouscoal", "ore-anthracite" });
			usedItem = Block.Attributes["usedMetal"].AsArray<string>(new string[] { "copper" });
			if (api is ICoreClientAPI capi) {
				// TODO: IMPLEMENT BUILT IN RENDERER FROM FORGE BLOCK.
				capi.Event.RegisterRenderer(renderer = new PostFuelRenderer(Pos, Block.Code, capi), EnumRenderStage.Opaque, "outpost");
				renderer.SetContents(contents, (float)burnFuel, isActive, true);
				RegisterGameTickListener(OnClientTick, 5000);
				RegisterGameTickListener(OnOffsetTick, 60000);
			}
			if (Api.Side == EnumAppSide.Server) {
				api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
			}
		}

		public override void OnBlockPlaced(ItemStack byItemStack = null) {
			base.OnBlockPlaced(byItemStack);
			respawns = (long)(Math.Round(3 * postTier));
			if (Api.World.NearestPlayer(Pos.X, Pos.Y, Pos.Z) != null) {
				ownerUID = Api.World.NearestPlayer(Pos.X, Pos.Y, Pos.Z).PlayerUID;
			}
		}

		public override void OnBlockBroken(IPlayer byPlayer = null) {
			ToggleAmbientSounds(false);
			totalDmg += 10 * (Math.Clamp(((10 - postTier) * 0.1), 0.1, 1));
			if (byPlayer.PlayerUID != ownerUID) {
				switch (totalDmg) {
					case <= 50: Api.World.BlockAccessor.ExchangeBlock(Api.World.GetBlock(Block.CodeWithVariant("level", "hurt")).Id, Pos); return;
					case <= 80: Api.World.BlockAccessor.ExchangeBlock(Api.World.GetBlock(Block.CodeWithVariant("level", "dead")).Id, Pos); return;
					case > 100: break;
				}
			}
			base.OnBlockBroken(byPlayer);
			if (contents != null) {
				Api.World.SpawnItemEntity(contents, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
			}
			ambientSound?.Dispose();
			Api.ModLoader.GetModSystem<POIRegistry>()?.RemovePOI(this);
		}

		public override void OnBlockRemoved() {
			base.OnBlockRemoved();
			ToggleAmbientSounds(false);
			if (renderer != null) {
				renderer.Dispose();
				renderer = null;
			}
			Api.ModLoader.GetModSystem<POIRegistry>()?.RemovePOI(this);
		}

		public override void OnBlockUnloaded() {
			ToggleAmbientSounds(false);
			renderer?.Dispose();
		}

		public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
			if (ownerUID == null) {
				base.GetBlockInfo(forPlayer, dsc);
				return;
			} else if (ownerUID != forPlayer.PlayerUID) {
				var ownerName = forPlayer.Entity.Api.World.PlayerByUid(ownerUID).PlayerName;
				dsc.AppendLine(ownerName);
				base.GetBlockInfo(forPlayer, dsc);
				return;
			}
			if (forPlayer.PlayerUID == ownerUID) {
				dsc.AppendLine($"Condition: {100 - totalDmg}%");
				dsc.AppendLine($"FuelLeft: {burnFuel}/{burnTime}");
				dsc.AppendLine($"FuelItem: {contents?.GetName() ?? "nothing"}");
				dsc.AppendLine($"Respawns: {respawns}/{maxpawns}");
				dsc.AppendLine($"AreaSize: {areasize}");
				dsc.AppendLine($"Capacity: {entGUIDS.Count}/{capacity}");
			} else {
				try {
					dsc.AppendLine($"{forPlayer.Entity.Api.World.PlayerByUid(ownerUID).PlayerName}");
				} catch { }
			}
			base.GetBlockInfo(forPlayer, dsc);
		}

		public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve) {
			base.FromTreeAttributes(tree, worldAccessForResolve);
			respawns = tree.GetInt("respawns");
			isActive = tree.GetInt("isActive") > 0;
			totalDmg = tree.GetDouble("totalDmg");
			burnFuel = tree.GetDouble("burnFuel");
			ownerUID = tree.GetString("ownerUID");
			if (tree.HasAttribute("contents")) {
				contents = tree.GetItemstack("contents");
			}
			if (tree.HasAttribute("entities")) {
				entGUIDS = GetListFromString(tree.GetString("entities"));
			}
			if (Api != null) {
				contents?.ResolveBlockOrItem(Api.World);
			}
			if (renderer != null) {
				renderer.SetContents(contents, (float)burnFuel, isActive, true);
			}
		}

		public override void ToTreeAttributes(ITreeAttribute tree) {
			base.ToTreeAttributes(tree);
			tree.SetInt("respawns", (int)respawns);
			tree.SetInt("isActive", isActive ? 1 : 0);
			tree.SetDouble("totalDmg", totalDmg);
			tree.SetDouble("burnFuel", burnFuel);
			tree.SetString("ownerUID", ownerUID);
			if (contents != null) {
				tree.SetItemstack("contents", contents.Clone());
			}
			if (entGUIDS.Count > 0) {
				tree.SetString("entities", GetStringFromList(entGUIDS));
			}
		}
	
		public override void OnReceivedServerPacket(int packetid, byte[] data) {
			base.OnReceivedServerPacket(packetid, data);
			switch (packetid) {
				case 7000: FromTreeAttributes(SerializerUtil.Deserialize<TreeAttribute>(data), Api.World); return;
				case 7001: IsCapacity(SerializerUtil.Deserialize<long>(data)); return;
			}
		}

		public bool IsCapacity(long entityId) {
			// Check if there is capacity left for another soldier.
			if (entGUIDS.Count < capacity && Api.World.GetEntityById(entityId) is EntitySentry sentry) {
				entGUIDS.Add(entityId);
				entities.Add(sentry);
			}
			return entGUIDS.Count < capacity;
		}
		
		private void OnClientTick(float dt) {
			if (Api?.Side == EnumAppSide.Client && cPrvBurn != isActive) {
				ToggleAmbientSounds(fireLive);
				cPrvBurn = fireLive;
			}
			if (isActive && Api.World.Rand.NextDouble() < 0.13) {
				BlockEntityCoalPile.SpawnBurningCoalParticles(Api, Pos.ToVec3d().Add(4 / 16f, 14 / 16f, 4 / 16f), 8 / 16f, 8 / 16f);
			}
			if (renderer != null) {
				renderer.SetContents(contents, (float)burnFuel, isActive, false);
			}
		}

		private void OnOffsetTick(float dt) {
			if (isActive) {
				burnFuel -= 60;
				if (burnFuel < 0) {
					burnFuel = 0;
					Extinguish();
				}
				MarkDirty();
			}
			if (entGUIDS.Count == 0) {
				return;
			}
			try {
				EntitySentry bestUsed = null;
				EntitySentry[] sentryList = new EntitySentry[entGUIDS.Count];
				long[] entityLists = entGUIDS.ToArray();
				double squaredArea = areasize * areasize;
				for (int i = 0; i < entGUIDS.Count; i++) {
					EntitySentry sentry = (Api.World.GetEntityById(entityLists[i]) as EntitySentry) ?? null;
					sentryList[i] = sentry;
					if (sentry == null) {
						entGUIDS.Remove(entityLists[i]);
						continue;
					}
					if (bestUsed == null) {
						bestUsed = sentry;
					}
					if (healPass > 0 && sentry.ServerPos.SquareDistanceTo(Pos.ToVec3d()) <= squaredArea) {
						sentry.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal }, Math.Abs(healPass));
					}
				}
				if (bestUsed == null) {
					return;
				}
				enemyIDs = bestUsed.cachedData.enemiesLIST.ToList();
				if (!hasEnemy) {
					return;
				}
				bool soundAlarm = false;
				List<Entity> hostiles = Api.World.GetEntitiesAround(Pos.ToVec3d(), (float)areasize, (float)areasize, (matches => EntHostile(matches))).ToList();
				if (hostiles.Count > 0) {
					soundAlarm = true;
				}
				if (!soundAlarm || hostiles.Count == 0 || entities.Count == 0) {
					return;
				}
				for (int i = 0; i < sentryList.Length; i++) {
					if (sentryList[i] != null && !sentryList[i].ruleOrder[1] && !sentryList[i].ruleOrder[5] && sentryList[i].ServerPos.XYZ.SquareDistanceTo(Pos.ToVec3d()) > squaredArea) {
						int target = Api.World.Rand.Next(0, (hostiles.Count - 1));
						var taskManager = sentryList[i].GetBehavior<EntityBehaviorTaskAI>().TaskManager;
						taskManager.GetTask<AiTaskSentryAttack>()?.OnAllyAttacked(hostiles[target]);
						taskManager.GetTask<AiTaskSentryRanged>()?.OnAllyAttacked(hostiles[target]);
						sentryList[i].ruleOrder[7] = true;
					}
				}
			} catch { }
		}

		private bool EntHostile(Entity ent) {
			if (entGUIDS.Contains(ent.EntityId) || !ent.Alive) {
				return false;
			}
			if (hasEnemy && ent.WatchedAttributes.HasAttribute(king_GUID) && enemyIDs.Contains(ent.WatchedAttributes.GetString(king_GUID))) {
				return true;
			}
			return false;
		}

		public bool SetVariant(string metal, string level) {
			try {
				Api.World.BlockAccessor.ExchangeBlock(Api.World.GetBlock(new AssetLocation($"vskingdom:post-{metal}-{level}")).Id, Pos);
				MarkDirty(true);
				return true;
			} catch {
				Api.Logger.Error("Block code was: " + Block.Code);
				Api.Logger.Error("Brand new code: " + $"vskingdom:post-{metal}-{level}");
			}
			return false;
		}

		public bool AddFuelsTo(ItemStack itemstack) {
			if (contents == null || contents?.StackSize == 0) {
				contents = itemstack.Clone();
			}
			try {
				if (contents != null && itemstack.Class == EnumItemClass.Item && contents.Item.Code.Path == itemstack.Item.Code.Path) {
					contents.StackSize += 1;
					// Special case for temporal gear! Resets all respawns and lasts an entire month!
					if (itemstack.Item.Code.Path == "gear-temporal") {
						respawns = maxpawns;
						burnFuel = burnTime;
					} else {
						CombustibleProperties fuelProperties = itemstack.Collectible.CombustibleProps;
						burnFuel += Math.Clamp(fuelProperties.BurnDuration, 0, burnTime);
					}
					renderer?.SetContents(itemstack, (float)burnFuel, isActive, false);
					return true;
				}
			} catch (NullReferenceException e) {
				Api.World.Logger.Error($"Something went wrong @~@!\n{e}");
			}
			return false;
		}

		public bool AddRepairs(int amount) {
			totalDmg -= amount;
			switch (Block.Variant["level"]) {
				case "dead":
					SetVariant(Block.Variant["metal"], "hurt");
					AddRespawn(amount);
					return true;
				case "hurt":
					SetVariant(Block.Variant["metal"], "good");
					AddRespawn(amount);
					return true;
				case "good":
					return AddRespawn(amount);
				default: return false;
			}
		}

		public bool AddRespawn(int amount) {
			if (Block.Variant["level"] == "dead" || respawns + amount > maxpawns) {
				return false;
			}
			respawns += amount;
			MarkDirty();
			return true;
		}

		public void UseRespawn(int amount) {
			respawns -= amount;
			if (respawns <= 0) {
				respawns = 0;
			}
			MarkDirty();
		}

		public void Extinguish() {
			if (!isActive) {
				return;
			}
			isActive = false;
		}

		public void IgnitePost() {
			if (isActive || burnFuel < 0) {
				return;
			}
			isActive = true;
			renderer?.SetContents(contents, (float)burnFuel, isActive, false);
			MarkDirty();
		}

		public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos) {
			return fireLive ? 10 : (hasSmoke ? 0.25f : 0);
		}

		private static string GetStringFromList(List<long> input) {
			string output = string.Empty;
			foreach (long num in input) {
				output += num + ',';
			}
			return output.TrimEnd(',');
		}

		private static List<long> GetListFromString(string input) {
			string[] strArray = input.Split(',');
			List<long> output = new List<long>();
			foreach (string num in strArray) {
				try { output.Add(Int64.Parse(num)); } catch { continue; }
			}
			return output;
		}
		#region Sound
		public virtual float SoundLevel { get => 0.66f; }

		public void ToggleAmbientSounds(bool on) {
			if (Api.Side != EnumAppSide.Client) { return; }
			if (on) {
				if (ambientSound is null || !ambientSound.IsPlaying) {
					ambientSound = ((IClientWorldAccessor)Api.World).LoadSound(new SoundParams() {
						Location = new AssetLocation("game:sounds/environment/fireplace"),
						ShouldLoop = true,
						Position = Pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
						DisposeOnFinish = false,
						Volume = SoundLevel
					});
					if (ambientSound != null) {
						ambientSound.Start();
						ambientSound.PlaybackPosition = ambientSound.SoundLengthSeconds * (float)Api.World.Rand.NextDouble();
					}
				}
			} else {
				ambientSound?.Stop();
				ambientSound?.Dispose();
				ambientSound = null;
			}
		}
		#endregion
		public bool OnPlayerInteract(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) {
			ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
			Item item = slot?.Itemstack?.Item ?? null;
			if (item == null) {
				return false;
			}
			if (item.Code.FirstCodePart() == "people" && ownerUID == null) {
				ownerUID = byPlayer.PlayerUID;
			}
			if (usedFuel.Contains(item.Code.Path)) {
				if (AddFuelsTo(slot.Itemstack)) {
					slot.TakeOut(1);
					slot.MarkDirty();
					return true;
				}
			}
			if (item.Attributes["currency"].Exists) {
				if (AddRespawn(1)) {
					slot.TakeOut(1);
					slot.MarkDirty();
					return true;
				}
			}
			if (item.Code.FirstCodePart() == "ingot" && usedItem.Contains(item.LastCodePart())) {
				if (AddRepairs(1)) {
					slot.TakeOut(1);
					slot.MarkDirty();
					return true;
				}
			}
			return false;
		}
	}

	public class BlockPost : Block, IIgnitable {
		public BlockPost() { }
		public WorldInteraction[] interactions;
		public List<ItemStack> fuelstacklist = new List<ItemStack>();

		public override void OnLoaded(ICoreAPI api) {
			base.OnLoaded(api);
			if (api.Side != EnumAppSide.Client) { return; }
			ICoreClientAPI capi = api as ICoreClientAPI;
			interactions = ObjectCacheUtil.GetOrCreate(api, "postBlockInteractions", delegate {
				List<ItemStack> list = new List<ItemStack>();
				List<ItemStack> list2 = BlockBehaviorCanIgnite.CanIgniteStacks(api, withFirestarter: false);
				return new WorldInteraction[2] {
					new WorldInteraction {
						ActionLangCode = "blockhelp-forge-fuel",
						HotKeyCode = "shift",
						MouseButton = EnumMouseButton.Right,
						Itemstacks = fuelstacklist.ToArray(),
						GetMatchingStacks = (WorldInteraction wi, BlockSelection bs, EntitySelection es) => (api.World.BlockAccessor.GetBlockEntity(bs.Position) is BlockEntityForge blockEntityForge2 && blockEntityForge2.FuelLevel < 0.625f) ? wi.Itemstacks : null
					},
					new WorldInteraction {
						ActionLangCode = "blockhelp-forge-ignite",
						HotKeyCode = "shift",
						MouseButton = EnumMouseButton.Right,
						Itemstacks = list2.ToArray(),
						GetMatchingStacks = (WorldInteraction wi, BlockSelection bs, EntitySelection es) => (api.World.BlockAccessor.GetBlockEntity(bs.Position) is BlockEntityForge blockEntityForge && blockEntityForge.CanIgnite && !blockEntityForge.IsBurning) ? wi.Itemstacks : null
					}
				};
			});
		}

		public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) {
			BlockEntityPost thisPost = GetBlockEntity<BlockEntityPost>(blockSel.Position) ?? null;
			if (thisPost != null) {
				return thisPost.OnPlayerInteract(world, byPlayer, blockSel);
			}
			return base.OnBlockInteractStart(world, byPlayer, blockSel);
		}

		public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer) {
			return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
		}

		public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer) {
			return new BlockDropItemStack[0];
		}

		public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1) {
			return null;
		}

		public void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling) {
			handling = EnumHandling.PreventDefault;
			BlockEntityPost post = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityPost;
			post?.IgnitePost();
		}

		EnumIgniteState IIgnitable.OnTryIgniteStack(EntityAgent byEntity, BlockPos pos, ItemSlot slot, float secondsIgniting) {
			BlockEntityPost post = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityPost;
			if (post.fireLive) {
				return secondsIgniting > 2 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
			}
			return EnumIgniteState.NotIgnitable;
		}

		public EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting) {
			BlockEntityPost post = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityPost;
			if (post == null || post.fireLive) {
				return EnumIgniteState.NotIgnitablePreventDefault;
			}
			return secondsIgniting > 3 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
		}
	}
}