using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using static VSKingdom.Utilities.GenericUtil;

namespace VSKingdom {
	public class AiTaskSentrySearch : AiTaskBaseTargetable {
		public AiTaskSentrySearch(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		protected bool cancelSearch;
		protected bool lastPathfind;
		protected long lastAttackedAtMs;
		protected long lastHurtByTarget;
		protected long lastFinishedAtMs;
		protected long lastRetreatsAtMs;
		protected float extraTargetOffset;
		protected float currentUpdateTime;
		protected float currentFollowTime;
		protected float maximumFollowTime;
		protected float maximumRange;
		protected float retreatRange;
		protected float seekingRange;
		protected Vec3d curTargetPos;
		protected Vec3d lastGoalReachedPos;
		protected Dictionary<long, int> futilityCounters;
		protected EnumAttackPattern attackPattern;
		protected AiTaskManager tasksManager;
		protected Entity followEntity;
		protected bool RecentlyTookDamages => entity.World.ElapsedMilliseconds - lastHurtByTarget < 10000;
		protected bool RemainInRetreatMode => entity.World.ElapsedMilliseconds - lastRetreatsAtMs < 20000;
		protected bool RemainInOffenseMode => entity.World.ElapsedMilliseconds - lastAttackedAtMs < 20000;
		protected float pursueRange { get => entity.WatchedAttributes.GetFloat("pursueRange", 1f); }

		public override void AfterInitialize() {
			world = entity.World;
			bhPhysics = entity.GetBehavior<EntityBehaviorControlledPhysics>();
			bhEmo = entity.GetBehavior<EntityBehaviorEmotionStates>();
			pathTraverser = entity.GetBehavior<EntityBehaviorTaskAI>().PathTraverser;
			tasksManager = entity.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
		}

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			this.partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();
			this.retaliateAttacks = taskConfig["retaliateAttacks"].AsBool(true);
			this.mincooldown = taskConfig["mincooldown"].AsInt(1000);
			this.maxcooldown = taskConfig["maxcooldown"].AsInt(1500);
			this.extraTargetOffset = taskConfig["extraTargetOffset"].AsFloat(1.5f);
			this.maximumFollowTime = taskConfig["maximumFollowTime"].AsFloat(60f);
			this.retreatRange = taskConfig["retreatRange"].AsFloat(36f);
			this.seekingRange = taskConfig["seekingRange"].AsFloat(25f);
		}

		public override bool ShouldExecute() {
			if (entity.ruleOrder[1] && entity.WatchedAttributes["mountedOn"] != null) {
				return false;
			}
			return targetEntity?.Alive ?? false;
		}

		public override void StartExecute() {
			cancelSearch = false;
			currentFollowTime = 0f;
			maximumRange = pursueRange * pursueRange;
			curTargetPos = targetEntity.ServerPos.XYZ;
			extraTargetOffset = TargetDist();
			if (RemainInRetreatMode) {
				Retreats();
				return;
			}
			if (entity.ruleOrder[1]) {
				followEntity = entity.Api.World.PlayerByUid(entity.WatchedAttributes.GetString("guardedPlayerUid", "")).Entity;
			}
			var navigateAction = DoSieged;
			if (entity.cachedData.usesRange) {
				bool safeSpot = !NotSafe();
				bool tooClose = targetEntity.ServerPos.SquareDistanceTo(entity.ServerPos) < 16f && CanSeeEnt(targetEntity, entity);
				bool runfight = tooClose || (!safeSpot && world.Rand.NextDouble() < 0.5);
				navigateAction = runfight ? Retreats : DoCircle;
			} else {
				navigateAction = DoDirect;
			}
			navigateAction();
		}

		public override bool CanContinueExecute() {
			if (pathTraverser.Ready) {
				lastAttackedAtMs = entity.World.ElapsedMilliseconds;
				lastPathfind = true;
				return true;
			}
			return attackPattern == EnumAttackPattern.TacticalRetreat;
		}

		public override bool ContinueExecute(float dt) {
			if (targetEntity == null || !targetEntity.Alive || cancelSearch) {
				return false;
			}
			if (!entity.ruleOrder[3] && entity.ruleOrder[1] && followEntity != null && entity.ServerPos.SquareDistanceTo(followEntity.ServerPos) > maximumRange) {
				return false;
			}
			if (currentFollowTime == 0f && world.Rand.Next(100) < 25) {
				base.StartExecute();
			}
			if (entity.cachedData.usesRange && entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos) < extraTargetOffset * extraTargetOffset) {
				attackPattern = EnumAttackPattern.TacticalRetreat;
			}
			retreatRange = Math.Max(20f, retreatRange - dt / 4f);
			currentFollowTime += dt;
			currentUpdateTime += dt;
			if (attackPattern != EnumAttackPattern.TacticalRetreat) {
				if (RecentlyTookDamages && (!lastPathfind || IsInEmotionState("fleeondamage"))) {
					Retreats();
				}
				if (attackPattern == EnumAttackPattern.DirectAttack && currentUpdateTime >= 0.75f && curTargetPos.SquareDistanceTo(targetEntity.ServerPos.XYZ) >= 9f) {
					curTargetPos.Set(targetEntity.ServerPos.X + targetEntity.ServerPos.Motion.X * 10.0, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z + targetEntity.ServerPos.Motion.Z * 10.0);
					pathTraverser.WalkTowards(curTargetPos, 1, extraTargetOffset, OnGoals, OnStuck);
					currentUpdateTime = 0f;
				}
				if (attackPattern == EnumAttackPattern.DirectAttack || attackPattern == EnumAttackPattern.BesiegeTarget) {
					pathTraverser.CurrentTarget.X = targetEntity.ServerPos.X;
					pathTraverser.CurrentTarget.Y = targetEntity.ServerPos.Y;
					pathTraverser.CurrentTarget.Z = targetEntity.ServerPos.Z;
				}
			}
			if (attackPattern == EnumAttackPattern.TacticalRetreat && world.Rand.NextDouble() >= 0.4) {
				updateTargetPosFleeMode(curTargetPos);
				pathTraverser.CurrentTarget.X = curTargetPos.X;
				pathTraverser.CurrentTarget.Y = curTargetPos.Y;
				pathTraverser.CurrentTarget.Z = curTargetPos.Z;
				Retreats();
			}
			Vec3d vec3 = entity.ServerPos.XYZ.Add(0.0, entity.SelectionBox.Y2 / 2f, 0.0).Ahead(entity.SelectionBox.XSize / 2f, 0f, entity.ServerPos.Yaw);
			double dist = targetEntity.SelectionBox.ToDouble().Translate(targetEntity.ServerPos.XYZ).ShortestDistanceFrom(vec3);
			bool flag = targetEntity != null && targetEntity.Alive && !cancelSearch && pathTraverser.Active;
			if (attackPattern == EnumAttackPattern.TacticalRetreat) {
				return flag && currentFollowTime < 9f && dist < retreatRange;
			}
			if (flag && currentFollowTime < maximumFollowTime && dist < seekingRange) {
				if (!(dist > extraTargetOffset)) {
					if (targetEntity is EntityAgent entityAgent) {
						return entityAgent?.ServerControls.TriesToMove ?? false;
					}
					return false;
				}
				return true;
			}
			return false;
		}

		public override void FinishExecute(bool cancelled) {
			cooldownUntilMs = entity.World.ElapsedMilliseconds + mincooldown + entity.World.Rand.Next(maxcooldown - mincooldown);
			lastFinishedAtMs = entity.World.ElapsedMilliseconds;
			if (targetEntity == null || !targetEntity.Alive) {
				ResetsTargets();
				StopMovements();
			}
		}

		public override bool Notify(string key, object data) {
			if (key == "seekEntity" && data != null) {
				targetEntity = (Entity)data;
				curTargetPos = targetEntity.ServerPos.XYZ;
				return true;
			}
			return false;
		}

		public void DoMovePattern(EnumAttackPattern pattern) {
			attackPattern = pattern;
		}

		public void SetTargetEnts(Entity target) {
			targetEntity = target;
			curTargetPos = target?.ServerPos.XYZ ?? curTargetPos;
		}

		public void ResetsTargets() {
			cancelSearch = true;
			targetEntity = null;
			curTargetPos = null;
		}

		public void StopMovements() {
			pathTraverser.Stop();
		}

		private void DoDirect() {
			// Just go forward towards the target!
			if (cancelSearch || targetEntity == null) {
				return;
			}
			int searchDepth = world.Rand.Next(3500, 10000);
			attackPattern = EnumAttackPattern.DirectAttack;
			pathTraverser.NavigateTo_Async(curTargetPos.OffsetCopy(rand.Next(-1, 1), 0, rand.Next(-1, 1)), 1, extraTargetOffset, OnGoals, OnStuck, DoSieged, searchDepth, 0);
		}

		private void DoSieged() {
			// Unable to perform direct attack pattern, trying sieged!
			if (cancelSearch || targetEntity == null) {
				return;
			}
			int searchDepth = world.Rand.Next(1500, 3500);
			attackPattern = EnumAttackPattern.BesiegeTarget;
			pathTraverser.NavigateTo_Async(curTargetPos, 1, extraTargetOffset, OnGoals, OnStuck, DoCircle, searchDepth, 3);
		}

		private void DoCircle() {
			// Unable to perform sieged attack pattern, trying circle!
			if (cancelSearch || targetEntity == null) {
				return;
			}
			if (curTargetPos.DistanceTo(entity.ServerPos.XYZ) > seekingRange) {
				Retreats();
				return;
			}
			attackPattern = EnumAttackPattern.CircleTarget;
			lastPathfind = false;
			float num1 = (float)Math.Atan2(entity.ServerPos.X - curTargetPos.X, entity.ServerPos.Z - curTargetPos.Z);
			for (int i = 0; i < 3; i++) {
				double value = (double)num1 + 0.5 + world.Rand.NextDouble() / 2.0;
				double num2 = 4.0 + world.Rand.NextDouble() * 6.0;
				double x = GameMath.Sin(value) * num2;
				double z = GameMath.Cos(value) * num2;
				curTargetPos.Add(x, 0.0, z);
				int num3 = 0;
				bool flag = false;
				BlockPos blockPos = new BlockPos((int)curTargetPos.X, (int)curTargetPos.Y, (int)curTargetPos.Z, curTargetPos.AsBlockPos.dimension);
				int num4 = 0;
				while (num3 < 5) {
					if (world.BlockAccessor.GetBlock(new BlockPos(blockPos.X, blockPos.Y - num4, blockPos.Z, blockPos.dimension)).SideSolid[BlockFacing.UP.Index] && !world.CollisionTester.IsColliding(world.BlockAccessor, entity.SelectionBox, new Vec3d((double)blockPos.X + 0.5, blockPos.Y - num4 + 1, (double)blockPos.Z + 0.5), alsoCheckTouch: false)) {
						flag = true;
						curTargetPos.Y -= num4;
						curTargetPos.Y += 1.0;
						break;
					}
					if (world.BlockAccessor.GetBlock(new BlockPos(blockPos.X, blockPos.Y + num4, blockPos.Z, blockPos.dimension)).SideSolid[BlockFacing.UP.Index] && !world.CollisionTester.IsColliding(world.BlockAccessor, entity.SelectionBox, new Vec3d((double)blockPos.X + 0.5, blockPos.Y + num4 + 1, (double)blockPos.Z + 0.5), alsoCheckTouch: false)) {
						flag = true;
						curTargetPos.Y += num4;
						curTargetPos.Y += 1.0;
						break;
					}
					num3++;
					num4++;
				}
				if (flag) {
					int searchDepth = world.Rand.Next(3500, 10000);
					pathTraverser.NavigateTo_Async(curTargetPos, 1, entity.cachedData.weapRange, OnGoals, OnStuck, Retreats, searchDepth, 1);
					return;
				}
			}
			Retreats();
		}

		private void Retreats() {
			// Unable to perform circle attack pattern, trying retreat!
			if (!RemainInOffenseMode && (RecentlyTookDamages || RemainInRetreatMode)) {
				Vec3d retreatPos = new Vec3d();
				updateTargetPosFleeMode(retreatPos);
				pathTraverser.CurrentTarget.X = retreatPos.X;
				pathTraverser.CurrentTarget.Y = retreatPos.Y;
				pathTraverser.CurrentTarget.Z = retreatPos.Z;
				pathTraverser.WalkTowards(retreatPos, 1, extraTargetOffset, OnGoals, OnStuck);
				if (attackPattern != EnumAttackPattern.TacticalRetreat) {
					lastRetreatsAtMs = entity.World.ElapsedMilliseconds;
				}
				attackPattern = EnumAttackPattern.TacticalRetreat;
			}
		}

		private void OnStuck() {
			cancelSearch = true;
		}

		private void OnGoals() {
			if ((attackPattern != 0 && attackPattern != EnumAttackPattern.BesiegeTarget) || cancelSearch || targetEntity == null) {
				return;
			}
			if (lastGoalReachedPos != null && lastGoalReachedPos.SquareDistanceTo(entity.ServerPos) < 0.005f) {
				if (futilityCounters == null) {
					futilityCounters = new Dictionary<long, int>();
				} else {
					futilityCounters.TryGetValue(targetEntity.EntityId, out var value);
					value++;
					futilityCounters[targetEntity.EntityId] = value;
					if (value > 19) { return; }
				}
			}
			lastGoalReachedPos = new Vec3d(entity.Pos);
			pathTraverser.Retarget();
		}

		private bool NotSafe() {
			if (targetEntity.ServerPos.SquareHorDistanceTo(entity.ServerPos.XYZ) > 16f || entity.ServerPos.Y - targetEntity.ServerPos.Y > 4) {
				return false;
			}
			bool noCrossing = false;
			float angleHor = (float)Math.Atan2(targetEntity.ServerPos.X, targetEntity.ServerPos.Z) + GameMath.PIHALF;
			Vec3d blockAhead = targetEntity.ServerPos.XYZ.Ahead(1, 0, angleHor);
			Vec3d startAhead = entity.ServerPos.XYZ.Ahead(1, 0, angleHor);
			GameMath.BresenHamPlotLine2d((int)startAhead.X, (int)startAhead.Z, (int)blockAhead.X, (int)blockAhead.Z, (x, z) => {
				int nowY = OnFloor(x, (int)startAhead.Y, z);
				// Not more than 4 blocks down.
				if (nowY < 0 || startAhead.Y - nowY > 4) {
					noCrossing = true;
				}
				startAhead.Y = nowY;
			});
			return !noCrossing;
		}

		private int OnFloor(int x, int y, int z) {
			int tries = 5;
			while (tries-- > 0) {
				if (world.BlockAccessor.IsSideSolid(x, y, z, BlockFacing.UP)) {
					return y + 1;
				}
				y--;
			}
			return -1;
		}

		private float TargetDist() => (entity.cachedData.usesRange ? 7f : entity.cachedData.weapRange);
	}
}