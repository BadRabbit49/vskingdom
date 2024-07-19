using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;

namespace VSKingdom {
	public class AiTaskSentryWander : AiTaskBase {
		public AiTaskSentryWander(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		protected bool cancelWander;
		protected bool doorIsBehind;
		protected long failedWanders;
		protected long lastInRangeMs;
		protected float execChance;
		protected float maxHeights;
		protected float targetDist;
		protected Vec3d mainTarget;
		protected NatFloat wanderRangeHor = NatFloat.createStrongerInvexp(3, 40);
		protected NatFloat wanderRangeVer = NatFloat.createStrongerInvexp(3, 10);

		protected int FailedPathfinds {
			get => entity.WatchedAttributes.GetInt("failedConsecutivePathfinds", 0);
			set => entity.WatchedAttributes.SetInt("failedConsecutivePathfinds", value);
		}

		protected float WanderRangeMul { get => entity.WatchedAttributes.GetFloat("wanderRangeMul", 1); }
		protected Vec3d postBlock { get => entity.Loyalties.GetBlockPos("outpost_xyzd").ToVec3d(); }

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			targetDist = taskConfig["targetRanges"].AsFloat(0.12f);
			maxHeights = taskConfig["wanderHeight"].AsFloat(7f);
			execChance = taskConfig["wanderChance"].AsFloat(0.005f);
		}

		public override bool ShouldExecute() {
			if (!entity.ruleOrder[0] || entity.ruleOrder[1] || entity.ruleOrder[6]) {
				cancelWander = true;
				return false;
			}
			if (rand.NextDouble() > (double)((failedWanders > 0) ? (1f - execChance * 4f * (float)failedWanders) : execChance)) {
				failedWanders = 0;
				return false;
			}
			if (entity.ruleOrder[6]) {
				if (entity.ServerPos.XYZ.SquareDistanceTo(postBlock) > entity.postRange) {
					// If after 2 minutes still not at spawn and no player nearby, teleport.
					if (entity.World.ElapsedMilliseconds - lastInRangeMs > 1000 * 60 * 2 && entity.World.GetNearestEntity(entity.ServerPos.XYZ, 15, 15, (e) => e is EntityPlayer) is null) {
						entity.TeleportTo(postBlock);
					}
					entity.ServerAPI.World.GetEntityById(entity.EntityId)?.GetBehavior<EntityBehaviorLoyalties>()?.SetCommand("command_return", true);
					mainTarget = postBlock.Clone();
					return true;
				} else {
					entity.ServerAPI.World.GetEntityById(entity.EntityId)?.GetBehavior<EntityBehaviorLoyalties>()?.SetCommand("command_return", false);
				}
				lastInRangeMs = entity.World.ElapsedMilliseconds;
			}
			mainTarget = LoadNextWanderTarget();
			return mainTarget != null;
		}

		public override void StartExecute() {
			base.StartExecute();
			cancelWander = false;
			wanderRangeHor = NatFloat.createInvexp(3f, (float)entity.postRange);
			bool ok = pathTraverser.WalkTowards(mainTarget, (float)entity.walkSpeed, targetDist, OnGoals, OnStuck);
		}

		public override bool ContinueExecute(float dt) {
			// If we are a climber dude and encountered a wall, let's not try to get behind the wall.
			// We do that by removing the coord component that would make the entity want to walk behind the wall.
			if (entity.Controls.IsClimbing && entity.Properties.CanClimbAnywhere && entity.ClimbingIntoFace != null) {
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
			if (mainTarget.HorizontalSquareDistanceTo(entity.ServerPos.X, entity.ServerPos.Z) < 0.5) {
				pathTraverser.Stop();
				return false;
			}
			return !cancelWander;
		}

		public override void FinishExecute(bool cancelled) {
			base.FinishExecute(cancelled);
            if (cancelled) {
				entity.Controls.StopAllMovement();
				pathTraverser.Stop();
			}
		}

		// Requirements:
		// - ✔ Try to not move a lot vertically.
		// - ✔ Try not to fall from very large heights if entity has FallDamageOn.
		// - ✔ Must be above a block the entity can stand on.
		// - ✔ If failed searches is high, reduce wander range.
		private Vec3d LoadNextWanderTarget() {
			bool canFallDamage = entity.Api.World.Config.GetAsBool("FallDamageOn");
			int num = 9;
			float wanderRangeMul = WanderRangeMul;
			Vec4d bestTarget = null;
			Vec4d currTarget = new Vec4d();
			if (FailedPathfinds > 10) {
				wanderRangeMul = Math.Max(0.1f, WanderRangeMul * 0.9f);
			} else {
				wanderRangeMul = Math.Min(1f, WanderRangeMul * 1.1f);
				if (rand.NextDouble() < 0.05) {
					wanderRangeMul = Math.Min(1f, WanderRangeMul * 1.5f);
				}
			}
			if (rand.NextDouble() < 0.05) {
				wanderRangeMul *= 3f;
			}
			while (num-- > 0) {
				double dx = wanderRangeHor.nextFloat() * (rand.Next(2) * 2 - 1) * wanderRangeMul;
				double dy = wanderRangeHor.nextFloat() * (rand.Next(2) * 2 - 1) * wanderRangeMul;
				double dz = wanderRangeHor.nextFloat() * (rand.Next(2) * 2 - 1) * wanderRangeMul;
				currTarget.X = entity.ServerPos.X + dx;
				currTarget.Y = entity.ServerPos.Y + dy;
				currTarget.Z = entity.ServerPos.Z + dz;
				currTarget.W = 1.0;
				// Return to spawn area or outpost if there is one.
				if (entity.ruleOrder[6]) {
					currTarget.W = 1.0 - (double)currTarget.SquareDistanceTo(postBlock) / entity.postRange;
				}
				currTarget.Y = MoveDownToFloor((int)currTarget.X, (int)currTarget.Y, (int)currTarget.Z);
				if (currTarget.Y < 0.0) {
					currTarget.W = 0.0;
				} else {
					if (canFallDamage) {
						// Lets make a straight line plot to see if we would fall off a cliff.
						bool mustStop = false;
						bool willFall = false;
						float angleHor = (float)Math.Atan2(dx, dz) + GameMath.PIHALF;
						Vec3d target1BlockAhead = currTarget.XYZ.Ahead(1, 0, angleHor);
						// Otherwise they are forever stuck if they stand over the edge.
						Vec3d startAhead = entity.ServerPos.XYZ.Ahead(1, 0, angleHor);
						// Draw a line from here to there and check ahead to see if we will fall.
						GameMath.BresenHamPlotLine2d((int)startAhead.X, (int)startAhead.Z, (int)target1BlockAhead.X, (int)target1BlockAhead.Z, (x, z) => {
							if (mustStop) {
								return;
							}
							int nowY = MoveDownToFloor(x, (int)startAhead.Y, z);
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
					if (currTarget.W >= 1.0) {
						break;
					}
				}
			}
			if (bestTarget.W > 0.0) {
				FailedPathfinds = Math.Max(FailedPathfinds - 3, 0);
				return bestTarget.XYZ;
			}
			FailedPathfinds++;
			return null;
		}
		
		private void OnStuck() {
			cancelWander = true;
			failedWanders++;
		}

		private void OnGoals() {
			cancelWander = true;
			failedWanders = 0;
		}

		private int MoveDownToFloor(int x, int y, int z) {
			int tries = 5;
			while (tries-- > 0) {
				if (world.BlockAccessor.IsSideSolid(x, y, z, BlockFacing.UP)) {
					return y + 1;
				}
				y--;
			}
			return -1;
		}
	}
}