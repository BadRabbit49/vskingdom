using System;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

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

		public void LootGear(EntitySentry killer, EntitySentry victim) {
			if (!entity.World.Config.GetAsBool("AllowLooting") || killer == null || victim == null) {
				return;
			}
			if (killer.ServerPos.DistanceTo(victim.ServerPos) > 4f) {
				return;
			}
			// If the entities were at war with eachother then loot will be dropped. Specifically their armor and what they had in their right hand slot.
			if (killer.cachedData.enemiesLIST.Contains(victim.cachedData.kingdomGUID) || killer.cachedData.kingdomGUID == GlobalCodes.banditryGUID) {
				// If the killer can, try looting the player corpse right away, take what is better.
				for (int i = 12; i < 14; i++) {
					try {
						float victimGearScore = 0f;
						float killerGearScore = 0f;
						if (!victim.GearInventory[i].Empty && victim.GearInventory[i]?.Itemstack?.Item is ItemWearable vGear) {
							victimGearScore = vGear.ProtectionModifiers.FlatDamageReduction * vGear.Durability;
						}
						if (!killer.GearInventory[i].Empty && victim.GearInventory[i]?.Itemstack?.Item is ItemWearable kGear) {
							killerGearScore = kGear.ProtectionModifiers.FlatDamageReduction * kGear.Durability;
						}
						if (victimGearScore > killerGearScore) {
							victim.GearInventory[i].TryFlipWith(killer.GearInventory[i]);
							killer.GearInvSlotModified(i);
							victim.GearInvSlotModified(i);
						}
					} catch { }
				}
				if (!victim.RightHandItemSlot.Empty) {
					string killerClass = killer.Properties.Attributes["baseClass"].AsString("melee").ToLower();
					if ((killerClass == "range" && victim.RightHandItemSlot.Itemstack.Item is ItemBow) || (killerClass == "melee" && victim.RightHandItemSlot.Itemstack.Item is not ItemBow)) {
						try {
							ItemStack victimWeapon = victim.RightHandItemSlot?.Itemstack ?? null;
							ItemStack killerWeapon = killer.RightHandItemSlot?.Itemstack ?? null;
							float victimWeapValue = victimWeapon != null ? (victimWeapon?.Collectible.Durability ?? 1f) * (victimWeapon?.Collectible.AttackPower ?? victimWeapon?.Collectible.Attributes?["damage"].AsFloat() ?? 1f) : 0f;
							float killerWeapValue = killerWeapon != null ? (killerWeapon?.Collectible.Durability ?? 1f) * (killerWeapon?.Collectible.AttackPower ?? killerWeapon?.Collectible.Attributes?["damage"].AsFloat() ?? 1f) : 0f;
							if (victimWeapValue > killerWeapValue) {
								victim.RightHandItemSlot.TryFlipWith(killer.RightHandItemSlot);
								killer.GearInvSlotModified(16);
								victim.GearInvSlotModified(16);
							}
						} catch { }
					}
				}
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
			Block skeletonBlock = entity.World.GetBlock(new AssetLocation(LangUtility.Get("body-" + entity.WatchedAttributes.GetString("deathSkeletons", "humanoid1"))));
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
				for (int i = 0; i < entity.GearInventory.Count; i++) {
					if (!entity.GearInventory[i].Empty) {
						entity.Api.World.SpawnItemEntity(entity.GearInventory[i].Itemstack, entity.ServerPos.XYZ);
						entity.GearInventory[i].Itemstack = null;
						entity.GearInventory[i].MarkDirty();
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
			if (entity is EntitySentry thisSentry) {
				HasAnyInvLeft = !thisSentry.GearInventory.Empty;
			}
			EnumDamageSource source = damageSourceForDeath.Source;
			if (source == EnumDamageSource.Entity || source == EnumDamageSource.Player) {
				if (damageSourceForDeath.GetCauseEntity() is EntityHumanoid && damageSourceForDeath.GetCauseEntity().WatchedAttributes.HasAttribute("kingdomGUID")) {
					DiedInABattle = (entity as EntitySentry)?.cachedData.enemiesLIST.Contains(damageSourceForDeath.GetCauseEntity().WatchedAttributes.GetString("kingdomGUID")) ?? false;
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
				bool canRespawn = CanRespawnAtOutpost();
				if (!canRespawn && entity is EntitySentry thisEnt && damageSourceForDeath.GetCauseEntity() is EntitySentry thatEnt) {
					LootGear(thatEnt, thisEnt);
				}
			}
		}

		private bool CanRespawnAtOutpost() {
			var blockAccessor = entity.World.BlockAccessor;
			var blockPosition = entity.WatchedAttributes.GetBlockPos("postBlock");
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
						outpost?.UseRespawn(1);
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
	}
}