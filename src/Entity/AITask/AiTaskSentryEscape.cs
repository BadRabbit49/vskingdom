using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class AiTaskSentryEscape : AiTaskFleeEntity {
		public AiTaskSentryEscape(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		public override bool AggressiveTargeting => false;

		protected bool cancelEscape;
		protected long fleeingStartMs;
		protected long fleeDurationMs = 5000L;
		protected float fleeRange = 30f;
		protected Vec3d targetPos = new Vec3d();

		private ITreeAttribute healthTree;
		private float CurHealth => healthTree.GetFloat("currenthealth");
		private float MaxHealth => healthTree.GetFloat("basemaxhealth");

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			fleeRange = taskConfig["fleeRange"].AsFloat(30f);
			fleeDurationMs = taskConfig["fleeDurationMs"].AsInt(5000);
			JsonObject jsonObject = taskConfig["animation"];
			if (jsonObject.Exists) {
				AnimationMetaData animationMetaData = entity.Properties.Client.Animations.FirstOrDefault((AnimationMetaData a) => a.Code == jsonObject.AsString()?.ToLowerInvariant());
				if (animationMetaData != null) {
					animMeta = animationMetaData;
				} else {
					animMeta = new AnimationMetaData {
						Code = taskConfig["animation"].AsString("move").ToLowerInvariant(),
						Animation = taskConfig["animation"].AsString("move").ToLowerInvariant(),
						AnimationSpeed = taskConfig["animationSpeed"].AsFloat(1),
					}.Init();
					animMeta.EaseInSpeed = 1f;
					animMeta.EaseOutSpeed = 1f;
				}
			}
		}

		public override void AfterInitialize() {
			base.AfterInitialize();
			healthTree = entity.WatchedAttributes.GetTreeAttribute("health");
		}

		public override bool ShouldExecute() {
			// If this flee behavior is due to the 'fleeondamage' condition, then lets make it react 4 times quicker!
			if (rand.NextDouble() > 3 * 0.1) {
				return false;
			}
			/**if (noEntityCodes && (attackedByEntity is null || !retaliateAttacks)) {
				return false;
			}
			entity.World.FrameProfiler.Mark("task-fleeentity-shouldexecute-init");
			entity.World.FrameProfiler.Mark("task-fleeentity-shouldexecute-entitysearch");**/
			if (targetEntity != null) {
				updateTargetPosFleeMode(targetPos);
				return true;
			}
			return targetEntity != null && targetEntity.Alive;
		}

		public override void StartExecute() {
			base.StartExecute();
			cancelEscape = false;
			soundChance = Math.Max(0.025f, soundChance - 0.2f);
			pathTraverser.NavigateTo(targetPos, (float)entity.moveSpeed, targetEntity.SelectionBox.XSize + 0.2f, OnGoals, OnStuck);
			fleeingStartMs = entity.World.ElapsedMilliseconds;
		}

		public override bool ContinueExecute(float dt) {
			if (world.Rand.NextDouble() < 0.2) {
				updateTargetPosFleeMode(targetPos);
				pathTraverser.CurrentTarget.X = targetPos.X;
				pathTraverser.CurrentTarget.Y = targetPos.Y;
				pathTraverser.CurrentTarget.Z = targetPos.Z;
				pathTraverser.Retarget();
			}
			if (entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos) > fleeRange * fleeRange) {
				return false;
			}
			return !cancelEscape && targetEntity != null && targetEntity.Alive && (entity.World.ElapsedMilliseconds - fleeingStartMs < fleeDurationMs) && pathTraverser.Active;
		}

		public override void OnEntityHurt(DamageSource source, float damage) {
			base.OnEntityHurt(source, damage);
			cancelEscape = ShouldFleeTarget();
		}

		public override void FinishExecute(bool cancelled) {
			cancelEscape = true;
			targetEntity = null;
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

		public void SetTargetEnts(Entity target) {
			targetEntity = target;
		}

		private void OnStuck() {
			cancelEscape = true;
		}

		private void OnGoals() {
			pathTraverser.Retarget();
		}

		private bool ShouldFleeTarget() {
			if (targetEntity == null || !targetEntity.Alive) {
				return false;
			}
			if (entity.weapClass == "range" && hasDirectContact(targetEntity, 4f, 4f)) {
				return true;
			}
			if (targetEntity.HasBehavior<EntityBehaviorHealth>()) {
				Vec3d targetPosOffset = new Vec3d().Set(entity.World.Rand.NextDouble() * 2.0 - 1.0, 0.0, entity.World.Rand.NextDouble() * 2.0 - 1.0);
				// Now if we have some breathing room to reevaluate the situation, see if we should continue this fight or not.
				if (entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos.X + targetPosOffset.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z + targetPosOffset.Z) > 9) {
					// Determine if enemy has more health, armor, and is stronger.
					ITreeAttribute targetHealthTree = targetEntity.WatchedAttributes.GetTreeAttribute("health");
					return (CurHealth / MaxHealth) < 0.25 && (targetHealthTree.GetFloat("currenthealth") / targetHealthTree.GetFloat("basemaxhealth")) > 0.25;
				}
			}
			return false;
		}
	}
}