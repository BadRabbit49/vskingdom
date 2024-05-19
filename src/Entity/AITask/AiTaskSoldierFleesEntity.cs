using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class AiTaskSoldierFleesEntity : AiTaskFleeEntity {
		long fleeStartMs;
		float moveSpeed = 0.035f;
		float seekingRange = 25f;
		float executionChance = 0.1f;
		float fleeingDistance = 30f;
		float fleeDurationMs = 5000;
		bool cancelOnHurt = false;
		bool stuck;
		bool cancelNow;
		Vec3d targetPos = new Vec3d();

		public override bool AggressiveTargeting => false;

		public AiTaskSoldierFleesEntity(EntityAgent entity) : base(entity) { }

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			moveSpeed = taskConfig["movespeed"].AsFloat(0.035f);
			seekingRange = taskConfig["seekingRange"].AsFloat(25);
			executionChance = taskConfig["executionChance"].AsFloat(0.1f);
			cancelOnHurt = taskConfig["cancelOnHurt"].AsBool(false);
			fleeingDistance = taskConfig["fleeingDistance"].AsFloat(seekingRange + 15);
			fleeDurationMs = taskConfig["fleeDurationMs"].AsInt(9000);
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
			return SoldierUtility.ShouldFleeNow(entity, targetEntity);
		}

		public override void StartExecute() {
			base.StartExecute();
			cancelNow = false;
			soundChance = Math.Max(0.025f, soundChance - 0.2f);
			float size = targetEntity.SelectionBox.XSize;
			pathTraverser.WalkTowards(targetPos, moveSpeed, size + 0.2f, OnGoalReached, OnStuck);
			fleeStartMs = entity.World.ElapsedMilliseconds;
			stuck = false;
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
			return !stuck && targetEntity.Alive && (entity.World.ElapsedMilliseconds - fleeStartMs < fleeDurationMs) && !cancelNow && pathTraverser.Active;
		}

		public override void OnEntityHurt(DamageSource source, float damage) {
			base.OnEntityHurt(source, damage);
			if (cancelOnHurt) {
				cancelNow = true;
			}
		}

		public override void FinishExecute(bool cancelled) {
			pathTraverser.Stop();
			base.FinishExecute(cancelled);
		}

		private void OnStuck() {
			stuck = true;
		}

		private void OnGoalReached() {
			pathTraverser.Retarget();
		}

		public override bool CanSense(Entity ent, double range) {
			// Unfortunate minor amount of code duplication but here we need the base method without the e.IsInteractable check
			if (ent.EntityId == entity.EntityId) {
				return false;
			}
			if (ent is EntityPlayer eplr) {
				return CanSensePlayer(eplr, range);
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
	}
}