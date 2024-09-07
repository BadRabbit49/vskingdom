using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class BlockEntityPost : BlockEntity, IHeatSource, IPointOfInterest {
		public BlockEntityPost() { }
		public bool doRedraw;
		public bool cPrvBurn;
		public long listener;
		public string ownerUID;
		public Int32 postTier;
		public Int32 respawns;
		public Int64 burnFuel;
		public Int32 capacity => (Int32)(postTier * 1);
		public Int32 healPass => (Int32)(postTier * 2);
		public Int32 maxpawns => (Int32)(postTier * 3);
		public Int32 areasize => (Int32)(postTier * 4);
		public Int32 offsetMs => (Int32)(msAnHour / 4);
		public Int64 burnTime => (Int64)(postTier * msAMonth);
		public bool fireLive { get => Block.Variant["state"] == "live"; }
		public bool hasSmoke { get => Block.Variant["fuels"] != "temp" && fireLive; }
		public bool fillAmmo { get => postTier >= 4; }
		public bool coldTemp { get => Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues).Temperature < 10; }
		public bool darkHere { get => Api.World.BlockAccessor.GetLightLevel(Pos, EnumLightLevelType.TimeOfDaySunLight) < 10; }
		public long msAnHour { get => (long)(1000 * 60 * Api.World.Calendar.SpeedOfTime); }
		public long msPerDay { get => (long)(msAnHour * Api.World.Calendar.HoursPerDay); }
		public long msAMonth { get => (long)(msPerDay * Api.World.Calendar.DaysPerMonth); }
		public List<long> entGUIDS = new List<long>();
		public long[] entityList { get => entGUIDS.ToArray(); }
		public Vec3d Position => Pos.ToVec3d();
		public string Type => "downtime";
		public string DialogTitle { get => Lang.Get("Brazier"); }
		public FirepitContentsRenderer renderer { get; set; }
		public ILoadedSound ambientSound { get; set; }

		public override void Initialize(ICoreAPI api) {
			base.Initialize(api);
			postTier = metalTiers.GetValueSafe(Block.Variant["metal"]);
			if (api is ICoreClientAPI capi) {
				// TODO: IMPLEMENT BUILT IN RENDERER FROM FORGE BLOCK.
				// capi.Event.RegisterRenderer(renderer = new ForgeContentsRenderer(Pos, capi), EnumRenderStage.Opaque, "forge");
				// renderer.SetContents(contents, fuelLevel, burning, true);
				this.listener = RegisterGameTickListener(OnOffsetTick, offsetMs);
			}
			api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
		}

		public override void OnBlockPlaced(ItemStack byItemStack = null) {
			base.OnBlockPlaced(byItemStack);
			respawns = 3 * postTier;
			if (Api.World.NearestPlayer(Pos.X, Pos.Y, Pos.Z) != null) {
				ownerUID = Api.World.NearestPlayer(Pos.X, Pos.Y, Pos.Z).PlayerUID;
			}
			if (Block.Variant["fuels"] != "null") {
				burnFuel = burnTime;
			}
		}

		public override void OnBlockBroken(IPlayer byPlayer = null) {
			base.OnBlockBroken(byPlayer);
			ToggleAmbientSounds(false);
			if (byPlayer.PlayerUID != ownerUID) {
				switch (Block.LastCodePart()) {
					case "good": Api.World.BlockAccessor.ExchangeBlock(Api.World.GetBlock(Block.CodeWithVariant("level", "hurt")).Id, Pos); return;
					case "hurt": Api.World.BlockAccessor.ExchangeBlock(Api.World.GetBlock(Block.CodeWithVariant("level", "dead")).Id, Pos); return;
					case "dead": break;
				}
			}
			Api.ModLoader.GetModSystem<POIRegistry>()?.RemovePOI(this);
		}

		public override void OnBlockRemoved() {
			base.OnBlockRemoved();
			ToggleAmbientSounds(false);
			Api.ModLoader.GetModSystem<POIRegistry>()?.RemovePOI(this);
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
			UpdateTreeFromServer(forPlayer);
			if (forPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative) {
				dsc.AppendLine($"FuelLeft: {burnFuel}/{burnTime}");
				dsc.AppendLine($"Respawns: {respawns}/{maxpawns}");
				dsc.AppendLine($"AreaSize: {areasize}");
				dsc.AppendLine($"Capacity: {entGUIDS.Count}/{capacity}");
			} else {
				dsc.AppendLine($"{100 * (burnFuel / burnTime)}%");
				dsc.AppendLine($"{100 * (respawns / maxpawns)}%");
				dsc.AppendLine($"{100 * (entGUIDS.Count / capacity)}%");
			}
			base.GetBlockInfo(forPlayer, dsc);
		}

		public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve) {
			base.FromTreeAttributes(tree, worldAccessForResolve);
			respawns = tree.GetInt("respawns");
			burnFuel = tree.GetLong("burnFuel");
			ownerUID = tree.GetString("ownerUID");
			entGUIDS = GetListFromString(tree.GetString("entities"));
			if (Api?.Side == EnumAppSide.Client) {
				UpdateRenderer();
				if (cPrvBurn != fireLive || doRedraw) {
					ToggleAmbientSounds(fireLive);
					cPrvBurn = fireLive;
					MarkDirty(true);
					doRedraw = false;
				}
			}
		}

		public override void ToTreeAttributes(ITreeAttribute tree) {
			// Retrieve inventory values.
			tree.SetInt("respawns", respawns);
			tree.SetLong("burnFuel", burnFuel);
			tree.SetString("ownerUID", ownerUID);
			tree.SetString("entities", GetStringFromList(entGUIDS));
			base.ToTreeAttributes(tree);
		}
	
		public override void OnReceivedServerPacket(int packetid, byte[] data) {
			base.OnReceivedServerPacket(packetid, data);
			switch (packetid) {
				case 7000: FromTreeAttributes(SerializerUtil.Deserialize<TreeAttribute>(data), Api.World); return;
				case 7001: IsCapacity(SerializerUtil.Deserialize<long>(data)); return;
			}
		}

		public virtual void UpdateTreeFromServer(IPlayer player) {
			TreeAttribute tree = new TreeAttribute();
			tree.SetInt("respawns", respawns);
			tree.SetLong("burnFuel", burnFuel);
			tree.SetString("ownerUID", ownerUID);
			tree.SetString("entities", GetStringFromList(entGUIDS));
			(Api as ICoreServerAPI)?.Network.SendBlockEntityPacket(player as IServerPlayer, Pos.X, Pos.Y, Pos.Z, 7000, SerializerUtil.Serialize<TreeAttribute>(tree));
		}

		public bool IsCapacity(long entityId) {
			// Check if there is capacity left for another soldier.
			if (entGUIDS.Count < capacity) {
				entGUIDS.Add(entityId);
			}
			return entGUIDS.Count < capacity;
		}

		public void OnOffsetTick(float dt) {
			MarkDirty();
			// Go down by 60 seconds each time.
			burnFuel -= (long)(Api.World.Calendar.SpeedOfTime);
			if (burnFuel < 0) {
				burnFuel = 0;
				Extinguish();
			}
			if (entGUIDS == null || entGUIDS.Count == 0) {
				return;
			}
			EntitySentry bestUsed = null;
			EntitySentry[] sentryList = new EntitySentry[entGUIDS.Count];
			int squaredArea = areasize * areasize;
			for (int i = 0; i < entGUIDS.Count; i++) {
				EntitySentry sentry = (Api.World.GetEntityById(entityList[i]) as EntitySentry) ?? null;
				sentryList[i] = sentry;
				if (sentry == null) {
					entGUIDS.Remove(entityList[i]);
					continue;
				} else if (bestUsed == null) {
					bestUsed = sentry;
				}
				if (sentry.ServerPos.SquareDistanceTo(Pos.ToVec3d()) <= squaredArea) {
					sentry.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Internal, Type = healPass > 0 ? EnumDamageType.Heal : EnumDamageType.Poison }, Math.Abs(healPass));
				}
			}
			if (bestUsed == null) {
				return;
			}
			string[] enemiesList = bestUsed.cachedData.enemiesLIST;
			string[] outlawsList = bestUsed.cachedData.outlawsLIST;
			bool hasEnemies = !(enemiesList != null && enemiesList?.Length > 0);
			bool hasOutlaws = !(outlawsList != null || outlawsList?.Length > 0);
			bool soundAlarm = false;
			if (!hasEnemies && !hasOutlaws) {
				return;
			}
			List<Entity> entities = Api.World.GetEntitiesAround(Pos.ToVec3d(), (float)areasize, (float)areasize).ToList();
			List<Entity> hostiles = new List<Entity>();
			if (entities == null || entities.Count == 0) {
				return;
			}
			for (int i = 0; i < entities.Count; i++) {
				if (entGUIDS.Contains(entities[i].EntityId) || !entities[i].Alive) {
					continue;
				} else if (hasEnemies && entities[i].WatchedAttributes.HasAttribute("kingdomGUID") && enemiesList.Contains(entities[i].WatchedAttributes.GetString("kingdomGUID"))) {
					soundAlarm = true;
					hostiles.Add(entities[i]);
				} else if (hasOutlaws && entities[i] is EntityPlayer player && outlawsList.Contains(player.PlayerUID)) {
					soundAlarm = true;
					hostiles.Add(entities[i]);
				}
			}
			if (!soundAlarm || hostiles.Count == 0) {
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
		}

		public bool EntsNearby(Entity[] entities) {
			for (int s = 0; s < entGUIDS.Count; s++) {
				if (entities[s]?.ServerPos.SquareDistanceTo(Pos.ToVec3d().Add(0.5, 0.5, 0.5)) < areasize * areasize) {
					return true;
				}
			}
			return false;
		}

		public bool SetVariant(string metal, string fuels, string state, string level) {
			try {
				if (metal == null) {
					metal = Block.Variant["metal"];
				}
				if (fuels == null) {
					fuels = Block.Variant["fuels"];
				}
				if (state == null) {
					state = Block.Variant["state"];
				}
				if (level == null) {
					level = Block.Variant["level"];
				}
				if (state == "live" && level == "dead") {
					state = "done";
				}
				Api.World.BlockAccessor.ExchangeBlock(Api.World.GetBlock(new AssetLocation($"vskingdom:post-{metal}-{fuels}-{state}-{level}")).Id, Pos);
				doRedraw = true;
				MarkDirty(true);
				return true;
			} catch {
				Api.Logger.Error("Block code was: " + Block.Code);
				Api.Logger.Error("Brand new code: " + $"vskingdom:post-{metal}-{fuels}-{state}-{level}");
			}
			return false;
		}

		public void AddFuelsTo(IItemStack stack) {
			IGameCalendar calendar = Api.World.Calendar;
			// Special case for temporal gear! Resets all respawns and lasts an entire month!
			if (stack.Item is ItemTemporalGear) {
				respawns = maxpawns;
				burnFuel = burnTime;
			} else {
				CombustibleProperties fuelProperties = stack.Collectible.CombustibleProps;
				burnFuel += (long)(Math.Clamp(fuelProperties.BurnDuration, 0, burnTime));
			}
			MarkDirty(true);
		}

		public bool AddRepairs(int amount) {
			switch (Block.Variant["level"]) {
				case "dead":
					SetVariant(null, null, null, "hurt");
					AddRespawn(amount);
					return true;
				case "hurt":
					SetVariant(null, null, null, "good");
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
			} else {
				respawns += amount;
				doRedraw = true;
				MarkDirty(true);
				return true;
			}
		}

		public bool UseRespawn(int amount) {
			respawns -= amount;
			bool result = false;
			// Change the block if at a quarter health and kill the block if no more respawns left.
			if (Block.Variant["level"] == "good" && respawns <= (maxpawns / 4)) {
				SetVariant(null, null, null, "hurt");
				result = true;
			} else if (Block.Variant["level"] == "hurt" && respawns <= 0) {
				SetVariant(null, null, "done", "dead");
				result = true;
			} else if (Block.Variant["level"] == "dead" || respawns <= 0) {
				respawns = 0;
				result = false;
			} else {
				result = true;
			}
			MarkDirty(true);
			return result;
		}

		public void ChangeFuel(string fuel) {
			Api.World.BlockAccessor.ExchangeBlock(Api.World.GetBlock(Block.CodeWithVariant("fuels", fuel)).Id, Pos);
			MarkDirty(true);
		}

		public void Extinguish() {
			if (Block.Variant["state"] == "live") {
				string fuel = burnFuel <= 0 ? "null" : null;
				cPrvBurn = false;
				SetVariant(null, fuel, "done", null);
			}
		}

		public void IgnitePost() {
			if (Block.Variant["state"] != "live" && burnFuel > 0) {
				cPrvBurn = true;
				SetVariant(null, null, "live", null);
			}
		}

		public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos) {
			return fireLive ? 10 : (hasSmoke ? 0.25f : 0);
		}

		private void UpdateRenderer() {
			if (renderer is null) { return; }
			renderer.contentStackRenderer?.Dispose();
			renderer.contentStackRenderer = null;
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

		#region Music
		public override void OnBlockUnloaded() => ToggleAmbientSounds(false);
		#endregion
	}

	public class BlockPost : Block, IIgnitable {
		public BlockPost() { }
		public bool isExtinct => Variant["state"] != "live";
		public bool isLitFire => Variant["state"] == "live";
		public bool hasNoFuel => Variant["fuels"] == "null";
		public Block deadVariant { get; private set; }

		public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null) {
			base.OnBlockPlaced(world, blockPos, byItemStack);
			if (world.BlockAccessor.GetBlockEntity(blockPos) != null && world.BlockAccessor.GetBlockEntity(blockPos) is BlockEntityPost post) {
				post.ownerUID = world.NearestPlayer(blockPos.X, blockPos.Y, blockPos.Z).PlayerUID;
			}
		}

		public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1) {
			BlockEntityPost thisPost = GetBlockEntity<BlockEntityPost>(pos) ?? null;
			if (thisPost == null || thisPost.ownerUID == byPlayer.PlayerUID || LastCodePart(0) == "dead") {
				base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
			}
		}

		public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) {
			ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
			BlockEntityPost thisPost = GetBlockEntity<BlockEntityPost>(blockSel) ?? null;
			Item items = slot?.Itemstack?.Item ?? null;
			Block block = slot?.Itemstack?.Block ?? null;
			if (thisPost == null || items == null) {
				return base.OnBlockInteractStart(world, byPlayer, blockSel);
			}
			bool takesTemporal = Variant["metal"] == "electrum";
			if ((!takesTemporal && items is ItemFirewood) || (!takesTemporal && items is ItemCoal) || (takesTemporal && items is ItemTemporalGear)) {
				string oldFuels = Variant["fuels"];
				string newFuels = "null";
				string oldState = Variant["state"];
				if (items is ItemCoal) {
					newFuels = "coal";
				} else if (items is ItemFirewood) {
					newFuels = "wood";
				} else if (items is ItemTemporalGear) {
					newFuels = "temp";
				}
				if (oldFuels == "null" || oldState == "none" || (oldFuels == newFuels)) {
					thisPost.ChangeFuel(newFuels);
					thisPost.AddFuelsTo(slot.Itemstack);
					slot.TakeOut(1);
					slot.MarkDirty();
				}
			} else if (items.Attributes["currency"].Exists) {
				if (thisPost.AddRespawn(1)) {
					slot.TakeOut(1);
					slot.MarkDirty();
				}
			} else if (items is ItemIngot) {
				if (thisPost.AddRepairs(1)) {
					slot.TakeOut(1);
					slot.MarkDirty();
				}
			}
			return base.OnBlockInteractStart(world, byPlayer, blockSel);
		}

		public override bool AllowSnowCoverage(IWorldAccessor world, BlockPos blockPos) {
			return Variant["state"] != "live";
		}

		public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1) {
			return null;
		}

		public override void OnLoaded(ICoreAPI api) {
			base.OnLoaded(api);
			deadVariant = api.World.GetBlock(CodeWithVariant("state", "done"));
		}

		public override void OnGroundIdle(EntityItem entityItem) {
			base.OnGroundIdle(entityItem);
			if (!isLitFire && entityItem.Swimming) {
				api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), entityItem.Pos.X + 0.5, entityItem.Pos.Y + 0.75, entityItem.Pos.Z + 0.5, null, false, 16);
				int stacks = entityItem.Itemstack.StackSize;
				entityItem.Itemstack = new ItemStack(deadVariant);
				entityItem.Itemstack.StackSize = stacks;
			}
		}

		public override bool ShouldPlayAmbientSound(IWorldAccessor world, BlockPos pos) {
			return Variant["state"] == "live" && base.ShouldPlayAmbientSound(world, pos);
		}

		public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer) {
			return new BlockDropItemStack[0];
		}

		public void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling) {
			BlockEntityPost post = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityPost;
			if (post != null && !post.fireLive) {
				post.IgnitePost();
			}
			handling = EnumHandling.PreventDefault;
		}

		EnumIgniteState IIgnitable.OnTryIgniteStack(EntityAgent byEntity, BlockPos pos, ItemSlot slot, float secondsIgniting) {
			if (isExtinct && !hasNoFuel) {
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