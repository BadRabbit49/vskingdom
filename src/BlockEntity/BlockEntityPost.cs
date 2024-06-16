using Vintagestory.GameContent;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using System.Text;
using System.Linq;
using System.IO;
using System.Collections.Generic;

namespace VSKingdom {
	public class BlockEntityPost : BlockEntityOpenableContainer, IHeatSource, IPointOfInterest {
		public bool fireLive { get; set; }
		public bool hasSmoke { get; set; }
		public bool doRedraw { get; set; }
		public bool cPrvBurn { get; set; }
		public int metlTier { get; set; }
		public int capacity { get; set; }
		public int maxpawns { get; set; }
		public int respawns { get; set; }
		public double areasize { get; set; }
		public double burnTime { get; set; }
		public double maxBTime { get; set; }
		public double burnFuel { get; set; }
		public string Type => "downtime";
		public virtual string DialogTitle { get { return Lang.Get("Brazier"); } }
		public enum EnumBlockContainerPacketId { OpenInventory = 5000 }
		public Vec3d Position => Pos.ToVec3d();
		public GuiDialogBlockEntityFirepit clientDialog;
		internal InventorySmelting inventory;
		public override InventoryBase Inventory { get { return inventory; } }
		public override string InventoryClassName { get { return "Fuels"; } }
		public ItemSlot fuelSlot { get { return inventory[0]; } }
		public FirepitContentsRenderer renderer;
		public List<long> EntityUIDs { get; set; }
		
		public BlockEntityPost() {
			inventory = new InventorySmelting(null, null);
			inventory.SlotModified += OnSlotModifid;
		}

		public override void Initialize(ICoreAPI api) {
			base.Initialize(api);
			metlTier = 1;
			string metalType = Block.Variant["metal"];
			switch (metalType) {
				case "lead": metlTier = 2; break;
				case "copper": metlTier = 2; break;
				case "oxidizedcopper": metlTier = 2; break;
				case "tinbronze": metlTier = 3;  break;
				case "bismuthbronze":  metlTier = 3;  break;
				case "blackbronze":  metlTier = 3;  break;
				case "brass": metlTier = 4;  break;
				case "silver": metlTier = 4;  break;
				case "gold": metlTier = 4;  break;
				case "iron": metlTier = 5;  break;
				case "rust": metlTier = 5;  break;
				case "meteoriciron": metlTier = 6;  break;
				case "steel": metlTier = 7;  break;
				case "stainlesssteel": metlTier = 8;  break;
				case "titanium": metlTier = 9;  break;
				case "electrum": metlTier = 9;  break;
			}
			capacity = 1 * metlTier;
			maxpawns = 2 * metlTier;
			respawns = 2 * metlTier;
			areasize = 3 * metlTier;
			// Register entity as a point of interest.
			if (api is ICoreServerAPI sapi) {
				sapi.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
			}
		}

		public override void OnBlockBroken(IPlayer byPlayer = null) {
			base.OnBlockBroken(byPlayer);
			if (Api is ICoreServerAPI sapi) {
				sapi.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
			}
			base.OnBlockRemoved();
			if (invDialog?.IsOpened() == true) {
				invDialog?.TryClose();
			}
			invDialog?.Dispose();
			Inventory.DropAll(Position);
		}

		public override void OnBlockUnloaded() {
			base.OnBlockRemoved();
			if (invDialog?.IsOpened() == true) {
				invDialog?.TryClose();
			}
			invDialog?.Dispose();
		}

		public override void OnBlockRemoved() {
			base.OnBlockRemoved();

			if (Api is ICoreServerAPI sapi) {
				sapi.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
			}
			if (invDialog?.IsOpened() == true) {
				invDialog?.TryClose();
			}
			invDialog?.Dispose();
			if (EntityUIDs is null || EntityUIDs.Count == 0) {
				return;
			}
			for (int n = 0; n < EntityUIDs.Count; n++) {
				Api.World.GetEntityById(EntityUIDs[n]).GetBehavior<EntityBehaviorLoyalties>()?.SetOutpost(new BlockPos(0, 0, 0, 0));
			}
		}

		public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel) {
			if (Api.Side == EnumAppSide.Client) {
				toggleInventoryDialogClient(byPlayer, () => {
					SyncedTreeAttribute dtree = new SyncedTreeAttribute();
					ToTreeAttributes(dtree);
					clientDialog = new GuiDialogBlockEntityFirepit(DialogTitle, Inventory, Pos, dtree, Api as ICoreClientAPI);
					return clientDialog;
				});
			}
			return true;
		}

		public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
			base.GetBlockInfo(forPlayer, dsc);
			string str = "Respawns: " + respawns + "/" + maxpawns + "\nAreaSize: " + areasize;
			if (EntityUIDs != null) {
				str = "\nCapacity: " + EntityUIDs.Count + "/" + capacity + str;
			}
			if (maxBTime != 0) {
				str = str + "\nFuelLeft: " + maxBTime + "/" + burnTime;
			}
			dsc.AppendLine(str);
			return;
		}

		public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve) {
			base.FromTreeAttributes(tree, worldAccessForResolve);
			for (int n = 0; n < capacity; n++) {
				string strL = "NUM" + n;
				EntityUIDs.Insert(n, tree.GetLong(strL));
			}
			Inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
			burnTime = tree.GetDouble("burnTime");
			maxBTime = tree.GetDouble("maxBTime");
			burnFuel = tree.GetDouble("burnFuel");
			if (Api?.Side == EnumAppSide.Client) {
				UpdateRenderer();
			}
			if (Api?.Side == EnumAppSide.Client && (cPrvBurn != fireLive || doRedraw)) {
				GetBehavior<BlockBehaviorResupply>()?.ToggleAmbientSounds(fireLive);
				cPrvBurn = fireLive;
				MarkDirty(true);
				doRedraw = false;
			}
		}

		public override void ToTreeAttributes(ITreeAttribute tree) {
			base.ToTreeAttributes(tree);
			for (int n = 0; n < capacity; n++) {
				string strL = "NUM" + n;
				tree.SetLong(strL, EntityUIDs.ElementAt(n));
			}
			// Retrieve inventory values.
			ITreeAttribute invtree = new TreeAttribute();
			Inventory.ToTreeAttributes(invtree);
			tree["inventory"] = invtree;
			tree.SetDouble("burnTime", burnTime);
			tree.SetDouble("maxBTime", maxBTime);
			tree.SetDouble("burnFuel", burnFuel);
		}
		
		public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data) {
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
			IClientWorldAccessor clientWorld = (IClientWorldAccessor)Api.World;
			if (packetid == (int)EnumBlockContainerPacketId.OpenInventory) {
				if (invDialog != null) {
					if (invDialog?.IsOpened() == true)
						invDialog.TryClose();
					invDialog?.Dispose();
					invDialog = null;
					return;
				}
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
				invDialog = new GuiDialogBlockEntityInventory(dialogTitle, Inventory, Pos, cols, Api as ICoreClientAPI);
				invDialog.TryOpen();
			}
			if (packetid == (int)EnumBlockEntityPacketId.Close) {
				clientWorld.Player.InventoryManager.CloseInventory(Inventory);
				if (invDialog?.IsOpened() == true)
					invDialog?.TryClose();
				invDialog?.Dispose();
				invDialog = null;
			}
		}

		public bool IsCapacity(long entityId) {
			// Check if there is capacity left for another soldier.
			if (EntityUIDs.Count < capacity) {
				EntityUIDs.Add(entityId);
				return true;
			} else {
				return false;
			}
		}

		public void UseRespawn() {
			// Change the block if at a quarter health.
			if (Block.Variant["level"] != "hurt" && respawns <= (maxpawns / 4)) {
				var brazierHurt = Api.World.GetBlock(Block.CodeWithVariant("level", "hurt"));
				Api.World.BlockAccessor.ExchangeBlock(brazierHurt.Id, Pos);
				Block = brazierHurt;
			}
			// Kill the block if no more respawns left.
			if (respawns <= 0) {
				var brazierGone = Api.World.GetBlock(Block.CodeWithVariant("level", "dead"));
				Api.World.BlockAccessor.ExchangeBlock(brazierGone.Id, Pos);
				Block = brazierGone;
				for (int n = 0; n < EntityUIDs.Count; n++) {
					Api.World.GetEntityById(EntityUIDs[n]).GetBehavior<EntityBehaviorLoyalties>()?.SetOutpost(new BlockPos(0, 0, 0, 0));
				}
			}
		}

		public EnumIgniteState GetIgnitableState(float secondsIgniting) {
			if (fuelSlot.Empty || fireLive) {
				return EnumIgniteState.NotIgnitablePreventDefault;
			}
			return secondsIgniting > 3 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
		}

		public void Extinguish() {
			if (Block.Variant["state"] == "live") {
				var brazierDead = Api.World.GetBlock(Block.CodeWithVariant("state", "done"));
				Api.World.BlockAccessor.ExchangeBlock(brazierDead.Id, Pos);
				Block = brazierDead;
				fireLive = false;
				hasSmoke = false;
			}
		}

		public void IgnitePost() {
			if (Block.Variant["state"] != "live") {
				var brazierLive = Api.World.GetBlock(Block.CodeWithVariant("state", "live"));
				Api.World.BlockAccessor.ExchangeBlock(brazierLive.Id, Pos);
				Block = brazierLive;
				fireLive = true;
				hasSmoke = true;
			}
		}

		public void igniteWithFuel(IItemStack stack) {
			IGameCalendar calendar = Api.World.Calendar;
			// Special case for temporal gear! Resets all respawns and lasts an entire month!
			if (stack.Item is ItemTemporalGear) {
				respawns = maxpawns;
				maxBTime = burnTime = 60 * calendar.HoursPerDay * calendar.DaysPerMonth;
			} else {
				CombustibleProperties fuelCopts = stack.Collectible.CombustibleProps;
				maxBTime = burnTime = fuelCopts.BurnDuration * 60 + (fuelCopts.BurnDuration * 60 * calendar.CalendarSpeedMul);
			}
			Api.World.Logger.Notification("Total burn time is set to: " + maxBTime);
			MarkDirty(true);
		}

		public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos) {
			return fireLive ? 10 : (hasSmoke ? 0.25f : 0);
		}

		private void UpdateRenderer() {
			if (renderer is null) {
				return;
			}
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
	}

	public class BlockPost : Block, IIgnitable {
		public BlockPost() { }

		public EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting) {
			BlockEntityCharcoalPit becp = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityCharcoalPit;
			if (becp == null || becp.Lit) {
				return EnumIgniteState.NotIgnitablePreventDefault;
			}
			return secondsIgniting > 3 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
		}

		EnumIgniteState IIgnitable.OnTryIgniteStack(EntityAgent byEntity, BlockPos pos, ItemSlot slot, float secondsIgniting) {
			BlockEntityCharcoalPit becp = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityCharcoalPit;
			if (becp.Lit) {
				return secondsIgniting > 2 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
			}
			return EnumIgniteState.NotIgnitable;
		}

		public void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling) {
			BlockEntityCharcoalPit becp = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityCharcoalPit;
			if (becp != null && !becp.Lit) {
				becp.IgniteNow();
			}
			handling = EnumHandling.PreventDefault;
		}

		public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1) {
			return null;
		}

		public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer) {
			return new BlockDropItemStack[0];
		}
	}
}