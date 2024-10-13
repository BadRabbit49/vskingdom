using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Essentials;
using Vintagestory.GameContent;

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
		public Single curPosDistSq;
		public Single prvDistToPos;
		public Single prvPosAccums;
		public Single curMoveSpeed;
		public string curIdleAnims = "idle";
		public string curWalkAnims = "walk";
		public string curMoveAnims = "move";
		public string curDuckAnims = "duck";
		public string curSwimAnims = "swim";
		public string curJumpAnims = "jump";
		public string curFallAnims = "fall";
		public string curSeatAnims = "seat";
		public string curAnimation;
		public PathfindSystem systemsPathfinder;
		public PathfinderTask asyncSearchObject;
		public PathfindingAsync asyncPathfinder;
		public override Vec3d CurrentTarget { get => curWaypoints[curWaypoints.Count - 1]; }
		public override bool Ready { get => curWaypoints != null && asyncSearchObject == null; }

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
			this.targetDistance = targetDistance;
			this.OnStuck = OnStuck;
			this.OnGoals = OnGoals;
			CalcMovement();
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
			this.targetDistance = targetDistance;
			this.NoPaths = onNoPath;
			this.OnGoals = OnGoals;
			this.OnStuck = OnStuck;
			CalcMovement();
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
			bool walked = base.WalkTowards(target, movingSpeed, targetDistance, OnGoals, OnStuck);
			CalcMovement();
			return walked;
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
		}

		public override void Stop() {
			for (int i = (curWaypoints?.Count ?? 0) - 1; i >= 0 && i >= waypointsUntil - 1; i--) {
				ToggleDoor(curWaypoints[i].AsBlockPos, true);
			}
			StopControls();
			StopMovement();
			Active = false;
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
					ToggleDoor(curWaypoints[waypointsUntil - 3].AsBlockPos, true);
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
				if (Math.Abs(curPosDistSq - prvDistToPos) < 0.1) {
					stuck = true;
					stuckCounter += 30;
				} else if (!stuck) {
					stuckCounter = 0;
				}
				// Only reset the stuckCounter in same tick as doing this test; otherwise the stuckCounter gets set to 0 every 2 or 3 ticks even if the entity collided horizontally (because motion vecs get set to 0 after the collision, so won't collide in the successive tick).
				prvDistToPos = curPosDistSq;
			}
			if (stuck) {
				stuckCounter++;
			}
			if (GlobalConstants.OverallSpeedMultiplier > 0 && stuckCounter > 60 / GlobalConstants.OverallSpeedMultiplier) {
				Stop();
				OnStuck?.Invoke();
				return;
			}
			curTargetVec.Set((float)(target.X - entity.ServerPos.X), (float)(target.Y - entity.ServerPos.Y), (float)(target.Z - entity.ServerPos.Z));
			curTargetVec.Normalize();
			float desiredYaw = 0;
			if (curPosDistSq >= 0.01) {
				desiredYaw = (float)Math.Atan2(curTargetVec.X, curTargetVec.Z);
			}
			CalcMovement();
			float nowMoveSpeed = curMoveSpeed;
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
			CalcMovement();
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
				Block inblock = entity.World.BlockAccessor.GetBlock(new BlockPos((int)pos.X, (int)(pos.Y), (int)pos.Z, entity.SidedPos.Dimension), BlockLayersAccess.Fluid);
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

		private bool IsFallingFar() {
			if (entity.OnGround && !entity.FeetInLiquid) {
				entity.Api.Logger.Notification("EntityMotion is: " + entity.ServerPos.Motion.Y);
				return true;
			}
			return false;
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

		private bool ToggleDoor(BlockPos pos, bool wasOpen) {
			if (pos.HorizontalManhattenDistance(entity.ServerPos.AsBlockPos) > 3) {
				return false;
			}
			var doorBehavior = BlockBehaviorDoor.getDoorAt(entity.World, pos);
			if (doorBehavior != null && doorBehavior.Opened == wasOpen) {
				doorBehavior.ToggleDoorState(null, !wasOpen);
				return true; // TryLock(doorBehavior.Pos); Check if the Ai can even open this with their mommy or daddy's credit card permissions.
			}
			if (entity.World.BlockAccessor.GetBlock(pos) is BlockBaseDoor doorBlock && doorBlock.IsOpened() == wasOpen) {
				doorBlock.OnBlockInteractStart(entity.World, null, new BlockSelection(pos, BlockFacing.UP, doorBlock));
				return true;
			}
			return false;
		}

		private bool TryLock(BlockPos pos) {
			ModSystemBlockReinforcement blockReinforcement = entity.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();
			if (blockReinforcement is null || !blockReinforcement.IsReinforced(pos)) {
				return true;
			}
			if (!entity.WatchedAttributes.HasAttribute(LeadersGUID)) {
				return false;
			}
			return !blockReinforcement.IsLockedForInteract(pos, entity.World.PlayerByUid(entity.WatchedAttributes.GetString(LeadersGUID)));
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
			entity.Controls.Sprint = false;
			entity.ServerControls.SetFrom(entity.Controls);
		}

		private void CalcControls() {
			if (!Active) {
				StopControls();
				return;
			}
			Vec3d vector = entity?.Controls.WalkVector ?? entity?.ServerControls.WalkVector;
			bool xPos = vector.X > 0;
			bool yPos = vector.Y > 0;
			entity.Controls.Forward = xPos;
			entity.Controls.Right = yPos;
			entity.Controls.Backward = !xPos && vector.X < 0;
			entity.Controls.Left = !yPos && vector.Y < 0;
			entity.Controls.Sprint = curPosDistSq > 30f;
			entity.ServerControls.SetFrom(entity.Controls);
		}

		private void CalcMovement() {
			String anims = curIdleAnims;
			Single speed = entity.cachedData.walkSpeed * GlobalConstants.OverallSpeedMultiplier;
			if (!entity.Alive || !Active) {
				anims = curIdleAnims;
				speed *= 0f;
			} else if (entity.Swimming) {
				anims = curSwimAnims;
				speed *= 1f;
			} else if (entity.FeetInLiquid) {
				anims = curWalkAnims;
				speed *= GlobalConstants.WaterDrag;
			} else if (entity.Controls.Sneak) {
				anims = curDuckAnims;
				speed *= GlobalConstants.SneakSpeedMultiplier;
			} else if (entity.Controls.Sprint || entity.IsOnFire || entity.InLava) {
				anims = curMoveAnims;
				speed = entity.cachedData.moveSpeed;
			} else if (entity.passenger[0]) {
				anims = entity.MountedOn?.SuggestedAnimation ?? curSeatAnims;
			} else if (entity.Controls.Right) {
				anims = "walkside";
				speed *= 0.95f;
			} else if (entity.Controls.Left) {
				anims = "walkleft";
				speed *= 0.95f;
			} else if (entity.Controls.Backward) {
				anims = "walkback";
				speed *= 0.95f;
			} else if (entity.Controls.Forward) {
				anims = curWalkAnims;
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
					},
					MulWithWalkSpeed = true,
					EaseInSpeed = 999f,
					EaseOutSpeed = 999f
				}.Init());
			}
			curMoveSpeed = speed;
			curAnimation = anims;
		}
	}
}