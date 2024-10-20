using System;
using System.Linq;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using System.Collections.Generic;

namespace VSKingdom {
	public class AiTaskZombieWander : AiTaskBase {
		public AiTaskZombieWander(EntityZombie entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntityZombie entity;
		#pragma warning restore CS0108
		protected Boolean cancelWander;
		protected Int32 failedWander;
		protected Int64 durationOfMs;
		protected Single wanderChance;
		protected Single wanderRanges;
		protected Single targetRanges;
		protected Single currentSpeed;
		protected String prvAnimation;
		protected Vec3d curTargetPos;
		protected NatFloat wanderRangeHor = NatFloat.createStrongerInvexp(3, 40);
		protected NatFloat wanderRangeVer = NatFloat.createStrongerInvexp(3, 10);
		protected int failedPathfinds {
			get => entity.WatchedAttributes.GetInt("failedConsecutivePathfinds", 0);
			set => entity.WatchedAttributes.SetInt("failedConsecutivePathfinds", value);
		}

		public override void AfterInitialize() {
			world = entity.World;
			bhEmo = entity.GetBehavior<EntityBehaviorEmotionStates>();
			pathTraverser = entity.GetBehavior<EntityBehaviorTaskAI>().PathTraverser;
		}

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			this.durationOfMs = taskConfig["lastCooldown"].AsInt(1500);
			this.targetRanges = taskConfig["targetRanges"].AsFloat(0.12f);
			this.wanderChance = taskConfig["wanderRanges"].AsFloat(6f);
			this.wanderChance = taskConfig["wanderChance"].AsFloat(0.15f);
			this.whenNotInEmotionState = taskConfig["whenNotInEmotionState"].AsString("aggressiveondamage|fleeondamage");
		}

		public override bool ShouldExecute() {
			if (cooldownUntilMs > entity.World.ElapsedMilliseconds) {
				return false;
			}
			cooldownUntilMs = entity.World.ElapsedMilliseconds + durationOfMs;
			if (!EmotionStatesSatisifed()) {
				return false;
			}
			if (rand.NextDouble() > wanderChance) {
				failedWander = 0;
				return false;
			}
			if (entity.InLava || ((entity.Swimming || entity.FeetInLiquid) && entity.World.Rand.NextDouble() < 0.04f)) {
				curTargetPos = LeaveTheWatersTarget();
			} else {
				curTargetPos = LoadNextWanderTarget();
			}
			return curTargetPos != null;
		}

		public override void StartExecute() {
			base.StartExecute();
			cancelWander = false;
			wanderRangeHor = NatFloat.createInvexp(1f, wanderRanges);
			wanderRangeVer = NatFloat.createInvexp(1f, wanderRanges / 2f);
			bool ok = pathTraverser.WalkTowards(curTargetPos, entity.walkSpeed, targetRanges, OnGoals, OnStuck);
		}

		public override bool ContinueExecute(float dt) {
			if (cancelWander || !EmotionStatesSatisifed()) {
				return false;
			}
			// If we are a climber dude and encountered a wall, let's not try to get behind the wall.
			// We do that by removing the coord component that would make the entity want to walk behind the wall.
			if (entity.ServerControls.IsClimbing && entity.Properties.CanClimbAnywhere && entity.ClimbingIntoFace != null) {
				BlockFacing facing = entity.ClimbingIntoFace;
				if (Math.Sign(facing.Normali.X) == Math.Sign(pathTraverser.CurrentTarget.X - entity.ServerPos.X)) {
					pathTraverser.CurrentTarget.X = entity.ServerPos.X;
				}
				if (Math.Sign(facing.Normali.Y) == Math.Sign(pathTraverser.CurrentTarget.Y - entity.ServerPos.Y)) {
					pathTraverser.CurrentTarget.Y = entity.ServerPos.Y;
				}
				if (Math.Sign(facing.Normali.Z) == Math.Sign(pathTraverser.CurrentTarget.Z - entity.ServerPos.Z)) {
					pathTraverser.CurrentTarget.Z = entity.ServerPos.Z;
				}
			}
			// If the entity is close enough to the primary target then leave it there.
			return !(curTargetPos.HorizontalSquareDistanceTo(entity.ServerPos.X, entity.ServerPos.Z) < 0.5);
		}

		public override void FinishExecute(bool cancelled) {
			cooldownUntilMs = entity.World.ElapsedMilliseconds + durationOfMs;
		}

		private Vec3d LeaveTheWatersTarget() {
			Vec3d exitPosition = new Vec3d();
			BlockPos pos = new BlockPos(Dimensions.NormalWorld);
			exitPosition.Y = entity.ServerPos.Y;
			int tries = 6;
			int px = (int)entity.ServerPos.X;
			int pz = (int)entity.ServerPos.Z;
			IBlockAccessor blockAccessor = entity.World.BlockAccessor;
			while (tries-- > 0) {
				pos.X = px + rand.Next(21) - 10;
				pos.Z = pz + rand.Next(21) - 10;
				pos.Y = blockAccessor.GetTerrainMapheightAt(pos);
				Cuboidf[] blockBoxes = blockAccessor.GetBlock(pos).GetCollisionBoxes(blockAccessor, pos);
				pos.Y--;
				Cuboidf[] belowBoxes = blockAccessor.GetBlock(pos).GetCollisionBoxes(blockAccessor, pos);
				if ((blockBoxes == null || blockBoxes.Max((cuboid) => cuboid.Y2) <= 1f) && (belowBoxes != null && belowBoxes.Length > 0)) {
					exitPosition.Set(pos.X + 0.5, pos.Y + 1, pos.Z + 0.5);
					return exitPosition;
				}
			}
			return null;
		}

		// Requirements:
		// - ✔ Try to not move a lot vertically.
		// - ✔ Try not to fall from very large heights if entity has Allowed_SentryTripping.
		// - ✔ Must be above a block the entity can stand on.
		// - ✔ If failed search is high, reduce wander range.
		// - ✘ If wander ranges are not met.
		private Vec3d LoadNextWanderTarget() {
			bool canFallDamage = entity.Api.World.Config.GetAsBool(FallDamages);
			int num = 9;
			float multiplier = 1f;
			Vec4d bestTarget = null;
			Vec4d currTarget = new Vec4d();
			if (failedPathfinds > 10) {
				multiplier = NatFloat.createInvexp(0.1f, 0.9f).nextFloat(1f, rand);
			}
			while (num-- > 0) {
				double dx = wanderRangeHor.nextFloat(multiplier, rand) * (rand.Next(2) * 2 - 1);
				double dy = wanderRangeVer.nextFloat(multiplier, rand) * (rand.Next(2) * 2 - 1);
				double dz = wanderRangeHor.nextFloat(multiplier, rand) * (rand.Next(2) * 2 - 1);
				currTarget.X = entity.ServerPos.X + dx;
				currTarget.Y = entity.ServerPos.Y + dy;
				currTarget.Z = entity.ServerPos.Z + dz;
				currTarget.W = 1.0;
				currTarget.Y = ToFloor((int)currTarget.X, (int)currTarget.Y, (int)currTarget.Z);
				if (currTarget.Y < 0.0) {
					currTarget.W = 0.0;
				} else {
					if (canFallDamage) {
						// Lets make a straight line plot to see if we would fall off a cliff.
						bool mustStop = false;
						bool willFall = false;
						float angleHor = (float)Math.Atan2(dx, dz) + GameMath.PIHALF;
						Vec3d blockAhead = currTarget.XYZ.Ahead(1, 0, angleHor);
						// Otherwise they are forever stuck if they stand over the edge.
						Vec3d startAhead = entity.ServerPos.XYZ.Ahead(1, 0, angleHor);
						// Draw a line from here to there and check ahead to see if we will fall.
						GameMath.BresenHamPlotLine2d((int)startAhead.X, (int)startAhead.Z, (int)blockAhead.X, (int)blockAhead.Z, (x, z) => {
							if (mustStop) { return; }
							int nowY = ToFloor(x, (int)startAhead.Y, z);
							// Not more than 4 blocks down.
							if (nowY < 0 || startAhead.Y - nowY > 4) {
								willFall = true;
								mustStop = true;
							}
							// Not more than 2 blocks up.
							if (nowY - startAhead.Y > 2) {
								mustStop = true;
							}
							startAhead.Y = nowY;
						});
						if (willFall) {
							currTarget.W = 0.0;
						}
					}
				}
				if (entity.World.BlockAccessor.GetBlock(new BlockPos((int)currTarget.X, (int)currTarget.Y, (int)currTarget.Z, entity.Pos.Dimension), 2).IsLiquid()) {
					currTarget.W /= 2.0;
				}
				if (currTarget.W > 0.0) {
					for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++) {
						BlockFacing blockFacing = BlockFacing.HORIZONTALS[i];
						if (entity.World.BlockAccessor.IsSideSolid((int)currTarget.X + blockFacing.Normali.X, (int)currTarget.Y, (int)currTarget.Z + blockFacing.Normali.Z, blockFacing.Opposite)) {
							currTarget.W *= 0.5;
						}
					}
				}
				if (bestTarget == null || currTarget.W > bestTarget.W) {
					bestTarget = new Vec4d(currTarget.X, currTarget.Y, currTarget.Z, currTarget.W);
					if (currTarget.W >= 1.0) { break; }
				}
			}
			if (bestTarget.W > 0.0) {
				failedPathfinds = Math.Max(failedPathfinds - 3, 0);
				return bestTarget.XYZ;
			}
			failedPathfinds++;
			return null;
		}

		private void OnStuck() {
			cancelWander = true;
			failedWander++;
		}

		private void OnGoals() {
			pathTraverser.Retarget();
			failedWander = 0;
		}

		private void NoPaths() {
			cancelWander = true;
		}

		private int ToFloor(int x, int y, int z) {
			for (int i = 5; i > 0; i--) {
				if (world.BlockAccessor.IsSideSolid(x, y, z, BlockFacing.UP)) {
					return y + 1;
				}
				y--;
			}
			return -1;
		}

		private void Animate(bool forcedSprint) {
			String anims = entity.idleAnims;
			if (!pathTraverser.Active) {
				entity.AnimManager.StopAnimation(prvAnimation);
			} else if (entity.Swimming) {
				anims = entity.swimAnims;
			} else if (entity.FeetInLiquid) {
				anims = entity.walkAnims;
			} else if (entity.Controls.Sneak) {
				anims = entity.duckAnims;
			} else if (forcedSprint) {
				anims = entity.moveAnims;
			} else if (currentSpeed > 0.01f) {
				anims = entity.walkAnims;
			} else if (currentSpeed < 0.01f) {
				entity.AnimManager.StopAnimation(prvAnimation);
			}
			if (!entity.AnimManager.IsAnimationActive(anims)) {
				entity.AnimManager.StartAnimation(new AnimationMetaData() {
					Animation = anims,
					Code = anims,
					BlendMode = EnumAnimationBlendMode.AddAverage,
					MulWithWalkSpeed = anims != entity.idleAnims,
					EaseInSpeed = 999f,
					EaseOutSpeed = 999f,
					ElementWeight = new Dictionary<string, float> {
						{ "UpperFootR", 2f },
						{ "UpperFootL", 2f },
						{ "LowerFootR", 2f },
						{ "LowerFootL", 2f }
					},
				}.Init());
			}
			prvAnimation = anims;
		}
	}
}
