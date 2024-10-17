using System;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class AiTaskSentryPatrol : AiTaskBase {
		public AiTaskSentryPatrol(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		protected bool cancelPatrols;
		protected bool pauseByPlayer;
		protected bool pauseByOnGoal;
		protected long pauseEntityId;
		protected long lastPausingMs;
		protected long lastCheckedMs;
		protected long pauseCooldown = 1500L;
		protected long checkCooldown = 1500L;
		protected float patrolChance;
		protected float patrolHeight;
		protected float targetRanges;
		protected Int32 currentStepAt;
		protected Vec3d currentTarget;
		protected float currentHours { get => world.Calendar.HourOfDay; }
		protected float patrolStarts { get => entity.WatchedAttributes.GetFloat("shiftStarts", 0f); }
		protected float patrolEnding { get => entity.WatchedAttributes.GetFloat("shiftEnding", 24f); }
		protected Vec3d outpostBlock { get => entity.WatchedAttributes.GetBlockPos("postBlock").ToVec3d(); }
		protected Vec3i[] waypoints { get => entity.WatchedAttributes.GetVec3is("patrolVec3i"); }
		
		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			this.targetRanges = taskConfig["targetRanges"].AsFloat(0.12f);
			this.patrolHeight = taskConfig["patrolHeight"].AsFloat(7f);
			this.patrolChance = taskConfig["patrolChance"].AsFloat(0.15f);
			this.whenNotInEmotionState = taskConfig["whenNotInEmotionState"].AsString("aggressiveondamage|fleeondamage");
		}

		public override bool ShouldExecute() {
			if (!entity.ruleOrder[5]) {
				return false;
			}
			if (lastCheckedMs + checkCooldown > entity.World.ElapsedMilliseconds) {
				return false;
			}
			lastCheckedMs = entity.World.ElapsedMilliseconds;
			if (entity.ruleOrder[1] || !EmotionStatesSatisifed()) {
				return false;
			}
			return InShiftsRange();
		}

		public override void StartExecute() {
			base.StartExecute();
			cancelPatrols = false;
			pauseByPlayer = false;
			pauseByOnGoal = false;
			int closestPoint = 0;
			for (int i = 0; i < waypoints.Length; i++) {
				if (entity.ServerPos.DistanceTo(waypoints[i].AsBlockPos.ToVec3d()) < entity.ServerPos.DistanceTo(waypoints[closestPoint].AsBlockPos.ToVec3d())) {
					closestPoint = i;
				}
			}
			currentStepAt = closestPoint;
			currentTarget = entity.ruleOrder[6] ? outpostBlock : LoadNextVec3d();
			if (GoingDirectly(entity.ServerPos.XYZ, currentTarget) && !DangerousLine(currentTarget)) {
				bool ok = pathTraverser.WalkTowards(currentTarget, 0.04f, targetRanges, OnGoals, OnStuck);
			} else {
				bool on = pathTraverser.NavigateTo(currentTarget, 0.04f, targetRanges, OnGoals, OnStuck);
			}
		}

		public override bool ContinueExecute(float dt) {
			if (cancelPatrols || entity.ruleOrder[1] || !entity.ruleOrder[5] || !EmotionStatesSatisifed()) {
				cancelPatrols = true;
				return false;
			}
			// If we are a climber dude and encountered a wall, let's not try to get behind the wall.
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
			// If the entity is locked in a dialog with a player, don't be rude.
			if (pauseByPlayer) {
				StareAt(dt);
				if (entity.World.ElapsedMilliseconds > lastPausingMs + pauseCooldown) {
					if (entity.World.GetEntityById(pauseEntityId).ServerPos.SquareDistanceTo(entity.ServerPos) > 4f) {
						pauseByPlayer = false;
						pathTraverser.Retarget();
					} else {
						lastPausingMs = entity.World.ElapsedMilliseconds;
					}
					return true;
				}
			}
			// Look ahead, if there is a door, open it if possible before walking through. Remember to shut afterwards if it was closed! Code courtesy of Dana!
			entity.World.BlockAccessor.WalkBlocks(entity.ServerPos.AsBlockPos.AddCopy(-1, -1, -1), entity.ServerPos.AsBlockPos.AddCopy(1, 1, 1), (block, x, y, z) => {
				BlockPos pos = new(x, y, z, entity.SidedPos.Dimension);
				TryOpen(pos);
			});
			// Stop for a second and wait. After cooldown, see plot to next area.
			if (ReachedTarget()) {
				pathTraverser.Stop();
				currentStepAt++;
				currentTarget = LoadNextVec3d();
				pauseByOnGoal = true;
				lastPausingMs = entity.World.ElapsedMilliseconds;
			}
			// Resume travelling to next waypoint as designated.
			if (pauseByOnGoal) {
				if (entity.World.ElapsedMilliseconds < lastPausingMs + pauseCooldown) {
					return true;
				}
				pauseByOnGoal = false;
				if (GoingDirectly(entity.ServerPos.XYZ, currentTarget) && !DangerousLine(currentTarget)) {
					bool go = pathTraverser.WalkTowards(currentTarget, 0.04f, targetRanges, OnGoals, OnStuck);
				} else {
					bool no = pathTraverser.NavigateTo(currentTarget, 0.04f, targetRanges, OnGoals, OnStuck);
				}
			}
			return entity.ruleOrder[5];
		}

		public override void FinishExecute(bool cancelled) {
			cooldownUntilMs = entity.World.ElapsedMilliseconds + mincooldown + entity.World.Rand.Next(maxcooldown - mincooldown);
		}

		public void PauseExecute(EntityAgent entity) {
			cancelPatrols = true;
			pauseByPlayer = true;
			pauseEntityId = entity.EntityId;
			lastPausingMs = entity.World.ElapsedMilliseconds;
		}

		private void StareAt(float dt) {
			Vec3f targetVec = new Vec3f();
			Entity target = world.GetEntityById(pauseEntityId);
			if (target == null || !pauseByPlayer) { return; }
			targetVec.Set((float)(target.ServerPos.X - entity.ServerPos.X), (float)(target.ServerPos.Y - entity.ServerPos.Y), (float)(target.ServerPos.Z - entity.ServerPos.Z));
			float desiredYaw = (float)Math.Atan2(targetVec.X, targetVec.Z);
			float maxturnRad = 360 * GameMath.DEG2RAD;
			float spawnAngle = entity.Attributes.GetFloat("spawnAngleRad");
			float entYawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, GameMath.Clamp(desiredYaw, spawnAngle - maxturnRad, spawnAngle + maxturnRad));
			entity.ServerPos.Yaw += GameMath.Clamp(entYawDist, -pathTraverser.curTurnRadPerSec * dt, pathTraverser.curTurnRadPerSec * dt);
			entity.ServerPos.Yaw = entity.ServerPos.Yaw % GameMath.TWOPI;
		}

		private void OnStuck() {
			cancelPatrols = true;
		}

		private void OnGoals() {
			pathTraverser.Retarget();
			pauseByOnGoal = true;
			lastPausingMs = entity.World.ElapsedMilliseconds;
		}

		private void NoPaths() {
			cancelPatrols = true;
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

		private bool DangerousLine(Vec3d pos) {
			// Lets make a straight line plot to see if we would fall off a cliff.
			bool mustStop = false;
			bool willFall = false;
			float angleHor = (float)Math.Atan2(pos.X, pos.Z) + GameMath.PIHALF;
			Vec3d blockAhead = pos.Ahead(1, 0, angleHor);
			// Otherwise they are forever stuck if they stand over the edge.
			Vec3d startAhead = entity.ServerPos.XYZ.Ahead(1, 0, angleHor);
			// Draw a line from here to there and check ahead to see if we will fall.
			GameMath.BresenHamPlotLine2d((int)startAhead.X, (int)startAhead.Z, (int)blockAhead.X, (int)blockAhead.Z, (x, z) => {
				if (mustStop) { return; }
				int nowY = ToFloor(x, (int)startAhead.Y, z);
				if (nowY < 0 || startAhead.Y - nowY > 4) { willFall = true; mustStop = true; }
				if (nowY - startAhead.Y > 2) { mustStop = true; }
				startAhead.Y = nowY;
			});
			return willFall;
		}

		private bool ReachedTarget() {
			return entity.ServerPos.SquareDistanceTo(currentTarget) < 4;
		}

		private bool GoingDirectly(Vec3d pos1, Vec3d pos2) {
			EntitySelection entitySel = new EntitySelection();
			BlockSelection blockSel = new BlockSelection();
			// Line trace to see if blocks are in the way.
			entity.World.RayTraceForSelection(pos1.AddCopy(entity.LocalEyePos), pos2.AddCopy(entity.LocalEyePos), ref blockSel, ref entitySel);
			// If a door is in the way, open it!
			if (blockSel?.Block is BlockBaseDoor || blockSel?.Block is BlockDoor) {
				return !IsLocked(blockSel.Position);
			}
			if (blockSel?.Block.IsLiquid() ?? false) {
				return true;
			}
			return false;
		}

		private bool InShiftsRange() {
			if (entity.ruleOrder[4] == false) {
				return true;
			}
			if (patrolStarts < patrolEnding) {
				return currentHours > patrolStarts && currentHours < patrolEnding;
			}
			if (patrolStarts > patrolEnding) {
				return currentHours > patrolStarts || currentHours < patrolEnding;
			}
			return true;
		}

		private bool TryOpen(BlockPos pos) {
			Block block = entity.World.BlockAccessor.GetBlock(pos);
			if (IsLocked(pos)) {
				return false;
			}
			Caller caller = new() { Type = EnumCallerType.Entity, Entity = entity, };
			BlockSelection blockSelection = new(pos, BlockFacing.DOWN, block);
			TreeAttribute activationArgs = new();
			activationArgs.SetBool("opened", true);
			block.Activate(entity.World, caller, blockSelection, activationArgs);
			return true;
		}

		private bool IsLocked(BlockPos pos) {
			ModSystemBlockReinforcement blockReinforcement = entity.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();
			if (!blockReinforcement.IsReinforced(pos)) {
				return false;
			}
			if (entity.cachedData.leadersGUID == null) {
				return true;
			}
			return blockReinforcement.IsLockedForInteract(pos, world.PlayerByUid(entity.cachedData.leadersGUID));
		}

		private Vec3d LoadNextVec3d() {
			Vec3i[] points = waypoints;
			return new Vec3d(points[currentStepAt].X, points[currentStepAt].Y + 1, points[currentStepAt].Z);
		}
	}
}