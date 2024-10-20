﻿using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;

namespace VSKingdom {
	public class BlockEntityBody : BlockEntity {
		public BlockEntityBody() { }
		public InventorySentry gearInv { get; set; }
		public string guidInv => "body-" + Pos.ToString();

		public override void Initialize(ICoreAPI api) {
			// Initialize gear slots if not done yet.
			base.Initialize(api);
			if (gearInv is null) {
				gearInv = new InventorySentry(guidInv, api);
			} else {
				gearInv.LateInitialize(guidInv, api);
			}
		}

		public override void OnBlockBroken(IPlayer byPlayer = null) {
			if (Api.World is IServerWorldAccessor) {
				gearInv.DropAll(Pos.ToVec3d().Add(0.5, 0.5, 0.5));
			}
			base.OnBlockBroken(byPlayer);
		}

		public override void ToTreeAttributes(ITreeAttribute tree) {
			base.ToTreeAttributes(tree);
			var inventory = tree.GetOrAddTreeAttribute("inventory");
			for (int i = 0; i < gearInv.Count; i++) {
				if (!gearInv[i].Empty) {
					inventory.SetItemstack($"IS_{i}", gearInv[i].Itemstack);
				}
			}
		}

		public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve) {
			base.FromTreeAttributes(tree, worldAccessForResolve);
			var inventory = tree.GetOrAddTreeAttribute("inventory");
			if (inventory.Count == 0) {
				return;
			}
			List<(int, ItemStack)> itemList = new List<(int, ItemStack)>();
			for (int i = 0; i < inventory.Count; i++) {
				if (inventory.HasAttribute($"IS_{i}")) {
					gearInv[i].Itemstack = inventory.GetItemstack($"IS_{i}");
					gearInv[i].MarkDirty();
				}
			}
		}
	}

	public class BlockBody : Block {
		public BlockBody() { }

		public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1) {
			return null;
		}

		public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer) {
			return new BlockDropItemStack[0];
		}
	}
}