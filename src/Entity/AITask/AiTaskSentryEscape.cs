using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class AiTaskSentryEscape : AiTaskFleeEntity {
		public AiTaskSentryEscape(EntityAgent entity) : base(entity) { }

		protected bool stuck;
		protected bool finished;
		protected long fleeStartMs;
		protected long fleeDurationMs = 9000L;
		protected float moveSpeed = 0.035f;
		protected float seekRange = 25f;
		protected float executionChance = 0.1f;
		protected float fleeingDistance = 30f;
		protected Vec3d targetPos = new Vec3d();

		private ITreeAttribute healthTree;
		private float CurHealth => healthTree.GetFloat("currenthealth");
		private float MaxHealth => healthTree.GetFloat("basemaxhealth");

		public override bool AggressiveTargeting => false;

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			moveSpeed = taskConfig["movespeed"].AsFloat(0.035f);
			seekRange = taskConfig["seekingRange"].AsFloat(25);
			executionChance = taskConfig["executionChance"].AsFloat(0.1f);
			fleeingDistance = taskConfig["fleeingDistance"].AsFloat(seekRange + 15f);
			fleeDurationMs = taskConfig["fleeDurationMs"].AsInt(9000);
		}

		public override void AfterInitialize() {
			base.AfterInitialize();
			healthTree = entity.WatchedAttributes.GetTreeAttribute("health");
		}

		public override bool ShouldExecute() {
			soundChance = Math.Min(1.01f, soundChance + 1 / 500f);
			// If this flee behavior is due to the 'fleeondamage' condition, then lets make it react 4 times quicker!
			if (rand.NextDouble() > 3 * executionChance) {
				return false;
			}
			if (noEntityCodes && (attackedByEntity is null || !retaliateAttacks)) {
				return false;
			}
			entity.World.FrameProfiler.Mark("task-fleeentity-shouldexecute-entitysearch");
			if (targetEntity is null || targetEntity?.Alive == false) {
				return false;
			} else {
				updateTargetPosFleeMode(targetEntity.Pos.XYZ);
			}
			if (DataUtility.IsAnEnemy(entity, targetEntity)) {
				return ShouldFleeTarget();
			}
			return false;
		}

		public override void StartExecute() {
			base.StartExecute();
			finished = false;
			soundChance = Math.Max(0.025f, soundChance - 0.2f);
			float size = targetEntity.SelectionBox.XSize;
			pathTraverser.WalkTowards(targetPos, moveSpeed, size + 0.2f, OnGoalReached, OnStuck);
			fleeStartMs = entity.World.ElapsedMilliseconds;
			stuck = false;
			entity.CurrentControls = EnumEntityActivity.SprintMode;
		}

		public override bool ContinueExecute(float dt) {
			if (world.Rand.NextDouble() < 0.2) {
				updateTargetPosFleeMode(targetPos);
				pathTraverser.CurrentTarget.X = targetPos.X;
				pathTraverser.CurrentTarget.Y = targetPos.Y;
				pathTraverser.CurrentTarget.Z = targetPos.Z;
				pathTraverser.Retarget();
			}
			if (entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos) > fleeingDistance * fleeingDistance) {
				return false;
			}
			return !stuck && targetEntity.Alive && (entity.World.ElapsedMilliseconds - fleeStartMs < fleeDurationMs) && !finished && pathTraverser.Active;
		}

		public override void OnEntityHurt(DamageSource source, float damage) {
			base.OnEntityHurt(source, damage);
			finished = ShouldFleeTarget();
		}

		public override void FinishExecute(bool cancelled) {
			pathTraverser.Stop();
			base.FinishExecute(cancelled);
		}

		public override bool CanSense(Entity ent, double range) {
			// Unfortunate minor amount of code duplication but here we need the base method without the e.IsInteractable check
			if (ent.EntityId == entity.EntityId) {
				return false;
			}
			if (ent is EntityPlayer player) {
				return CanSensePlayer(player, range);
			}
			if (skipEntityCodes != null) {
				for (int i = 0; i < skipEntityCodes.Length; i++) {
					if (WildcardUtil.Match(skipEntityCodes[i], ent.Code)) {
						return false;
					}
				}
			}
			return true;
		}

		private void OnStuck() {
			stuck = true;
		}

		private void OnGoalReached() {
			pathTraverser.Retarget();
		}

		private bool ShouldFleeTarget() {
			if (!targetEntity.Alive) {
				return false;
			}
			if (targetEntity.HasBehavior<EntityBehaviorHealth>()) {
				Vec3d targetPosOffset = new Vec3d().Set(entity.World.Rand.NextDouble() * 2.0 - 1.0, 0.0, entity.World.Rand.NextDouble() * 2.0 - 1.0);
				// Now if we have some breathing room to reevaluate the situation, see if we should continue this fight or not.
				if (entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos.X + targetPosOffset.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z + targetPosOffset.Z) > 3) {
					// Determine if enemy has more health, armor, and is stronger.
					ITreeAttribute targetHealthTree = targetEntity.WatchedAttributes.GetTreeAttribute("health");
					return (CurHealth / MaxHealth) < 0.25 && (targetHealthTree.GetFloat("currenthealth") / targetHealthTree.GetFloat("basemaxhealth")) > 0.25;
				}
			}
			return false;
		}
	}
}