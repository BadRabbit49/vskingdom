using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class BlockEntityPost : BlockEntityContainer, IHeatSource, IPointOfInterest {
		public BlockEntityPost() {
			inventory = new InventorySmelting(null, null);
			inventory.SlotModified += OnSlotModifid;
		}

		public bool fireLive { get => Block.Variant["state"] == "live"; }
		public bool hasSmoke { get => Block.Variant["fuels"] != "temp" && fireLive; }
		public bool doRedraw;
		public bool cPrvBurn;
		public int metlTier;
		public int capacity;
		public int maxpawns;
		public int respawns;
		public double areasize;
		public double burnTime;
		public double maxBTime;
		public double burnFuel;
		public string ownerUID;
		public string Type => "downtime";
		public string DialogTitle { get => Lang.Get("Brazier"); }
		public enum EnumBlockContainerPacketId { OpenInventory = 5000 }
		public Vec3d Position => Pos.ToVec3d();
		public GuiDialogBlockEntityFirepit clientDialog;
		public InventorySmelting inventory;
		public override InventoryBase Inventory { get => inventory; }
		public override string InventoryClassName { get => "Fuels"; }
		public ItemSlot fuelSlot { get => inventory[0]; }
		public ItemSlot ammoSlot { get => inventory[1]; }
		public FirepitContentsRenderer renderer;
		public List<long> EntityUIDs { get; set; } = new List<long>();
		public static Dictionary<string, int> tiers = new Dictionary<string, int> {
			{ "lead", 2 },
			{ "copper", 2 },
			{ "oxidizedcopper", 2 },
			{ "tinbronze", 3 },
			{ "bismuthbronze",  3 },
			{ "blackbronze",  3 },
			{ "brass", 4 },
			{ "silver", 4 },
			{ "gold", 4 },
			{ "iron", 5 },
			{ "rust", 5 },
			{ "meteoriciron", 6 },
			{ "steel", 7 },
			{ "stainlesssteel", 8 },
			{ "titanium", 9 },
			{ "electrum", 9 }
		};

		public override void Initialize(ICoreAPI api) {
			base.Initialize(api);
			// Establish metal tier values and their effects.
			metlTier = tiers.GetValueSafe(Block.Variant["metal"]);
			capacity = 1 * metlTier;
			maxpawns = 2 * metlTier;
			respawns = 3 * metlTier;
			areasize = 4 * metlTier;
			maxBTime = metlTier * (Api.World.Calendar.HoursPerDay * Api.World.Calendar.DaysPerMonth);
			RegisterGameTickListener(Returnings, 60000);
			// Register entity as a point of interest.
			if (api is ICoreServerAPI sapi) {
				sapi.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
			}
		}

		public override void OnBlockPlaced(ItemStack byItemStack = null) {
			base.OnBlockPlaced(byItemStack);
			if (Block.Variant["fuels"] != "null") {
				burnTime = maxBTime;
			}
		}
		
		public override void OnBlockBroken(IPlayer byPlayer = null) {
			if (byPlayer.PlayerUID != ownerUID) {
				switch (Block.LastCodePart()) {
					case "good": Api.World.BlockAccessor.ExchangeBlock(Api.World.GetBlock(Block.CodeWithVariant("level", "hurt")).Id, Pos); return;
					case "hurt": Api.World.BlockAccessor.ExchangeBlock(Api.World.GetBlock(Block.CodeWithVariant("level", "dead")).Id, Pos); return;
					case "dead": break;
				}
			}
			if (Api is ICoreServerAPI sapi) {
				sapi.ModLoader.GetModSystem<POIRegistry>()?.RemovePOI(this);
			}
			GetBehavior<BlockBehaviorResupply>()?.ToggleAmbientSounds(false);
			base.OnBlockBroken(byPlayer);
		}

		public override void OnBlockRemoved() {
			if (Api is ICoreServerAPI sapi) {
				sapi.ModLoader.GetModSystem<POIRegistry>()?.RemovePOI(this);
			}
			GetBehavior<BlockBehaviorResupply>()?.ToggleAmbientSounds(false);
			base.OnBlockRemoved();
		}

		public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
			dsc.AppendLine($"Respawns: {respawns}/{maxpawns}");
			dsc.AppendLine($"AreaSize: {areasize}");
			dsc.AppendLine($"FuelLeft: {maxBTime}/{burnTime}");
			dsc.AppendLine($"Capacity: {EntityUIDs.Count}/{capacity}");
			base.GetBlockInfo(forPlayer, dsc);
		}

		public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve) {
			base.FromTreeAttributes(tree, worldAccessForResolve);
			Inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
			doRedraw = tree.GetBool("doRedraw");
			cPrvBurn = tree.GetBool("cPrvBurn");
			metlTier = tree.GetInt("metlTier");
			capacity = tree.GetInt("capacity");
			maxpawns = tree.GetInt("maxpawns");
			respawns = tree.GetInt("respawns");
			areasize = tree.GetDouble("areasize");
			burnTime = tree.GetDouble("burnTime");
			maxBTime = tree.GetDouble("maxBTime");
			burnFuel = tree.GetDouble("burnFuel");
			ownerUID = tree.GetString("ownerUID");
			EntityUIDs = GetListFromString(tree.GetString("entities"));
			if (Api.Side == EnumAppSide.Client) {
				UpdateRenderer();
				if (cPrvBurn != fireLive || doRedraw) {
					GetBehavior<BlockBehaviorResupply>()?.ToggleAmbientSounds(fireLive);
					cPrvBurn = fireLive;
					MarkDirty(true);
					doRedraw = false;
				}
			}
		}
		
		public override void ToTreeAttributes(ITreeAttribute tree) {
			// Retrieve inventory values.
			ITreeAttribute invtree = new TreeAttribute();
			Inventory.ToTreeAttributes(invtree);
			tree["inventory"] = invtree;
			tree.SetBool("doRedraw", doRedraw);
			tree.SetBool("cPrvBurn", cPrvBurn);
			tree.SetInt("metlTier", metlTier);
			tree.SetInt("capacity", capacity);
			tree.SetInt("maxpawns", maxpawns);
			tree.SetInt("respawns", respawns);
			tree.SetDouble("areasize", areasize);
			tree.SetDouble("burnTime", burnTime);
			tree.SetDouble("maxBTime", maxBTime);
			tree.SetDouble("burnFuel", burnFuel);
			tree.SetString("ownerUID", ownerUID);
			tree.SetString("entities", GetStringFromList(EntityUIDs));
			base.ToTreeAttributes(tree);
		}
		
		public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data) {
			base.OnReceivedClientPacket(player, packetid, data);
			if (packetid < 1000) {
				Inventory.InvNetworkUtil.HandleClientPacket(player, packetid, data);
				// Tell server to save this chunk to disk again.
				Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();
				return;
			}
			if (packetid == (int)EnumBlockEntityPacketId.Close) {
				player.InventoryManager?.CloseInventory(Inventory);
			}
			if (packetid == (int)EnumBlockEntityPacketId.Open) {
				player.InventoryManager?.OpenInventory(Inventory);
			}
		}

		public override void OnReceivedServerPacket(int packetid, byte[] data) {
			base.OnReceivedServerPacket(packetid, data);
			if (packetid == (int)EnumBlockContainerPacketId.OpenInventory) {
				string dialogClassName;
				string dialogTitle;
				int cols;
				TreeAttribute tree = new TreeAttribute();
				using (MemoryStream ms = new MemoryStream(data)) {
					BinaryReader reader = new BinaryReader(ms);
					dialogClassName = reader.ReadString();
					dialogTitle = reader.ReadString();
					cols = reader.ReadByte();
					tree.FromBytes(reader);
				}
				Inventory.FromTreeAttributes(tree);
				Inventory.ResolveBlocksOrItems();
			}
		}

		public bool IsCapacity(long entityId) {
			// Check if there is capacity left for another soldier.
			if (EntityUIDs.Count < capacity) {
				EntityUIDs.Add(entityId);
			}
			return EntityUIDs.Count < capacity;
		}

		public void Returnings(float dt) {
			foreach (long uid in EntityUIDs) {
				Entity entity = (Api as ICoreServerAPI)?.World.GetEntityById(uid) ?? null;
				if (entity != null && entity.ServerPos.XYZ.DistanceTo(Pos.ToVec3d()) > areasize) {
					var taskManager = entity.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
					taskManager.GetTask<AiTaskSentryReturn>()?.StartExecute();
				}
			}
			/* TODO MAKE BURN TIMES CORRECT AND OPTIMIZE IT */
			if (fireLive) {
				// Go down by 60 seconds each time.
				burnTime -= 60;
				if (burnTime < 0) {
					burnTime = 0;
					Extinguish();
				}
			}
		}

		public void UseRespawn() {
			respawns--;
			// Change the block if at a quarter health and kill the block if no more respawns left.
			if (Block.Variant["level"] != "hurt" && respawns <= (maxpawns / 4)) {
				Api.World.BlockAccessor.ExchangeBlock(Api.World.GetBlock(Block.CodeWithVariant("level", "hurt")).Id, Pos);
			}
			if (respawns <= 0) {
				Api.World.BlockAccessor.ExchangeBlock(Api.World.GetBlock(Block.CodeWithVariants(new Dictionary<string, string>() { { "level", "dead" }, { "state", "done" } })).Id, Pos);
			}
			MarkDirty(true);
		}

		public void ChangeFuel(string fuel) {
			Api.World.BlockAccessor.ExchangeBlock(Api.World.GetBlock(Block.CodeWithVariant("fuels", fuel)).Id, Pos);
			MarkDirty(true);
		}

		public void Extinguish() {
			if (Block.Variant["state"] == "live") {
				Api.World.BlockAccessor.ExchangeBlock(Api.World.GetBlock(Block.CodeWithVariant("state", "done")).Id, Pos);
				MarkDirty(true);
			}
		}

		public void IgnitePost() {
			if (Block.Variant["state"] != "live") {
				Api.World.BlockAccessor.ExchangeBlock(Api.World.GetBlock(Block.CodeWithVariant("state", "live")).Id, Pos);
				MarkDirty(true);
			}
		}

		public void IgniteWithFuel(IItemStack stack) {
			IGameCalendar calendar = Api.World.Calendar;
			// Special case for temporal gear! Resets all respawns and lasts an entire month!
			if (stack.Item is ItemTemporalGear) {
				respawns = maxpawns;
				burnTime = maxBTime;
			} else {
				CombustibleProperties fuelCopts = stack.Collectible.CombustibleProps;
				burnTime += Math.Clamp((fuelCopts.BurnDuration * calendar.HoursPerDay), 0, maxBTime);
			}
			MarkDirty(true);
		}

		public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos) {
			return fireLive ? 10 : (hasSmoke ? 0.25f : 0);
		}

		private void UpdateRenderer() {
			if (renderer is null) { return; }
			renderer.contentStackRenderer?.Dispose();
			renderer.contentStackRenderer = null;
		}

		private void OnSlotModifid(int slotid) {
			Block = Api.World.BlockAccessor.GetBlock(Pos);
			UpdateRenderer();
			MarkDirty(Api.Side == EnumAppSide.Server);
			doRedraw = true;
			Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
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
	}

	public class BlockPost : Block, IIgnitable {
		public BlockPost() { }

		public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null) {
			base.OnBlockPlaced(world, blockPos, byItemStack);
			(api.World.BlockAccessor.GetBlockEntity(blockPos) as BlockEntityPost).ownerUID = world.NearestPlayer(blockPos.X, blockPos.Y, blockPos.Z).PlayerUID;
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
			Item itemType = slot?.Itemstack?.Item ?? null;
			if (thisPost == null || itemType == null) {
				return base.OnBlockInteractStart(world, byPlayer, blockSel);
			}
			bool takesTemporal = Variant["metal"] == "electrum";
			if ((!takesTemporal && itemType is ItemFirewood) || (!takesTemporal && itemType is ItemCoal) || (takesTemporal && itemType is ItemTemporalGear)) {
				string newFuel = "null";
				if (itemType is ItemCoal) {
					newFuel = "coal";
				} else if (itemType is ItemFirewood) {
					newFuel = "wood";
				} else if (itemType is ItemTemporalGear) {
					newFuel = "temp";
				}
				thisPost.ChangeFuel(newFuel);
				thisPost.IgniteWithFuel(slot.Itemstack);
				slot.TakeOut(1);
				slot.MarkDirty();
			} else if (itemType.Attributes["currency"].Exists && thisPost.respawns < thisPost.maxpawns) {
				thisPost.respawns++;
				slot.TakeOut(1);
				slot.MarkDirty();
			}
			return base.OnBlockInteractStart(world, byPlayer, blockSel);
		}

		public override bool AllowSnowCoverage(IWorldAccessor world, BlockPos blockPos) {
			return Variant["state"] != "live";
		}

		public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1) {
			return null;
		}
		
		public override bool ShouldPlayAmbientSound(IWorldAccessor world, BlockPos pos) {
			return Variant["state"] == "live" && base.ShouldPlayAmbientSound(world, pos);
		}

		public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer) {
			return new BlockDropItemStack[0];
		}

		public void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling) {
			BlockEntityPost post = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityPost;
			if (post != null && !post.fireLive) {
				post.IgnitePost();
			}
			handling = EnumHandling.PreventDefault;
		}

		EnumIgniteState IIgnitable.OnTryIgniteStack(EntityAgent byEntity, BlockPos pos, ItemSlot slot, float secondsIgniting) {
			BlockEntityPost post = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityPost;
			if (post.fireLive) {
				return secondsIgniting > 2 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
			}
			return EnumIgniteState.NotIgnitable;
		}

		public EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting) {
			BlockEntityPost post = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityPost;
			if (post == null || post.fuelSlot.Empty || post.fireLive) {
				return EnumIgniteState.NotIgnitablePreventDefault;
			}
			return secondsIgniting > 3 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
		}
	}
}