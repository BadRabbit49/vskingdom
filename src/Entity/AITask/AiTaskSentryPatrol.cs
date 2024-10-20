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
		protected bool cancelPatrol;
		protected long durationOfMs = 1500L;
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
			if (entity.World.ElapsedMilliseconds < cooldownUntilMs) {
				return false;
			}
			cooldownUntilMs = entity.World.ElapsedMilliseconds + durationOfMs;
			if (!entity.ruleOrder[5] || entity.ruleOrder[1] || !EmotionStatesSatisifed()) {
				return false;
			}
			return InShiftsRange();
		}

		public override void StartExecute() {
			base.StartExecute();
			cancelPatrol = false;
			int closestPoint = 0;
			for (int i = 0; i < waypoints.Length; i++) {
				if (entity.ServerPos.DistanceTo(waypoints[i].AsBlockPos.ToVec3d()) < entity.ServerPos.DistanceTo(waypoints[closestPoint].AsBlockPos.ToVec3d())) {
					closestPoint = i;
				}
			}
			currentStepAt = closestPoint;
			currentTarget = entity.ruleOrder[6] ? outpostBlock : LoadNextVec3d();
			bool on = pathTraverser.NavigateTo_Async(currentTarget, entity.cachedData.walkSpeed, targetRanges, OnGoals, OnStuck, NoPaths);
		}

		public override bool ContinueExecute(float dt) {
			if (cancelPatrol || entity.ruleOrder[1] || !entity.ruleOrder[5] || !EmotionStatesSatisifed()) {
				return false;
			}
			return entity.ruleOrder[5];
		}

		public override void FinishExecute(bool cancelled) {
			durationOfMs = entity.World.ElapsedMilliseconds + mincooldown + entity.World.Rand.Next(maxcooldown - mincooldown);
			cooldownUntilMs = entity.World.ElapsedMilliseconds + durationOfMs;
		}

		private void OnStuck() {
			// Check for doorsways.
		}

		private void OnGoals() {
			// Check around surroundings.
		}

		private void NoPaths() {
			cancelPatrol = true;
		}

		private int ToFloor(int x, int y, int z) {
			int tries = 5;
			while (tries-- > 0) {
				if (world.BlockAccessor.IsSideSolid(x, y, z, BlockFacing.UP)) { return y + 1; }
				y--;
			}
			return -1;
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

		private bool InShiftsRange() {
			if (!entity.ruleOrder[4]) {
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

		private Vec3d LoadNextVec3d() {
			Vec3i[] points = waypoints;
			return new Vec3d(points[currentStepAt].X, points[currentStepAt].Y + 1, points[currentStepAt].Z);
		}
	}
}