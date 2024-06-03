using Vintagestory.API.Common.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class EntityBehaviorDecayBody : EntityBehavior {
		public EntityBehaviorDecayBody(Entity entity) : base(entity) { }
		protected ITreeAttribute decayTree;
		protected JsonObject typeAttributes;
		protected bool DiedNaturally;
		protected bool HasAnyInvLeft;
		protected double HoursDecayTime { get; set; }
		protected double TotalHoursDead {
			get => decayTree.GetDouble("totalHoursDead");
			set => decayTree.SetDouble("totalHoursDead", value);
		}
		
		public override string PropertyName() {
			return "SoldierDecayBody";
		}

		public override void Initialize(EntityProperties properties, JsonObject typeAttributes) {
			base.Initialize(properties, typeAttributes);
			DiedNaturally = true;
			HasAnyInvLeft = false;
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
				DecayNow();
			}
		}

		public void DecayNow() {
			if ((entity as EntityAgent).AllowDespawn) {
				return;
			}
			if (!DiedNaturally) {
				return;
			}

			(entity as EntityAgent).AllowDespawn = true;

			if (typeAttributes["skeleton"].Exists) {
				BlockEntity decblock = entity.World.ClassRegistry.CreateBlockEntity(typeAttributes["skeleton"].AsString());
				if (decblock is BlockEntityBody bodyblock) {
					double x = entity.ServerPos.X + entity.SelectionBox.X1 - entity.OriginSelectionBox.X1;
					double y = entity.ServerPos.Y + entity.SelectionBox.Y1 - entity.OriginSelectionBox.Y1;
					double z = entity.ServerPos.Z + entity.SelectionBox.Z1 - entity.OriginSelectionBox.Z1;
					double d = entity.ServerPos.Dimension;

					BlockPos bonepos = new BlockPos((int)x, (int)y, (int)z, (int)d);
					var bl = entity.World.BlockAccessor;
					Block exblock = bl.GetBlock(bonepos);

					if (exblock.IsReplacableBy(new BlockRequireSolidGround())) {
						bl.SetBlock(bodyblock.Block.BlockId, bonepos);
						bl.MarkBlockDirty(bonepos);
					} else {
						foreach (BlockFacing facing in BlockFacing.HORIZONTALS) {
							facing.IterateThruFacingOffsets(bonepos);
							exblock = entity.World.BlockAccessor.GetBlock(bonepos);
							if (exblock.IsReplacableBy(new BlockRequireSolidGround())) {
								entity.World.BlockAccessor.SpawnBlockEntity(typeAttributes["skeleton"].AsString(), bonepos);
								break;
							} else {
								if (entity is EntitySentry) {
									(entity as EntitySentry).gearInv.DropAll(entity.ServerPos.AsBlockPos.ToVec3d().Add(0.5, 0.5, 0.5));
								}
							}
						}
					}
					// Get the inventory of the person who died if they have one.
					if (entity.WatchedAttributes.HasAttribute("inventory") && entity is EntitySentry thisEnt) {
						for (int i = 0; i < thisEnt.gearInv.Count; i++) {
							thisEnt.gearInv[i].TryPutInto(entity.Api.World, bodyblock.gearInv[i], thisEnt.gearInv[i].StackSize);
						}
					}
				}
			}
			
			Vec3d pos = entity.SidedPos.XYZ;
			pos.Y += entity.Properties.DeadCollisionBoxSize.Y / 2;
			entity.World.SpawnParticles(new EntityCubeParticles(entity.World, entity.EntityId, pos, 0.15f, (int)(40 + entity.Properties.DeadCollisionBoxSize.X * 60), 0.4f, 1f));
		}

		public override void OnEntityDeath(DamageSource damageSourceForDeath) {
			base.OnEntityDeath(damageSourceForDeath);
			TotalHoursDead = entity.World.Calendar.TotalHours;
			if (damageSourceForDeath is null) {
				DiedNaturally = false;
			}
			if (damageSourceForDeath?.Source == EnumDamageSource.Void) {
				(entity as EntityAgent).AllowDespawn = true;
			}
			if (entity is EntitySentry thisEnt) {
				HasAnyInvLeft = !thisEnt.GearInventory.Empty;
			}
		}
	}
}