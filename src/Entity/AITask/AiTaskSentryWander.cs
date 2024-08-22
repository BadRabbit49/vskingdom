using System;
using System.Linq;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;

namespace VSKingdom {
	public class AiTaskSentryWander : AiTaskBase {
		public AiTaskSentryWander(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		protected bool cancelWanders;
		protected long failedWanders;
		protected long lastCheckedMs;
		protected long checkCooldown;
		protected float wanderChance;
		protected float targetRanges;
		protected float curMoveSpeed;
		protected float maxSquareDis;
		protected string curAnimation;
		protected Vec3d curTargetPos;
		protected NatFloat wanderRangeHor = NatFloat.createStrongerInvexp(3, 40);
		protected NatFloat wanderRangeVer = NatFloat.createStrongerInvexp(3, 10);
		protected int failedPathfinds {
			get => entity.WatchedAttributes.GetInt("failedConsecutivePathfinds", 0);
			set => entity.WatchedAttributes.SetInt("failedConsecutivePathfinds", value);
		}
		protected float wanderRange { get => entity.WatchedAttributes.GetFloat("wanderRange", 1f); }

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			this.checkCooldown = taskConfig["checkCooldown"].AsInt(1500);
			this.targetRanges = taskConfig["targetRanges"].AsFloat(0.12f);
			this.wanderChance = taskConfig["wanderChance"].AsFloat(0.15f);
			this.curMoveSpeed = taskConfig["curMoveSpeed"].AsFloat(0.03f);
			this.whenNotInEmotionState = taskConfig["whenNotInEmotionState"].AsString("aggressiveondamage|fleeondamage");
		}

		public override bool ShouldExecute() {
			if (!entity.ruleOrder[0]) {
				return false;
			}
			if (lastCheckedMs + checkCooldown > entity.World.ElapsedMilliseconds) {
				return false;
			}
			lastCheckedMs = entity.World.ElapsedMilliseconds;
			if (entity.ruleOrder[1] || entity.ruleOrder[5] || !EmotionStatesSatisifed()) {
				return false;
			}
			if (rand.NextDouble() > wanderChance) {
				failedWanders = 0;
				return false;
			}
			maxSquareDis = maxSquareDis = entity.cachedData.postRange * entity.cachedData.postRange;
			if (entity.ruleOrder[6] || entity.ServerPos.SquareDistanceTo(entity.cachedData.postBlock) > maxSquareDis) {
				curTargetPos = entity.cachedData.postBlock.Clone();
			} else if (entity.InLava || ((entity.Swimming || entity.FeetInLiquid) && entity.World.Rand.NextDouble() < 0.04f)) {
				curTargetPos = LeaveTheWatersTarget();
			} else {
				curTargetPos = LoadNextWanderTarget();
			}
			return curTargetPos != null;
		}

		public override void StartExecute() {
			base.StartExecute();
			cancelWanders = false;
			wanderRangeHor = NatFloat.createInvexp(1f, wanderRange);
			wanderRangeVer = NatFloat.createInvexp(1f, wanderRange / 2f);
			MoveAnimation();
			bool ok = pathTraverser.WalkTowards(curTargetPos, curMoveSpeed, targetRanges, OnGoals, OnStuck);
		}

		public override bool ContinueExecute(float dt) {
			if (cancelWanders || !entity.ruleOrder[0] || entity.ruleOrder[1] || entity.ruleOrder[5] || !EmotionStatesSatisifed()) {
				cancelWanders = true;
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
			cooldownUntilMs = entity.World.ElapsedMilliseconds + mincooldown + entity.World.Rand.Next(maxcooldown - mincooldown);
			StopAnimation();
			if (entity.ruleOrder[6] && entity.ServerPos.SquareDistanceTo(entity.cachedData.postBlock) < maxSquareDis) {
				HasReturnedTo();
			}
		}

		public virtual void PauseExecute(EntityAgent entity) {
			cancelWanders = true;
			MoveAnimation();
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
		// - ✔ Try not to fall from very large heights if entity has FallDamageOn.
		// - ✔ Must be above a block the entity can stand on.
		// - ✔ If failed search is high, reduce wander range.
		// - ✘ If wander ranges are not met.
		private Vec3d LoadNextWanderTarget() {
			bool canFallDamage = entity.Api.World.Config.GetAsBool("FallDamageOn");
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
			cancelWanders = true;
			failedWanders++;
			StopAnimation();
		}

		private void OnGoals() {
			pathTraverser.Retarget();
			failedWanders = 0;
		}

		private void NoPaths() {
			cancelWanders = true;
		}

		private int ToFloor(int x, int y, int z) {
			int tries = 5;
			while (tries-- > 0) {
				if (world.BlockAccessor.IsSideSolid(x, y, z, BlockFacing.UP)) { return y + 1; }
				y--;
			}
			return -1;
		}

		private void HasReturnedTo() {
			entity.ruleOrder[6] = false;
			SentryOrdersToServer updatedOrders = new SentryOrdersToServer() { entityUID = entity.EntityId, returning = false, usedorder = false };
			IServerPlayer nearestPlayer = entity.ServerAPI.World.NearestPlayer(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z) as IServerPlayer;
			entity.ServerAPI?.Network.GetChannel("sentrynetwork").SendPacket<SentryOrdersToServer>(updatedOrders, nearestPlayer);
		}

		private void MoveAnimation() {
			if (cancelWanders) {
				StopAnimation();
				return;
			} else if (entity.Swimming) {
				curMoveSpeed = entity.cachedData.walkSpeed * GlobalConstants.WaterDrag;
				entity.AnimManager.StopAnimation(curAnimation);
				curAnimation = new string(entity.cachedData.swimAnims);
			} else {
				curMoveSpeed = entity.cachedData.walkSpeed;
				entity.AnimManager.StopAnimation(curAnimation);
				curAnimation = new string(entity.cachedData.walkAnims);
			}
			entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = curAnimation, Code = curAnimation, BlendMode = EnumAnimationBlendMode.Average, MulWithWalkSpeed = true, EaseOutSpeed = 999f }.Init());
		}

		private void StopAnimation() {
			curMoveSpeed = 0;
			if (curAnimation != null) {
				entity.AnimManager.StopAnimation(curAnimation);
			}
			entity.AnimManager.StopAnimation(entity.cachedData.walkAnims);
			entity.AnimManager.StopAnimation(entity.cachedData.swimAnims);
		}
	}
}