using Vintagestory.API.Common.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using System;
using System.Collections.Generic;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VSKingdom {
	public class EntityBehaviorDecayBody : EntityBehavior {
		public EntityBehaviorDecayBody(Entity entity) : base(entity) { this.ServerAPI = entity.Api as ICoreServerAPI; }
		protected ICoreServerAPI ServerAPI;
		protected ITreeAttribute decayTree;
		protected JsonObject typeAttributes;
		protected bool DiedNaturally;
		protected bool HasAnyInvLeft;
		protected bool DiedInABattle;
		protected double HoursDecayTime { get; set; }
		protected double TotalHoursDead { get => decayTree.GetDouble("totalHoursDead"); set => decayTree.SetDouble("totalHoursDead", value); }
		protected string[] SkeletonBodies { get; set; }
		
		public override string PropertyName() {
			return "SoldierDecayBody";
		}

		public override void Initialize(EntityProperties properties, JsonObject typeAttributes) {
			base.Initialize(properties, typeAttributes);
			DiedNaturally = true;
			HasAnyInvLeft = false;
			DiedInABattle = false;
			(entity as EntityAgent).AllowDespawn = false;
			this.typeAttributes = typeAttributes;
			HoursDecayTime = typeAttributes["hoursToDecay"].AsFloat(96);
			SkeletonBodies = typeAttributes["skeletonBody"].AsArray(new string[] { "humanoid1", "humanoid2" });
			decayTree = entity.WatchedAttributes.GetTreeAttribute("decay");
			if (decayTree is null) {
				entity.WatchedAttributes.SetAttribute("decay", decayTree = new TreeAttribute());
				TotalHoursDead = entity.World.Calendar.TotalHours;
			}
		}

		public override void OnGameTick(float deltaTime) {
			base.OnGameTick(deltaTime);
			if (!entity.Alive && TotalHoursDead + HoursDecayTime < entity.World.Calendar.TotalHours) {
				DecayNow(entity as EntityAgent);
			}
		}

		public void DecayNow(EntityAgent entity) {
			if (DiedInABattle && CanRespawnAtOutpost()) {
				entity.AllowDespawn = false;
				return;
			}
			if (entity.AllowDespawn || !DiedNaturally) {
				return;
			}
			var blockAccessor = entity.World.BlockAccessor;
			double x = entity.ServerPos.X + entity.SelectionBox.X1 - entity.OriginSelectionBox.X1;
			double y = entity.ServerPos.Y + entity.SelectionBox.Y1 - entity.OriginSelectionBox.Y1;
			double z = entity.ServerPos.Z + entity.SelectionBox.Z1 - entity.OriginSelectionBox.Z1;
			double d = entity.ServerPos.Dimension;

			BlockPos bonePos = new BlockPos((int)x, (int)y, (int)z, (int)d);
			Random rnd = new Random();
			Block skeletonBlock = entity.World.GetBlock(new AssetLocation(LangUtility.Get("body-" + SkeletonBodies[rnd.Next(0, SkeletonBodies.Length)])));
			Block exblock = blockAccessor.GetBlock(bonePos);
			bool placedBlock = false;
			// Ensure the blocks here are replaceable like grass or something.
			if (exblock.IsReplacableBy(new BlockRequireSolidGround())) {
				blockAccessor.SetBlock(skeletonBlock.BlockId, bonePos);
				blockAccessor.MarkBlockDirty(bonePos);
				placedBlock = true;
			} else {
				foreach (BlockFacing facing in BlockFacing.HORIZONTALS) {
					facing.IterateThruFacingOffsets(bonePos);
					exblock = blockAccessor.GetBlock(bonePos);
					if (exblock.IsReplacableBy(new BlockRequireSolidGround())) {
						blockAccessor.SetBlock(skeletonBlock.BlockId, bonePos);
						blockAccessor.MarkBlockDirty(bonePos);
						placedBlock = true;
						break;
					}
				}
			}
			// Spawn the body block here if it was placed, drop all items if not possible.
			if (placedBlock && entity.WatchedAttributes.HasAttribute("inventory")) {
				// Initialize BlockEntityBody here and put stuff into it.
				if (blockAccessor.GetBlockEntity(bonePos) is BlockEntityBody decblock) {
					// Get the inventory of the person who died if they have one.
					for (int i = 0; i < entity.GearInventory.Count; i++) {
						entity.GearInventory[i].TryPutInto(entity.World, decblock.gearInv[i], entity.GearInventory[i].StackSize);
					}
				}
			} else {
				foreach (ItemSlot item in entity.GearInventory) {
					if (!item.Empty) {
						entity.Api.World.SpawnItemEntity(item.Itemstack, entity.ServerPos.XYZ);
						item.Itemstack = null;
						item.MarkDirty();
					}
				}
			}
			entity.AllowDespawn = true;
		}

		public override void OnEntityDeath(DamageSource damageSourceForDeath) {
			base.OnEntityDeath(damageSourceForDeath);
			TotalHoursDead = entity.World.Calendar.TotalHours;
			if (damageSourceForDeath is null) {
				DiedNaturally = false;
			}
			if (entity is EntitySentry thisEnt) {
				HasAnyInvLeft = !thisEnt.GearInventory.Empty;
			}
			EnumDamageSource source = damageSourceForDeath.Source;
			if (source == EnumDamageSource.Entity || source == EnumDamageSource.Player) {
				if (damageSourceForDeath.CauseEntity is EntityHumanoid) {
					string thisKingdom = entity.WatchedAttributes?.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") ?? "00000000";
					string thatKingdom = damageSourceForDeath.CauseEntity?.WatchedAttributes?.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") ?? "00000000";
					DiedInABattle = kingdomList.Find(kingdomMatch => kingdomMatch.KingdomGUID == thisKingdom)?.EnemiesGUID.Contains(thatKingdom) ?? false;
				}
			} else if (source == EnumDamageSource.Void) {
				(entity as EntityAgent).AllowDespawn = true;
			}
			if (!HasAnyInvLeft) {
				(entity as EntityAgent).AllowDespawn = true;
				DiedInABattle = false;
				DecayNow(entity as EntityAgent);
				return;
			}
			// Respawn the entity if they didn't die in a battle.
			if (!DiedInABattle) {
				CanRespawnAtOutpost();
			}
		}

		private bool CanRespawnAtOutpost() {
			var blockAccessor = entity.World.BlockAccessor;
			var blockPosition = entity.WatchedAttributes.GetTreeAttribute("loyalties").GetBlockPos("outpost_xyzd");
			BlockEntity blockEntity = blockAccessor.GetBlockEntity(blockPosition);
			if (blockAccessor.IsValidPos(blockPosition) && blockEntity is not null && blockEntity is BlockEntityPost outpost && outpost?.respawns > 0) {
				// Check valid positions to teleport entity to around the outpost.
				BlockPos aboveSpace = new BlockPos(blockPosition.X, blockPosition.Y + 1, blockPosition.Z, blockPosition.dimension);
				BlockPos belowSpace = new BlockPos(blockPosition.X, blockPosition.Y, blockPosition.Z, blockPosition.dimension);
				Block aboveBlock = blockAccessor.GetBlock(aboveSpace);
				Block belowBlock = blockAccessor.GetBlock(belowSpace);
				foreach (BlockFacing facing in BlockFacing.ALLFACES) {
					facing.IterateThruFacingOffsets(belowSpace);
					aboveBlock = blockAccessor.GetBlock(aboveSpace);
					facing.IterateThruFacingOffsets(belowSpace);
					belowBlock = blockAccessor.GetBlock(belowSpace);
					// Make sure it is safe to spawn here and we aren't going to suffocate or get stuck.
					if (belowBlock.IsReplacableBy(new BlockRequireSolidGround()) && aboveBlock.IsReplacableBy(new BlockRequireSolidGround())) {
						entity.TeleportTo(belowSpace);
						outpost?.UseRespawn();
						entity.Revive();
						// Stop decaying!
						DiedInABattle = false;
						(entity as EntityAgent).AllowDespawn = false;
						return true;
					}
				}
			}
			return false;
		}

		private byte[] kingdomData { get => ServerAPI.WorldManager.SaveGame.GetData("kingdomData"); }
		private byte[] cultureData { get => ServerAPI.WorldManager.SaveGame.GetData("cultureData"); }
		private List<Kingdom> kingdomList => kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);
		private List<Culture> cultureList => cultureData is null ? new List<Culture>() : SerializerUtil.Deserialize<List<Culture>>(cultureData);
	}
}