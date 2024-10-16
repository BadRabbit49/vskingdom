using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Essentials;
using Vintagestory.GameContent;
using VSEssentialsMod.Entity.AI.Task;

namespace VSKingdom {
	public class SentryTraverser : PathTraverserBase {
		#pragma warning disable CS0108
		public EntitySentry entity;
		public Action OnGoals;
		public Action OnStuck;
		public Action NoPaths;
		#pragma warning restore CS0108
		public Int32 waypointsUntil = 0;
		public Int64 waypointsPrvMs = 0;
		public Vec3f curTargetVec = new Vec3f();
		public Vec3d prvTargetPos = new Vec3d(0, -2000, 0);
		public Vec3d prvTargetPrv = new Vec3d(0, -1000, 0);
		public List<Vec3d> curWaypoints;
		public List<Vec3d> newWaypoints;
		public Single minTurnAngle;
		public Single maxTurnAngle;
		public Single distChecksMs;
		public Single prvPosAccums;
		public Single prvPosDistSq;
		public Single curPosDistSq;
		public Single curMoveSpeed;
		public Double distToTarget;
		public String curIdleAnims = "idle";
		public String curWalkAnims = "walk";
		public String curMoveAnims = "move";
		public String curDuckAnims = "duck";
		public String curSwimAnims = "swim";
		public String curJumpAnims = "jump";
		public String curAnimation;
		private PathfindSystem systemsPathfinder;
		private PathfinderTask asyncSearchObject;
		private PathfindingAsync asyncPathfinder;
		private ModSystemBlockReinforcement reinforceSystem;
		private EntityBehaviorEmotionStates emotionalStates;
		public bool forcesSprint = false;
		public override bool Ready { get => curWaypoints != null && asyncSearchObject == null; }
		public override Vec3d CurrentTarget { get => curWaypoints[curWaypoints.Count - 1]; }
		
		public SentryTraverser(EntitySentry entity) : base(entity) {
			this.entity = entity;
			if (entity?.Properties.Server?.Attributes?.GetTreeAttribute("pathfinder") != null) {
				this.minTurnAngle = (float)entity.Properties.Server.Attributes.GetTreeAttribute("pathfinder").GetDecimal("minTurnAnglePerSec", 250);
				this.maxTurnAngle = (float)entity.Properties.Server.Attributes.GetTreeAttribute("pathfinder").GetDecimal("maxTurnAnglePerSec", 450);
			} else {
				this.minTurnAngle = 250;
				this.maxTurnAngle = 450;
			}
			this.systemsPathfinder = entity.World.Api.ModLoader.GetModSystem<PathfindSystem>();
			this.asyncPathfinder = entity.World.Api.ModLoader.GetModSystem<PathfindingAsync>();
			this.reinforceSystem = entity.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();
			this.emotionalStates = entity.GetBehavior<EntityBehaviorEmotionStates>();
		}

		public PathfinderTask PreparePathfinderTask(BlockPos startBlockPos, BlockPos targetBlockPos, int searchDepth = 999, int mhdistanceTolerance = 0) {
			var bh = entity.GetBehavior<EntityBehaviorControlledPhysics>();
			float stepHeight = bh == null ? 0.6f : bh.stepHeight;
			bool avoidFall = entity.Properties.FallDamage && entity.Properties.Attributes?["reckless"].AsBool(false) != true;
			// Fast moving entities cannot safely fall so far (might miss target block below due to outward drift).
			int maxFallHeight = avoidFall ? 4 - (int)(curMoveSpeed * 30) : 12;
			return new PathfinderTask(startBlockPos, targetBlockPos, maxFallHeight, stepHeight, entity.CollisionBox, searchDepth, mhdistanceTolerance);
		}

		public override bool NavigateTo(Vec3d target, float movingSpeed, float targetDistance, Action OnGoals, Action OnStuck, bool giveUpWhenNoPath = false, int searchDepth = 10000, int mhdistanceTolerance = 0) {
			this.target = target;
			this.targetDistance = targetDistance * 0.9f;
			this.OnStuck = OnStuck;
			this.OnGoals = OnGoals;
			this.forcesSprint = movingSpeed >= entity.cachedData.moveSpeed;
			CalcMovement();
			CalcAnimated();
			BlockPos startBlockPos = entity.ServerPos.AsBlockPos;
			if (entity.World.BlockAccessor.IsNotTraversable(startBlockPos)) {
				PathsNotFound();
				return false;
			}
			FindPath(startBlockPos, target.AsBlockPos, searchDepth, mhdistanceTolerance);
			return PathsAreFound();
		}

		public override bool NavigateTo_Async(Vec3d target, float movingSpeed, float targetDistance, Action OnGoals, Action OnStuck, Action onNoPath = null, int searchDepth = 999, int mhdistanceTolerance = 0) {
			if (this.asyncSearchObject != null) {
				return false;
			}
			this.target = target;
			this.targetDistance = targetDistance * 0.9f;
			this.NoPaths = onNoPath;
			this.OnGoals = OnGoals;
			this.OnStuck = OnStuck;
			this.forcesSprint = movingSpeed >= entity.cachedData.moveSpeed;
			CalcMovement();
			CalcAnimated();
			BlockPos startBlockPos = entity.ServerPos.AsBlockPos;
			if (entity.World.BlockAccessor.IsNotTraversable(startBlockPos)) {
				PathsNotFound();
				return false;
			}
			FindPath_Async(startBlockPos, target.AsBlockPos, searchDepth, mhdistanceTolerance);
			return true;
		}

		public override bool WalkTowards(Vec3d target, float movingSpeed, float targetDistance, Action OnGoals, Action OnStuck) {
			curWaypoints = new List<Vec3d>();
			curWaypoints.Add(target);
			this.target = target;
			this.targetDistance = targetDistance * 0.9f;
			this.OnGoals = OnGoals;
			this.OnStuck = OnStuck;
			this.forcesSprint = movingSpeed >= entity.cachedData.moveSpeed;
			CalcMovement();
			return base.WalkTowards(target, curMoveSpeed, targetDistance * 0.9f, OnGoals, OnStuck);
		}

		protected override bool BeginGo() {
			curTurnRadPerSec = minTurnAngle + (float)entity.World.Rand.NextDouble() * (maxTurnAngle - minTurnAngle);
			curTurnRadPerSec *= GameMath.DEG2RAD * 50;
			waypointsPrvMs = entity.World.ElapsedMilliseconds;
			waypointsUntil = 0;
			stuckCounter = 0;
			distChecksMs = 0;
			prvPosAccums = 0;
			return true;
		}

		public override void Retarget() {
			Active = true;
			distChecksMs = 0;
			prvPosAccums = 0;
			waypointsUntil = curWaypoints.Count - 1;
			forcesSprint = curMoveSpeed >= entity.cachedData.moveSpeed;
		}

		public override void Stop() {
			for (int i = (curWaypoints?.Count ?? 0) - 1; i >= 0 && i >= waypointsUntil - 1; i--) {
				ToggleDoors(curWaypoints[i].AsBlockPos, false);
			}
			Active = false;
			distToTarget = 0;
			forcesSprint = false;
			CalcMovement();
			CalcControls();
			CalcAnimated();
			stuckCounter = 0;
			distChecksMs = 0;
			prvPosAccums = 0;
			asyncSearchObject = null;
		}

		public override void OnGameTick(float dt) {
			if (this.asyncSearchObject != null) {
				if (!asyncSearchObject?.Finished ?? true) {
					return;
				}
				PathsAreFound();
			}
			if (!Active) {
				return;
			}
			int offset = 0;
			bool nearHorizontally = false;
			bool nearAllDirs = IsNearTarget(offset++, ref nearHorizontally) || IsNearTarget(offset++, ref nearHorizontally) || IsNearTarget(offset++, ref nearHorizontally);
			EntityControls controls = entity.MountedOn == null ? entity.Controls : entity.MountedOn.Controls;
			if (controls == null) {
				return;
			}
			if (nearAllDirs) {
				waypointsUntil += offset;
				waypointsPrvMs = entity.World.ElapsedMilliseconds;
				target = curWaypoints[Math.Min(curWaypoints.Count - 1, waypointsUntil)];
				if (waypointsUntil > 2) {
					ToggleDoors(curWaypoints[waypointsUntil - 3].AsBlockPos, true);
				}
				controls.Sneak = target.Y < entity.ServerPos.Y && target.X == entity.ServerPos.X && target.Z == entity.ServerPos.Z;
			} else {
				target = curWaypoints[Math.Min(curWaypoints.Count - 1, waypointsUntil)];
			}
			bool onlastWaypoint = waypointsUntil == curWaypoints.Count - 1;
			if (waypointsUntil >= curWaypoints.Count) {
				Stop();
				OnGoals?.Invoke();
				return;
			}
			bool stuckBelowOrAbove = (nearHorizontally && !nearAllDirs);
			bool stuck = (entity.CollidedVertically && entity.Controls.IsClimbing) || (entity.CollidedHorizontally && entity.ServerPos.Motion.Y <= 0) || stuckBelowOrAbove || (entity.CollidedHorizontally && curWaypoints.Count > 1 && waypointsUntil < curWaypoints.Count && entity.World.ElapsedMilliseconds - waypointsPrvMs > 2000);
			float distsq = prvTargetPrv.SquareDistanceTo(prvTargetPos);
			stuck |= (distsq < 0.01 * 0.01) ? (entity.World.Rand.NextDouble() < GameMath.Clamp(1 - distsq * 1.2, 0.1, 0.9)) : false;
			// Test movement progress between two points in 150 millisecond intervalls.
			prvPosAccums += dt;
			if (prvPosAccums > 0.2) {
				prvPosAccums = 0;
				prvTargetPrv.Set(prvTargetPos);
				prvTargetPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
			}
			// Long duration tests to make sure we're not just wobbling around in the same spot.
			distChecksMs += dt;
			if (distChecksMs > 2) {
				distChecksMs = 0;
				if (Math.Abs(curPosDistSq - prvPosDistSq) < 0.1) {
					stuck = true;
					stuckCounter += 30;
				} else if (!stuck) {
					stuckCounter = 0;
				}
				// Only reset the stuckCounter in same tick as doing this test; otherwise the stuckCounter gets set to 0 every 2 or 3 ticks even if the entity collided horizontally (because motion vecs get set to 0 after the collision, so won't collide in the successive tick).
				prvPosDistSq = curPosDistSq;
			}
			if (stuck) {
				stuckCounter++;
			}
			if (GlobalConstants.OverallSpeedMultiplier > 0 && stuckCounter > 60 / GlobalConstants.OverallSpeedMultiplier) {
				Stop();
				OnStuck?.Invoke();
				return;
			}
			distToTarget = entity.ServerPos.SquareHorDistanceTo(target);
			curTargetVec.Set((float)(target.X - entity.ServerPos.X), (float)(target.Y - entity.ServerPos.Y), (float)(target.Z - entity.ServerPos.Z));
			curTargetVec.Normalize();
			CalcMovement();
			float desiredYaw = 0;
			float nowMoveSpeed = curMoveSpeed;
			if (curPosDistSq >= 0.01) {
				desiredYaw = (float)Math.Atan2(curTargetVec.X, curTargetVec.Z);
			}
			if (curPosDistSq < 1) {
				nowMoveSpeed = Math.Max(0.005f, curMoveSpeed * Math.Max(curPosDistSq, 0.2f));
			}
			float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);
			float turnSpeed = curTurnRadPerSec * dt * curMoveSpeed;
			entity.ServerPos.Yaw += GameMath.Clamp(yawDist, -turnSpeed, turnSpeed);
			entity.ServerPos.Yaw = entity.ServerPos.Yaw % GameMath.TWOPI;
			double cosYaw = Math.Cos(entity.ServerPos.Yaw);
			double sinYaw = Math.Sin(entity.ServerPos.Yaw);
			controls.WalkVector.Set(sinYaw, GameMath.Clamp(curTargetVec.Y, -1, 1), cosYaw);
			controls.WalkVector.Mul(nowMoveSpeed / Math.Max(1, Math.Abs(yawDist) * 3));
			CalcControls();
			CalcAnimated();
			// Make it walk along the wall, but not walk into the wall, which causes it to climb
			if (entity.Properties.RotateModelOnClimb && entity.Controls.IsClimbing && entity.ClimbingIntoFace != null && entity.Alive) {
				BlockFacing facing = entity.ClimbingIntoFace;
				if (Math.Sign(facing.Normali.X) == Math.Sign(controls.WalkVector.X)) {
					controls.WalkVector.X = 0;
				}
				if (Math.Sign(facing.Normali.Y) == Math.Sign(controls.WalkVector.Y)) {
					controls.WalkVector.Y = -controls.WalkVector.Y;
				}
				if (Math.Sign(facing.Normali.Z) == Math.Sign(controls.WalkVector.Z)) {
					controls.WalkVector.Z = 0;
				}
			}
			if (entity.Swimming) {
				controls.FlyVector.Set(controls.WalkVector);
				Vec3d pos = entity.Pos.XYZ;
				Block inblock = entity.World.BlockAccessor.GetBlock(new BlockPos(pos.XInt, pos.YInt, pos.ZInt, entity.SidedPos.Dimension), BlockLayersAccess.Fluid);
				Block aboveblock = entity.World.BlockAccessor.GetBlock(new BlockPos((int)pos.X, (int)(pos.Y + 1), (int)pos.Z, entity.SidedPos.Dimension), BlockLayersAccess.Fluid);
				float waterY = (int)pos.Y + inblock.LiquidLevel / 8f + (aboveblock.IsLiquid() ? 9 / 8f : 0);
				float bottomSubmergedness = waterY - (float)pos.Y;
				float swimlineSubmergedness = GameMath.Clamp(bottomSubmergedness - ((float)entity.SwimmingOffsetY), 0, 1);
				swimlineSubmergedness = Math.Min(1, swimlineSubmergedness + 0.5f);
				controls.FlyVector.Y = GameMath.Clamp(controls.FlyVector.Y, 0.02f, 0.04f) * swimlineSubmergedness;
				if (entity.CollidedHorizontally) {
					controls.FlyVector.Y = 0.05f;
				}
			}
		}

		private bool IsNearTarget(int waypointOffset, ref bool nearHorizontally) {
			if (curWaypoints.Count - 1 < waypointsUntil + waypointOffset) {
				return false;
			}
			int wayPointIndex = Math.Min(curWaypoints.Count - 1, waypointsUntil + waypointOffset);
			Vec3d target = curWaypoints[wayPointIndex];
			double curPosY = entity.ServerPos.Y;
			curPosDistSq = target.HorizontalSquareDistanceTo(entity.ServerPos.X, entity.ServerPos.Z);
			var vdistsq = (target.Y - curPosY) * (target.Y - curPosY);
			bool above = curPosY > target.Y;
			// Ok to be up to 1 block above or 0.5 blocks below.
			curPosDistSq += (float)Math.Max(0, vdistsq - (above ? 1 : 0.5));
			if (!nearHorizontally) {
				double horcurDistToPos = target.HorizontalSquareDistanceTo(entity.ServerPos.X, entity.ServerPos.Z);
				nearHorizontally = horcurDistToPos < targetDistance * targetDistance;
			}
			return curPosDistSq < targetDistance * targetDistance;
		}

		private bool CanBeOpened(BlockPos pos) {
			if (!reinforceSystem.IsReinforced(pos)) {
				return true;
			}
			if (!entity.WatchedAttributes.HasAttribute(LeadersGUID)) {
				return false;
			}
			IPlayer leader = entity.Api.World.PlayerByUid(new string(entity.WatchedAttributes.GetString(LeadersGUID)));
			if (leader != null) {
				return !reinforceSystem.IsLockedForInteract(pos, leader);
			}
			return false;
		}

		private void ToggleDoors(BlockPos pos, bool toggleOpen) {
			if (pos.HorizontalManhattenDistance(entity.ServerPos.AsBlockPos) > 3) {
				return;
			}
			var doorBehavior = BlockBehaviorDoor.getDoorAt(entity.World, pos);
			if (doorBehavior != null && doorBehavior.Opened != toggleOpen && CanBeOpened(pos)) {
				// doorBehavior.Opened == false;
				doorBehavior.ToggleDoorState(null, toggleOpen);
			}
		}

		private void FindPath(BlockPos startBlockPos, BlockPos targetBlockPos, int searchDepth, int mhdistanceTolerance = 0) {
			waypointsUntil = 0;
			var bh = entity.GetBehavior<EntityBehaviorControlledPhysics>();
			float stepHeight = bh == null ? 0.6f : bh.stepHeight;
			int maxFallHeight = entity.Properties.FallDamage ? Math.Min(8, (int)Math.Round(3.51 / Math.Max(0.01, entity.Properties.FallDamageMultiplier))) - (int)(movingSpeed * 30) : 8;
			newWaypoints = systemsPathfinder.FindPathAsWaypoints(startBlockPos, targetBlockPos, maxFallHeight, stepHeight, entity.CollisionBox, searchDepth, mhdistanceTolerance);
		}

		private void FindPath_Async(BlockPos startBlockPos, BlockPos targetBlockPos, int searchDepth, int mhdistanceTolerance = 0) {
			waypointsUntil = 0;
			asyncSearchObject = PreparePathfinderTask(startBlockPos, targetBlockPos, searchDepth, mhdistanceTolerance);
			asyncPathfinder.EnqueuePathfinderTask(asyncSearchObject);
		}

		private void PathsNotFound() {
			curWaypoints = new List<Vec3d>();
			curWaypoints.Add(target);
			base.WalkTowards(target, curMoveSpeed, targetDistance, OnGoals, OnStuck);
			if (NoPaths != null) {
				Active = false;
				NoPaths.Invoke();
			}
		}

		private bool PathsAreFound() {
			if (asyncSearchObject != null) {
				newWaypoints = asyncSearchObject.waypoints;
				asyncSearchObject = null;
			}
			if (newWaypoints == null) {
				PathsNotFound();
				return false;
			}
			curWaypoints = newWaypoints;
			curWaypoints.Add(target);
			base.WalkTowards(target, curMoveSpeed, targetDistance, OnGoals, OnStuck);
			return true;
		}

		private void StopMovement() {
			if (entity.World.Side == EnumAppSide.Server) {
				entity.AnimManager.AnimationsDirty = true;
			}
			if (entity.AnimManager.ActiveAnimationsByAnimCode.Count > 0) {
				foreach (KeyValuePair<string, AnimationMetaData> item in entity.AnimManager.ActiveAnimationsByAnimCode) {
					if (item.Value.Code != curIdleAnims) {
						entity.AnimManager.ActiveAnimationsByAnimCode.Remove(item.Key);
					}
				}
			}
		}

		private void StopControls() {
			curMoveSpeed = 0;
			curMoveAnims = curIdleAnims;
			entity.Controls.WalkVector.Set(0, 0, 0);
			entity.Controls.Forward = false;
			entity.Controls.Right = false;
			entity.Controls.Backward = false;
			entity.Controls.Left = false;
			if (!entity.ruleOrder[1]) {
				entity.Controls.Sprint = false;
			}
			entity.ServerControls.SetFrom(entity.Controls);
		}

		private void CalcControls() {
			if (!Active || curMoveSpeed == 0) {
				StopControls();
				return;
			}
			Vec3d vector = entity?.Controls.WalkVector ?? entity?.ServerControls.WalkVector;
			bool xPos = vector.X > 0.1f;
			bool yPos = vector.Y > 0.1f;
			bool xNeg = vector.X < -0.1f;
			bool yNeg = vector.Y < -0.1f;
			entity.Controls.Forward = xPos && !xNeg;
			entity.Controls.Backward = !xPos && xNeg;
			entity.Controls.Right = yPos && !yNeg;
			entity.Controls.Left = !yPos && yNeg;
			if (!entity.ruleOrder[1] || entity.ServerPos.HorDistanceTo(target) > 81f) {
				entity.Controls.Sprint = xPos && entity.ServerPos.HorDistanceTo(target) > 36f;
			}
			entity.ServerControls.SetFrom(entity.Controls);
		}

		private void CalcMovement() {
			Single speed = entity.cachedData.walkSpeed;
			if (!entity.Alive || !Active || distToTarget <= targetDistance * targetDistance) {
				speed = 0;
			} else if (entity.Controls.Sprint || forcesSprint || (distToTarget > 36f)) {
				speed = entity.cachedData.moveSpeed;
			}
			curMoveSpeed = speed * GlobalConstants.OverallSpeedMultiplier;
		}

		private void CalcAnimated() {
			String anims = curIdleAnims;
			if (!entity.Alive || !Active || curMoveSpeed < 0.005f) {
				entity.AnimManager.StopAnimation(curAnimation);
			} else if (entity.Swimming) {
				anims = curSwimAnims;
			} else if (entity.FeetInLiquid) {
				anims = curWalkAnims;
			} else if (entity.Controls.Sneak) {
				anims = curDuckAnims;
			} else if (entity.Controls.Sprint || forcesSprint || entity.IsOnFire) {
				anims = curMoveAnims;
			} else if (curMoveSpeed > 0) {
				anims = curWalkAnims;
			} else if (entity.Controls.Backward) {
				anims = "walkback";
			} else if (entity.Controls.Right) {
				anims = "walkside";
			} else if (entity.Controls.Left) {
				anims = "walkleft";
			}
			if (!entity.AnimManager.IsAnimationActive(anims)) {
				StopMovement();
				entity.AnimManager.StartAnimation(new AnimationMetaData() {
					Animation = anims,
					Code = anims,
					BlendMode = EnumAnimationBlendMode.Average,
					ElementWeight = new Dictionary<string, float> {
						{ "UpperFootR", 2f },
						{ "UpperFootL", 2f },
						{ "LowerFootR", 2f },
						{ "LowerFootL", 2f },
						{ "ItemAnchor", 0f }
					},
					MulWithWalkSpeed = true,
					EaseInSpeed = 999f,
					EaseOutSpeed = 999f
				}.Init());
			}
			curAnimation = anims;
		}
	}
}