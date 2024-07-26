using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using System.Linq;
using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace VSKingdom {
	public class AiTaskSentrySearch : AiTaskBaseTargetable {
		public AiTaskSentrySearch(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		protected bool archerPilled = false;
		protected bool banditPilled = false;
		protected bool cancelSearch;
		protected bool jumpAtAnimOn;
		protected bool lastPathfind;
		protected bool leapAtTarget;
		protected long lastJumpingsAtMS;
		protected long lastAttackedAtMs;
		protected long lastHurtByTarget;
		protected long lastFinishedAtMs;
		protected long lastRetreatsAtMs;
		protected float currentFollowTime;
		protected float extraTargetDistance;
		protected float lastPathUpdateSeconds;
		protected float maxFollowTime = 60f;
		protected float minRange;
		protected float retreatRange = 20f;
		protected float seekingRange = 25f;
		protected float curMoveSpeed = 0.03f;
		protected Vec3d lastGoalReachedPos;
		protected Vec3d targetPos;
		protected Dictionary<long, int> futilityCounters;
		protected EnumAttackPattern attackPattern;
		protected bool RecentlyHurt => entity.World.ElapsedMilliseconds - lastHurtByTarget < 10000;
		protected bool RemainInRetreatMode => entity.World.ElapsedMilliseconds - lastRetreatsAtMs < 20000;
		protected bool RemainInOffenseMode => entity.World.ElapsedMilliseconds - lastAttackedAtMs < 20000;

		protected static readonly string walkAnimCode = "walk";
		protected static readonly string moveAnimCode = "move";
		protected static readonly string swimAnimCode = "swim";

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			this.partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();
			this.retaliateAttacks = taskConfig["retaliateAttacks"].AsBool(defaultValue: true);
			this.triggerEmotionState = taskConfig["triggerEmotionState"].AsString();
			this.skipEntityCodes = taskConfig["skipEntityCodes"].AsArray<string>()?.Select((string str) => AssetLocation.Create(str, entity.Code.Domain)).ToArray();
			this.archerPilled = taskConfig["isArcher"].AsBool(false);
			this.banditPilled = taskConfig["isBandit"].AsBool(false);
		}

		public override bool ShouldExecute() {
			return targetEntity != null && (targetEntity?.Alive ?? false);
		}

		public override void StartExecute() {
			cancelSearch = false;
			currentFollowTime = 0f;
			targetPos = targetEntity.ServerPos.XYZ;
			var navigateAction = DoSieged;
			if (RemainInRetreatMode) {
				Retreats();
				return;
			}
			if (archerPilled) {
				bool safeSpot = !NotSafe();
				bool runfight = !safeSpot && world.Rand.NextDouble() < 0.5;
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
			} else {
				return attackPattern == EnumAttackPattern.TacticalRetreat;
			}
		}

		public override bool ContinueExecute(float dt) {
			if (targetEntity == null || !targetEntity.Alive || cancelSearch) {
				return false;
			}
			if (currentFollowTime == 0f && world.Rand.NextDouble() < 0.25) {
				base.StartExecute();
			}
			if (archerPilled && InReach()) {
				attackPattern = EnumAttackPattern.TacticalRetreat;
			}
			retreatRange = Math.Max(20f, retreatRange - dt / 4f);
			currentFollowTime += dt;
			lastPathUpdateSeconds += dt;
			if (attackPattern == EnumAttackPattern.TacticalRetreat && world.Rand.NextDouble() < 0.2) {
				updateTargetPosFleeMode(targetPos);
				pathTraverser.CurrentTarget.X = targetPos.X;
				pathTraverser.CurrentTarget.Y = targetPos.Y;
				pathTraverser.CurrentTarget.Z = targetPos.Z;
			}
			if (attackPattern != EnumAttackPattern.TacticalRetreat) {
				if (RecentlyHurt && !lastPathfind) {
					Retreats();
				}
				if (attackPattern == EnumAttackPattern.DirectAttack && lastPathUpdateSeconds >= 0.75f && targetPos.SquareDistanceTo(targetEntity.ServerPos.XYZ) >= 9f) {
					targetPos.Set(targetEntity.ServerPos.X + targetEntity.ServerPos.Motion.X * 10.0, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z + targetEntity.ServerPos.Motion.Z * 10.0);
					pathTraverser.WalkTowards(targetPos, (float)entity.moveSpeed, TargetDist(), OnGoals, OnStuck);
					lastPathUpdateSeconds = 0f;
				}
				if (attackPattern == EnumAttackPattern.DirectAttack || attackPattern == EnumAttackPattern.BesiegeTarget) {
					pathTraverser.CurrentTarget.X = targetEntity.ServerPos.X;
					pathTraverser.CurrentTarget.Y = targetEntity.ServerPos.Y;
					pathTraverser.CurrentTarget.Z = targetEntity.ServerPos.Z;
				}
			}
			Vec3d vec = entity.ServerPos.XYZ.Add(0.0, entity.SelectionBox.Y2 / 2f, 0.0).Ahead(entity.SelectionBox.XSize / 2f, 0f, entity.ServerPos.Yaw);
			double num1 = targetEntity.SelectionBox.ToDouble().Translate(targetEntity.ServerPos.XYZ).ShortestDistanceFrom(vec);
			bool flag3 = targetEntity != null && targetEntity.Alive && !cancelSearch && pathTraverser.Active;
			if (attackPattern == EnumAttackPattern.TacticalRetreat) {
				if (flag3 && currentFollowTime < 9f) {
					return num1 < retreatRange;
				}
				return false;
			}
			if (flag3 && currentFollowTime < maxFollowTime && num1 < seekingRange) {
				if (!(num1 > TargetDist())) {
					if (targetEntity is EntityAgent entityAgent) {
						return entityAgent?.ServerControls?.TriesToMove ?? false;
					}
					return false;
				}
				return true;
			}
			return false;
		}

		public override void FinishExecute(bool cancelled) {
			cooldownUntilMs = entity.World.ElapsedMilliseconds + mincooldown + entity.World.Rand.Next(maxcooldown - mincooldown);
			cooldownUntilTotalHours = entity.World.Calendar.TotalHours + mincooldownHours + entity.World.Rand.NextDouble() * (maxcooldownHours - mincooldownHours);
			lastFinishedAtMs = entity.World.ElapsedMilliseconds;
			if (targetEntity == null || !targetEntity.Alive) {
				targetEntity = null;
				targetPos = null;
				cancelSearch = true;
				pathTraverser.Stop();
			}
			entity.AnimManager.StopAnimation(walkAnimCode);
			entity.AnimManager.StopAnimation(moveAnimCode);
			if (!entity.Swimming && entity.AnimManager.IsAnimationActive(swimAnimCode)) {
				entity.AnimManager.StopAnimation(swimAnimCode);
			}
		}

		public override bool Notify(string key, object data) {
			if (key == "seekEntity" && data != null) {
				targetEntity = (Entity)data;
				targetPos = targetEntity.ServerPos.XYZ;
				return true;
			}
			return false;
		}

		public void SetTargetEnts(Entity target) {
			targetEntity = target;
			targetPos = target.ServerPos.XYZ;
		}

		private void DoDirect() {
			int searchDepth = (world.Rand.NextDouble() < 0.05) ? 10000 : 3500;
			attackPattern = EnumAttackPattern.DirectAttack;
			MoveAnimation();
			pathTraverser.NavigateTo_Async(targetPos, curMoveSpeed, TargetDist(), OnGoals, OnStuck, DoSieged, searchDepth, 1);
		}

		private void DoSieged() {
			// Unable to perform direct attack pattern, trying sieged!
			attackPattern = EnumAttackPattern.BesiegeTarget;
			MoveAnimation();
			pathTraverser.NavigateTo_Async(targetPos, curMoveSpeed, TargetDist(), OnGoals, OnStuck, DoCircle, 3500, 3);
		}

		private void DoCircle() {
			// Unable to perform sieged attack pattern, trying circle!
			if (targetPos.DistanceTo(entity.ServerPos.XYZ) > seekingRange) {
				Retreats();
				return;
			}
			attackPattern = EnumAttackPattern.CircleTarget;
			lastPathfind = false;
			float num1 = (float)Math.Atan2(entity.ServerPos.X - targetPos.X, entity.ServerPos.Z - targetPos.Z);
			for (int i = 0; i < 3; i++) {
				double value = (double)num1 + 0.5 + world.Rand.NextDouble() / 2.0;
				double num2 = 4.0 + world.Rand.NextDouble() * 6.0;
				double x = GameMath.Sin(value) * num2;
				double z = GameMath.Cos(value) * num2;
				targetPos.Add(x, 0.0, z);
				int num3 = 0;
				bool flag = false;
				BlockPos blockPos = new BlockPos((int)targetPos.X, (int)targetPos.Y, (int)targetPos.Z, targetPos.AsBlockPos.dimension);
				int num4 = 0;
				while (num3 < 5) {
					if (world.BlockAccessor.GetBlock(new BlockPos(blockPos.X, blockPos.Y - num4, blockPos.Z, blockPos.dimension)).SideSolid[BlockFacing.UP.Index] && !world.CollisionTester.IsColliding(world.BlockAccessor, entity.SelectionBox, new Vec3d((double)blockPos.X + 0.5, blockPos.Y - num4 + 1, (double)blockPos.Z + 0.5), alsoCheckTouch: false)) {
						flag = true;
						targetPos.Y -= num4;
						targetPos.Y += 1.0;
						break;
					}
					if (world.BlockAccessor.GetBlock(new BlockPos(blockPos.X, blockPos.Y + num4, blockPos.Z, blockPos.dimension)).SideSolid[BlockFacing.UP.Index] && !world.CollisionTester.IsColliding(world.BlockAccessor, entity.SelectionBox, new Vec3d((double)blockPos.X + 0.5, blockPos.Y + num4 + 1, (double)blockPos.Z + 0.5), alsoCheckTouch: false)) {
						flag = true;
						targetPos.Y += num4;
						targetPos.Y += 1.0;
						break;
					}
					num3++;
					num4++;
				}
				if (flag) {
					MoveAnimation();
					pathTraverser.NavigateTo_Async(targetPos, curMoveSpeed, TargetDist(), OnGoals, OnStuck, Retreats, 3500, 1);
					return;
				}
			}
			Retreats();
		}

		private void Retreats() {
			// Unable to perform circle attack pattern, trying retreat!
			if (!RemainInOffenseMode && (RecentlyHurt || RemainInRetreatMode)) {
				updateTargetPosFleeMode(targetPos);
				MoveAnimation();
				pathTraverser.WalkTowards(targetPos, curMoveSpeed, targetEntity.SelectionBox.XSize + 0.2f, OnGoals, OnStuck);
				if (attackPattern != EnumAttackPattern.TacticalRetreat) {
					lastRetreatsAtMs = entity.World.ElapsedMilliseconds;
				}
				attackPattern = EnumAttackPattern.TacticalRetreat;
				attackedByEntity = null;
			}
		}

		private void OnStuck() {
			cancelSearch = true;
		}

		private void OnGoals() {
			if (attackPattern != 0 && attackPattern != EnumAttackPattern.BesiegeTarget) {
				return;
			}
			if (lastGoalReachedPos != null && lastGoalReachedPos.SquareDistanceTo(entity.ServerPos) < 0.005f) {
				if (futilityCounters == null) {
					futilityCounters = new Dictionary<long, int>();
				} else {
					futilityCounters.TryGetValue(targetEntity.EntityId, out var value);
					value++;
					futilityCounters[targetEntity.EntityId] = value;
					if (value > 19) {
						return;
					}
				}
			}
			lastGoalReachedPos = new Vec3d(entity.Pos);
			pathTraverser.Retarget();
		}

		private void MoveAnimation() {
			if (cancelSearch) {
				curMoveSpeed = 0;
				entity.AnimManager.StopAnimation(walkAnimCode);
				entity.AnimManager.StopAnimation(moveAnimCode);
				entity.AnimManager.StopAnimation(swimAnimCode);
			} else if (entity.Swimming) {
				curMoveSpeed = (float)entity.moveSpeed;
				entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = swimAnimCode, Code = swimAnimCode, BlendMode = EnumAnimationBlendMode.Average }.Init());
				entity.AnimManager.StopAnimation(walkAnimCode);
				entity.AnimManager.StopAnimation(moveAnimCode);
			} else if (entity.ServerPos.SquareDistanceTo(targetPos) > 4 * 4) {
				curMoveSpeed = (float)entity.moveSpeed;
				entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = moveAnimCode, Code = moveAnimCode, MulWithWalkSpeed = true, BlendMode = EnumAnimationBlendMode.Average }.Init());
				entity.AnimManager.StopAnimation(walkAnimCode);
				entity.AnimManager.StopAnimation(swimAnimCode);
			} else {
				curMoveSpeed = (float)entity.walkSpeed;
				entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = walkAnimCode, Code = walkAnimCode, MulWithWalkSpeed = true, BlendMode = EnumAnimationBlendMode.Average, EaseOutSpeed = 1f }.Init());
				entity.AnimManager.StopAnimation(moveAnimCode);
				entity.AnimManager.StopAnimation(swimAnimCode);
			}
		}

		private bool NotSafe() {
			if (targetEntity.ServerPos.SquareHorDistanceTo(entity.ServerPos.XYZ) > 4 * 4) {
				return false;
			}
			if (entity.ServerPos.Y - targetEntity.ServerPos.Y > 4) {
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

		private bool InReach() {
			double num = targetEntity.ServerPos.SquareDistanceTo(entity.ServerPos.XYZ);
			if (archerPilled) {
				return num < (2d * 2d) && entity.RightHandItemSlot?.Itemstack?.Item is ItemBow && !entity.AmmoItemSlot.Empty;
			}
			return num > (minRange * minRange);
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

		private float TargetDist() {
			return extraTargetDistance + Math.Max(0.1f, targetEntity.SelectionBox.XSize / 2f + entity.SelectionBox.XSize / 4f);
		}
	}
}