using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using System.Linq;
using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using VSEssentialsMod.Entity.AI.Task;

namespace VSKingdom {
	public class AiTaskSentrySearch : AiTaskBaseTargetable {
		public AiTaskSentrySearch(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		protected bool banditryBehavior = false;
		protected bool cancelSearch;
		protected bool jumpAnimOn;
		protected bool lastPathfind;
		protected bool leapAtTarget;
		protected float currentFollowTime;
		protected float extraTargetDistance;
		protected float lastPathUpdateSeconds;
		protected float leapChance = 1f;
		protected float leapHeightMul = 1f;
		protected float maxFollowTime = 60f;
		protected float minRange;
		protected float retreatRange = 20f;
		protected float seekingRange = 25f;
		protected long jumpedMS;
		protected long lastAttackedMs;
		protected long lastCheckCooldown = 500L;
		protected long lastCheckForHelp;
		protected long lastCheckTotalMs;
		protected long lastFinishedMs;
		protected long lastHurtByTargetTotalMs;
		protected long searchWaitMs = 4000;
		protected long tacticalRetreatBeginTotalMs;
		protected string jumpAnimCode = "jump";
		protected Vec3d lastGoalReachedPos;
		protected Vec3d targetPos;
		protected Dictionary<long, int> futilityCounters;
		protected EnumAttackPattern attackPattern;
		protected bool RecentlyHurt => entity.World.ElapsedMilliseconds - lastHurtByTargetTotalMs < 10000;
		protected bool RemainInTacticalRetreat => entity.World.ElapsedMilliseconds - tacticalRetreatBeginTotalMs < 20000;
		protected bool RemainInOffensiveMode => entity.World.ElapsedMilliseconds - lastAttackedMs < 20000;

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();
			retaliateAttacks = taskConfig["retaliateAttacks"].AsBool(defaultValue: true);
			triggerEmotionState = taskConfig["triggerEmotionState"].AsString();
			skipEntityCodes = taskConfig["skipEntityCodes"].AsArray<string>()?.Select((string str) => AssetLocation.Create(str, entity.Code.Domain)).ToArray();
			banditryBehavior = taskConfig["isBandit"].AsBool(false);
		}

		public override bool ShouldExecute() {
			return targetEntity != null && (targetEntity?.Alive ?? false);
		}

		public override bool IsTargetableEntity(Entity ent, float range, bool ignoreEntityCode = false) {
			if (ent == null || ent == entity || !ent.Alive) {
				return false;
			}
			if (ent is EntityProjectile projectile && projectile.FiredBy is not null) {
				targetEntity = projectile.FiredBy;
			}
			if (ent.WatchedAttributes.HasAttribute("loyalties")) {
				if (banditryBehavior) {
					return ent is EntityPlayer || (ent is EntitySentry sentry && sentry.kingdomID != "xxxxxxxx");
				}
				if (ent is EntitySentry sent) {
					return entity.enemiesID.Contains(sent.kingdomID) || sent.kingdomID == "xxxxxxxx";
				}
				return entity.enemiesID.Contains(ent.WatchedAttributes.GetTreeAttribute("loyalties").GetString("kingdom_guid"));
			}
			if (ent == attackedByEntity && ent != null && ent.Alive) {
				return true;
			}
			return base.IsTargetableEntity(ent, range, ignoreEntityCode);
		}

		public override void StartExecute() {
			cancelSearch = false;
			currentFollowTime = 0f;
			if (RemainInTacticalRetreat) {
				TryRetreating();
				return;
			}
			attackPattern = EnumAttackPattern.DirectAttack;
			targetPos = targetEntity.ServerPos.XYZ;
			int searchDepth = 3500;
			if (world.Rand.NextDouble() < 0.05) {
				searchDepth = 10000;
			}
			pathTraverser.NavigateTo_Async(targetPos.Clone(), (float)entity.moveSpeed, DistToTargets(), OnGoals, OnStuck, OnSeekUnable, searchDepth, 1);
		}

		public override bool CanContinueExecute() {
			if (pathTraverser.Ready) {
				lastAttackedMs = entity.World.ElapsedMilliseconds;
				lastPathfind = true;
			}
			if (!pathTraverser.Ready) {
				return attackPattern == EnumAttackPattern.TacticalRetreat;
			}
			return true;
		}

		public override bool ContinueExecute(float dt) {
			if (targetEntity == null || !targetEntity.Alive) {
				return false;
			}
			if (currentFollowTime == 0f && (!cancelSearch || world.Rand.NextDouble() < 0.25)) {
				base.StartExecute();
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
					TryRetreating();
				}
				if (attackPattern == EnumAttackPattern.DirectAttack && lastPathUpdateSeconds >= 0.75f && targetPos.SquareDistanceTo(targetEntity.ServerPos.XYZ) >= 9f) {
					targetPos.Set(targetEntity.ServerPos.X + targetEntity.ServerPos.Motion.X * 10.0, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z + targetEntity.ServerPos.Motion.Z * 10.0);
					pathTraverser.NavigateTo(targetPos, (float)entity.moveSpeed, DistToTargets(), OnGoals, OnStuck, giveUpWhenNoPath: false, 2000, 1);
					lastPathUpdateSeconds = 0f;
				}
				if (leapAtTarget && !entity.AnimManager.IsAnimationActive(animMeta.Code)) {
					RunningAnimation animationState = entity.AnimManager.Animator.GetAnimationState(jumpAnimCode);
					if (animationState == null || !animationState.Active) {
						animMeta.EaseInSpeed = 1f;
						animMeta.EaseOutSpeed = 1f;
						entity.AnimManager.StartAnimation(animMeta);
					}
				}
				if (jumpAnimOn && entity.World.ElapsedMilliseconds - lastFinishedMs > 2000) {
					entity.AnimManager.StopAnimation(jumpAnimCode);
					animMeta.EaseInSpeed = 1f;
					animMeta.EaseOutSpeed = 1f;
					entity.AnimManager.StartAnimation(animMeta);
				}
				if (attackPattern == EnumAttackPattern.DirectAttack || attackPattern == EnumAttackPattern.BesiegeTarget) {
					pathTraverser.CurrentTarget.X = targetEntity.ServerPos.X;
					pathTraverser.CurrentTarget.Y = targetEntity.ServerPos.Y;
					pathTraverser.CurrentTarget.Z = targetEntity.ServerPos.Z;
				}
			}

			Vec3d vec = entity.ServerPos.XYZ.Add(0.0, entity.SelectionBox.Y2 / 2f, 0.0).Ahead(entity.SelectionBox.XSize / 2f, 0f, entity.ServerPos.Yaw);
			double num = targetEntity.SelectionBox.ToDouble().Translate(targetEntity.ServerPos.XYZ).ShortestDistanceFrom(vec);
			if (leapAtTarget && base.rand.NextDouble() < (double)leapChance) {
				bool flag2 = entity.World.ElapsedMilliseconds - jumpedMS < 3000;
				if (num > 0.5 && num < 4.0 && !flag2 && targetEntity.ServerPos.Y + 0.1 >= entity.ServerPos.Y) {
					double num2 = (targetEntity.ServerPos.X + targetEntity.ServerPos.Motion.X * 80.0 - entity.ServerPos.X) / 30.0;
					double num3 = (targetEntity.ServerPos.Z + targetEntity.ServerPos.Motion.Z * 80.0 - entity.ServerPos.Z) / 30.0;
					entity.ServerPos.Motion.Add(num2, (double)leapHeightMul * GameMath.Max(0.13, (targetEntity.ServerPos.Y - entity.ServerPos.Y) / 30.0), num3);
					float yaw = (float)Math.Atan2(num2, num3);
					entity.ServerPos.Yaw = yaw;
					jumpedMS = entity.World.ElapsedMilliseconds;
					lastFinishedMs = entity.World.ElapsedMilliseconds;
					if (jumpAnimCode != null) {
						entity.AnimManager.StopAnimation("walk");
						entity.AnimManager.StopAnimation("move");
						entity.AnimManager.StartAnimation(new AnimationMetaData {
							Animation = jumpAnimCode,
							Code = jumpAnimCode
						}.Init());
						jumpAnimOn = true;
					}
				}
				if (flag2 && !entity.Collided && num < 0.5) {
					entity.ServerPos.Motion /= 2f;
				}
			}

			float num4 = DistToTargets();
			bool flag3 = targetEntity != null && targetEntity.Alive && !cancelSearch && pathTraverser.Active;
			if (attackPattern == EnumAttackPattern.TacticalRetreat) {
				if (flag3 && currentFollowTime < 9f) {
					return num < (double)retreatRange;
				}
				return false;
			}
			if (flag3 && currentFollowTime < maxFollowTime && num < seekingRange) {
				if (!(num > (double)num4)) {
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
			base.FinishExecute(cancelled);
			lastFinishedMs = entity.World.ElapsedMilliseconds;
			pathTraverser.Stop();
		}

		public override void OnEntityHurt(DamageSource source, float damage) {
			if (source.GetCauseEntity() == null || !IsTargetableEntity(source.GetCauseEntity(), (float)source.GetCauseEntity().Pos.DistanceTo(entity.Pos))) {
				return;
			}
			if (source.Type != EnumDamageType.Heal && lastCheckForHelp + 5000 < entity.World.ElapsedMilliseconds) {
				lastCheckForHelp = entity.World.ElapsedMilliseconds;
				// Alert all surrounding units! We're under attack!
				foreach (EntitySentry soldier in entity.World.GetEntitiesAround(entity.ServerPos.XYZ, 20, 4, entity => (entity is EntitySentry))) {
					if (entity.kingdomID == soldier.kingdomID) {
						var taskManager = soldier.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
						taskManager.GetTask<AiTaskSentryAttack>()?.OnAllyAttacked(source.SourceEntity);
						taskManager.GetTask<AiTaskSentryRanged>()?.OnAllyAttacked(source.SourceEntity);
					}
				}
			}
			base.OnEntityHurt(source, damage);
		}

		public override bool Notify(string key, object data) {
			if (key == "seekEntity") {
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

		public float DistToTargets() {
			return extraTargetDistance + Math.Max(0.1f, targetEntity.SelectionBox.XSize / 2f + entity.SelectionBox.XSize / 4f);
		}

		private void OnSeekUnable() {
			attackPattern = EnumAttackPattern.BesiegeTarget;
			pathTraverser.NavigateTo_Async(targetPos.Clone(), (float)entity.moveSpeed, DistToTargets(), OnGoals, OnStuck, OnSiegeUnable, 3500, 3);
		}

		private void OnSiegeUnable() {
			if (targetPos.DistanceTo(entity.ServerPos.XYZ) < seekingRange && !TryCircleTarget()) {
				OnCircleTargetUnable();
			}
		}

		public void OnCircleTargetUnable() {
			TryRetreating();
		}

		private bool TryCircleTarget() {
			targetPos.SquareDistanceTo(entity.Pos);
			int searchDepth = 3500;
			attackPattern = EnumAttackPattern.CircleTarget;
			lastPathfind = false;
			float num = (float)Math.Atan2(entity.ServerPos.X - targetPos.X, entity.ServerPos.Z - targetPos.Z);
			for (int i = 0; i < 3; i++) {
				double value = (double)num + 0.5 + world.Rand.NextDouble() / 2.0;
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
					pathTraverser.NavigateTo_Async(targetPos.Clone(), (float)entity.moveSpeed, DistToTargets(), OnGoals, OnStuck, OnCircleTargetUnable, searchDepth, 1);
					return true;
				}
			}
			return false;
		}

		private void TryRetreating() {
			if (!RemainInOffensiveMode && (RecentlyHurt || RemainInTacticalRetreat)) {
				updateTargetPosFleeMode(targetPos);
				float xSize = targetEntity.SelectionBox.XSize;
				pathTraverser.WalkTowards(targetPos, (float)entity.moveSpeed, xSize + 0.2f, OnGoals, OnStuck);
				if (attackPattern != EnumAttackPattern.TacticalRetreat) {
					tacticalRetreatBeginTotalMs = entity.World.ElapsedMilliseconds;
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
			if (lastGoalReachedPos != null && (double)lastGoalReachedPos.SquareDistanceTo(entity.Pos) < 0.001) {
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

		private bool InReach(Entity candidate) {
			double num = candidate.ServerPos.SquareDistanceTo(entity.ServerPos.XYZ);
			if (num < (double)(seekingRange * seekingRange * 2f)) {
				if (entity.weapClass == "range") {
					return num > (8d * 8d) && entity.RightHandItemSlot?.Itemstack?.Item is ItemBow && !entity.AmmoItemSlot.Empty;
				}
				return num > (double)(minRange * minRange);
			}
			return false;
		}
	}
}