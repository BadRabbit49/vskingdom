using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class AiTaskSentryWander : AiTaskBase {
		public AiTaskSentryWander(EntityAgent entity) : base(entity) { }
		
		protected bool finished;
		protected long failedWanders;
		protected long lastTimeInRangeMs;
		protected float moveSpeed = 0.035f;
		protected float wanderChance = 0.005f;
		protected float wanderRangeMin = 3f;
		protected float wanderRangeMax = 30f;
		protected float maxHeight = 7f;
		protected float targetDistance = 0.12f;
		protected Vec3d mainTarget;
		protected NatFloat wanderRangeHor = NatFloat.createStrongerInvexp(3, 40);
		protected NatFloat wanderRangeVer = NatFloat.createStrongerInvexp(3, 10);

		private ITreeAttribute loyalties;
		private bool wandering { get => loyalties.GetBool("command_wander"); }
		private bool following { get => loyalties.GetBool("command_follow"); }
		private bool returning { get => loyalties.GetBool("command_return"); }
		private float postRange { get => (float)loyalties.GetDouble("outpost_size", 30); }
		private Vec3d postBlock { get => loyalties.GetBlockPos("outpost_xyzd").ToVec3d(); }

		public float WanderRangeMul {
			get => entity.Attributes.GetFloat("wanderRangeMul", 1);
			set => entity.Attributes.SetFloat("wanderRangeMul", value);
		}

		public int FailedConsecutivePathfinds {
			get => entity.Attributes.GetInt("failedConsecutivePathfinds", 0);
			set => entity.Attributes.SetInt("failedConsecutivePathfinds", value);
		}

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			targetDistance = taskConfig["targetDistance"].AsFloat(0.12f);
			moveSpeed = taskConfig["movespeed"].AsFloat(0.035f);
			maxHeight = taskConfig["maxHeight"].AsFloat(7f);
			wanderChance = taskConfig["wanderChance"].AsFloat(0.005f);
			wanderRangeMin = taskConfig["wanderRangeMin"].AsFloat(3f);
			wanderRangeMax = taskConfig["wanderRangeMax"].AsFloat(30f);
			wanderRangeHor = NatFloat.createInvexp(wanderRangeMin, wanderRangeMax);
		}

		public override void OnEntitySpawn() {
			entity.Attributes.SetBlockPos("outpost_xyzd", entity.ServerPos.AsBlockPos);
		}

		public override void AfterInitialize() {
			base.AfterInitialize();
			loyalties = entity.WatchedAttributes.GetTreeAttribute("loyalties");
			pathTraverser = entity.GetBehavior<EntityBehaviorTaskAI>().PathTraverser;
			wanderRangeMax = postRange;
		}

		public override bool ShouldExecute() {
			if (!wandering || following) {
				finished = true;
				return false;
			}
			if (rand.NextDouble() > (double)((failedWanders > 0) ? (1f - wanderChance * 4f * (float)failedWanders) : wanderChance)) {
				failedWanders = 0;
				return false;
			}
			if (returning) {
				if (entity.ServerPos.XYZ.SquareDistanceTo(postBlock) > wanderRangeMax) {
					// If after 2 minutes still not at spawn and no player nearby, teleport.
					if (entity.World.ElapsedMilliseconds - lastTimeInRangeMs > 1000 * 60 * 2 && entity.World.GetNearestEntity(entity.ServerPos.XYZ, 15, 15, (e) => e is EntityPlayer) is null) {
						entity.TeleportTo(postBlock);
					}
					loyalties.SetBool("command_return", true);
					mainTarget = postBlock.Clone();
					return true;
				} else {
					loyalties.SetBool("command_return", false);
				}
				lastTimeInRangeMs = entity.World.ElapsedMilliseconds;
			}
			mainTarget = LoadNextWanderTarget();
			return mainTarget != null;
		}

		public override void StartExecute() {
			base.StartExecute();
			finished = false;
			wanderRangeMax = postRange;
			bool ok = pathTraverser.WalkTowards(mainTarget, moveSpeed, targetDistance, OnGoalReached, OnStuck);
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
			return !finished;
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
		// - ✔ If water habitat: Don't walk onto land.
		// - ✔ Try not to fall from very large heights. Try not to fall from any large heights if entity has FallDamage.
		// - ✔ If land habitat: Must be above a block the entity can stand on.
		// - ✔ if failed searches is high, reduce wander range.
		private Vec3d LoadNextWanderTarget() {
			bool canFallDamage = entity.Api.World.Config.GetAsBool("FallDamageOn");
			int num = 9;
			Vec4d bestTarget = null;
			Vec4d curTarget = new Vec4d();
			if (FailedConsecutivePathfinds > 10) {
				WanderRangeMul = Math.Max(0.1f, WanderRangeMul * 0.9f);
			} else {
				WanderRangeMul = Math.Min(1f, WanderRangeMul * 1.1f);
				if (rand.NextDouble() < 0.05) {
					WanderRangeMul = Math.Min(1f, WanderRangeMul * 1.5f);
				}
			}
			float wanderRangeMul = WanderRangeMul;
			if (rand.NextDouble() < 0.05) {
				wanderRangeMul *= 3f;
			}
			while (num-- > 0) {
				double dx = wanderRangeHor.nextFloat() * (rand.Next(2) * 2 - 1) * wanderRangeMul;
				double dy = wanderRangeHor.nextFloat() * (rand.Next(2) * 2 - 1) * wanderRangeMul;
				double dz = wanderRangeHor.nextFloat() * (rand.Next(2) * 2 - 1) * wanderRangeMul;
				curTarget.X = entity.ServerPos.X + dx;
				curTarget.Y = entity.ServerPos.Y + dy;
				curTarget.Z = entity.ServerPos.Z + dz;
				curTarget.W = 1.0;
				// Return to spawn area or outpost if there is one.
				if (returning) {
					curTarget.W = 1.0 - (double)curTarget.SquareDistanceTo(postBlock) / postRange;
				}
				curTarget.Y = MoveDownToFloor((int)curTarget.X, (int)curTarget.Y, (int)curTarget.Z);
				if (curTarget.Y < 0.0) {
					curTarget.W = 0.0;
				} else {
					if (canFallDamage) {
						// Lets make a straight line plot to see if we would fall off a cliff.
						bool mustStop = false;
						bool willFall = false;
						float angleHor = (float)Math.Atan2(dx, dz) + GameMath.PIHALF;
						Vec3d target1BlockAhead = curTarget.XYZ.Ahead(1, 0, angleHor);
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
							curTarget.W = 0.0;
						}
					}
				}
				if (entity.World.BlockAccessor.GetBlock(new BlockPos((int)curTarget.X, (int)curTarget.Y, (int)curTarget.Z, entity.Pos.Dimension), 2).IsLiquid()) {
					curTarget.W /= 2.0;
				}
				if (curTarget.W > 0.0) {
					for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++) {
						BlockFacing blockFacing = BlockFacing.HORIZONTALS[i];
						if (entity.World.BlockAccessor.IsSideSolid((int)curTarget.X + blockFacing.Normali.X, (int)curTarget.Y, (int)curTarget.Z + blockFacing.Normali.Z, blockFacing.Opposite)) {
							curTarget.W *= 0.5;
						}
					}
				}
				if (bestTarget == null || curTarget.W > bestTarget.W) {
					bestTarget = new Vec4d(curTarget.X, curTarget.Y, curTarget.Z, curTarget.W);
					if (curTarget.W >= 1.0) {
						break;
					}
				}
			}
			if (bestTarget.W > 0.0) {
				FailedConsecutivePathfinds = Math.Max(FailedConsecutivePathfinds - 3, 0);
				return bestTarget.XYZ;
			}
			FailedConsecutivePathfinds++;
			return null;
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

		private void OnStuck() {
			finished = true;
			failedWanders++;
		}

		private void OnGoalReached() {
			finished = true;
			failedWanders = 0;
		}
	}
}