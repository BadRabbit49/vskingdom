using Vintagestory.API.Common;
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
		protected float groupsOffset;
		protected float squaredRange;
		protected float curMoveSpeed;
		protected string curAnimation;
		protected float followsRange { get => entity.WatchedAttributes.GetFloat("followRange", 2f); }
		protected Vec3d curTargetPos { get => pathTraverser.CurrentTarget.Clone(); }
		
		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			this.curMoveSpeed = taskConfig["curMoveSpeed"].AsFloat(0.03f);
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
			targetEntity = GetGuardedEntity();
			long[] followers = (targetEntity.WatchedAttributes.GetAttribute("followerEntityUids") as LongArrayAttribute)?.value;
			groupsOffset = targetEntity.SelectionBox.XSize + 0.2f;
			for (int i = 0; i < followers.Length; i++) {
				groupsOffset += entity.World.GetEntityById(followers[i])?.SelectionBox.XSize ?? 0;
				if (entity.EntityId == followers[i]) { break; }
			}
			pathTraverser.NavigateTo_Async(targetEntity.ServerPos.XYZ, curMoveSpeed, groupsOffset, OnGoals, OnStuck, null, 1000, 1);
			targetOffset.Set(entity.World.Rand.NextDouble() * 2 - 1, 0, entity.World.Rand.NextDouble() * 2 - 1);
			stuck = false;
			// Overridden base method to avoid constant teleporting when stuck.
			if (allowTeleport && entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos.X + targetOffset.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z + targetOffset.Z) > teleportAfterRange * teleportAfterRange) {
				tryTeleport();
			}
			squaredRange = followsRange * followsRange;
			MoveAnimation();
		}

		public override bool CanContinueExecute() {
			return pathTraverser.Ready && entity.ruleOrder[1];
		}

		public override bool ContinueExecute(float dt) {
			double x = targetEntity.ServerPos.X + targetOffset.X;
			double y = targetEntity.ServerPos.Y;
			double z = targetEntity.ServerPos.Z + targetOffset.Z;
			pathTraverser.CurrentTarget.X = x;
			pathTraverser.CurrentTarget.Y = y;
			pathTraverser.CurrentTarget.Z = z;
			float num = entity.ServerPos.SquareDistanceTo(x, y, z);
			if (num < squaredRange) {
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
			if (!entity.ruleOrder[1]) {
				long[] followers = (targetEntity.WatchedAttributes.GetAttribute("followerEntityUids") as LongArrayAttribute)?.value;
				targetEntity.WatchedAttributes.SetAttribute("followerEntityUids", new LongArrayAttribute(followers.Remove(entity.EntityId)));
				entity.WatchedAttributes.SetBool("orderFollow", false);
				entity.WatchedAttributes.SetLong("guardedEntityId", 0);
				pathTraverser.Stop();
			}
			StopAnimation();
		}

		protected void IsStuck() {
			stuck = true;
		}

		protected void OnGoals() {
			pathTraverser.Stop();
			MoveAnimation();
			pathTraverser.NavigateTo_Async(targetEntity.ServerPos.XYZ, curMoveSpeed, groupsOffset, OnGoals, OnStuck, null, 1000, 1);
		}

		private void MoveAnimation() {
			if (!entity.ruleOrder[1]) {
				curMoveSpeed = 0;
				StopAnimation();
				return;
			}
			double distance = entity.ServerPos.SquareDistanceTo(curTargetPos);
			entity.AnimManager.StopAnimation(curAnimation);
			if (entity.FeetInLiquid && !entity.Swimming) {
				curMoveSpeed = entity.cachedData.walkSpeed;
				curAnimation = new string(entity.cachedData.walkAnims);
			} else if (entity.Swimming) {
				curMoveSpeed = entity.cachedData.moveSpeed;
				curAnimation = new string(entity.cachedData.swimAnims);
			} else if (distance > 81f) {
				curMoveSpeed = entity.cachedData.moveSpeed;
				curAnimation = new string(entity.cachedData.moveAnims);
			} else if (distance > 4f && distance < 81f) {
				curMoveSpeed = entity.cachedData.walkSpeed;
				curAnimation = new string(entity.cachedData.walkAnims);
			} else {
				StopAnimation();
				return;
			}
			entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = curAnimation, Code = curAnimation, MulWithWalkSpeed = true, BlendMode = EnumAnimationBlendMode.Average, EaseInSpeed = 999f, EaseOutSpeed = 1f }.Init());
		}

		private void StopAnimation() {
			curMoveSpeed = 0;
			if (curAnimation != null) {
				entity.AnimManager.StopAnimation(curAnimation);
			}
			entity.AnimManager.StopAnimation(new string(entity.cachedData.walkAnims));
			entity.AnimManager.StopAnimation(new string(entity.cachedData.moveAnims));
			entity.AnimManager.StopAnimation(new string(entity.cachedData.swimAnims));
		}
	}
}