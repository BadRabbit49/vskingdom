using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class AiTaskSentryFollow : AiTaskStayCloseToGuardedEntity {
		public AiTaskSentryFollow(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		protected float curMoveSpeed = 0.03f;
		protected Vec3d curTargetPos => pathTraverser.CurrentTarget;

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			allowTeleport &= entity.Api.World.Config.GetAsBool("AllowTeleport");
		}

		public override bool ShouldExecute() {
			if (!entity.ruleOrder[1]) {
				return false;
			}
			return base.ShouldExecute();
		}
		
		public override void StartExecute() {
			if (!targetEntity.WatchedAttributes.HasAttribute("followerEntityUids")) {
				targetEntity.WatchedAttributes.SetAttribute("followerEntityUids", new LongArrayAttribute(new long[] { entity.EntityId }));
			}
			long[] followers = (targetEntity.WatchedAttributes.GetAttribute("followerEntityUids") as LongArrayAttribute)?.value;
			float size = targetEntity.SelectionBox.XSize;
			for (int i = 0; i < followers.Length; i++) {
				size += entity.World.GetEntityById(followers[i])?.SelectionBox.XSize ?? 0;
			}
			pathTraverser.NavigateTo_Async(targetEntity.ServerPos.XYZ, entity.cachedData.moveSpeed, size + 0.2f, OnGoalReached, () => stuck = true, null, 1000, 1);
			targetOffset.Set(entity.World.Rand.NextDouble() * 2 - 1, 0, entity.World.Rand.NextDouble() * 2 - 1);
			stuck = false;
			// Overridden base method to avoid constant teleporting when stuck.
			if (allowTeleport && entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos.X + targetOffset.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z + targetOffset.Z) > teleportAfterRange * teleportAfterRange) {
				tryTeleport();
			}
			MoveAnimation();
		}

		public override bool CanContinueExecute() {
			if (!entity.ruleOrder[1]) {
				return false;
			}
			return pathTraverser.Ready;
		}

		public override bool ContinueExecute(float dt) {
			double x = targetEntity.ServerPos.X + targetOffset.X;
			double y = targetEntity.ServerPos.Y;
			double z = targetEntity.ServerPos.Z + targetOffset.Z;
			pathTraverser.CurrentTarget.X = x;
			pathTraverser.CurrentTarget.Y = y;
			pathTraverser.CurrentTarget.Z = z;
			float num = entity.ServerPos.SquareDistanceTo(x, y, z);
			if (num < 9f) {
				pathTraverser.Stop();
				return false;
			}
			if (allowTeleport && num > teleportAfterRange * teleportAfterRange && entity.World.Rand.NextDouble() < 0.05) {
				tryTeleport();
			}
			if (!stuck) {
				MoveAnimation();
				return pathTraverser.Active;
			}
			return false;
		}

		public override void FinishExecute(bool cancelled) {
			base.FinishExecute(cancelled);
			long[] followers = (targetEntity.WatchedAttributes.GetAttribute("followerEntityUids") as LongArrayAttribute)?.value;
			targetEntity.WatchedAttributes.SetAttribute("followerEntityUids", new LongArrayAttribute(followers.Remove(entity.EntityId)));
			StopAnimation();
		}

		private void MoveAnimation() {
			if (!pathTraverser.Active) {
				curMoveSpeed = 0;
				StopAnimation();
			} else if (entity.Swimming) {
				curMoveSpeed = entity.cachedData.moveSpeed * GlobalConstants.WaterDrag;
				entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = entity.cachedData.swimAnims, Code = entity.cachedData.swimAnims, BlendMode = EnumAnimationBlendMode.Average }.Init());
				entity.AnimManager.StopAnimation(entity.cachedData.walkAnims);
				entity.AnimManager.StopAnimation(entity.cachedData.moveAnims);
			} else if (entity.ServerPos.SquareDistanceTo(curTargetPos) > 81f) {
				curMoveSpeed = entity.cachedData.moveSpeed;
				entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = entity.cachedData.moveAnims, Code = entity.cachedData.moveAnims, MulWithWalkSpeed = true, BlendMode = EnumAnimationBlendMode.Average }.Init());
				entity.AnimManager.StopAnimation(entity.cachedData.walkAnims);
				entity.AnimManager.StopAnimation(entity.cachedData.swimAnims);
			} else if (entity.ServerPos.SquareDistanceTo(curTargetPos) > 1f) {
				curMoveSpeed = entity.cachedData.walkSpeed;
				entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = entity.cachedData.walkAnims, Code = entity.cachedData.walkAnims, MulWithWalkSpeed = true, BlendMode = EnumAnimationBlendMode.Average, EaseOutSpeed = 1f }.Init());
				entity.AnimManager.StopAnimation(entity.cachedData.moveAnims);
				entity.AnimManager.StopAnimation(entity.cachedData.swimAnims);
			} else {
				StopAnimation();
			}
		}

		private void StopAnimation() {
			entity.AnimManager.StopAnimation(entity.cachedData.walkAnims);
			entity.AnimManager.StopAnimation(entity.cachedData.moveAnims);
			if (!entity.Swimming) {
				entity.AnimManager.StopAnimation(entity.cachedData.swimAnims);
			}
		}
	}
}