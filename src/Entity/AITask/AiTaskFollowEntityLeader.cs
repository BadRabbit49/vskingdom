using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class AiTaskFollowEntityLeader : AiTaskStayCloseToEntity {
		public AiTaskFollowEntityLeader(EntityAgent entity) : base(entity) { }

		public bool commandActive { get; set; }

		protected double targetX;
		protected double targetY;
		protected double targetZ;
		protected float targetD;

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			allowTeleport &= entity.Api.World.Config.GetAsBool("AllowTeleport");
			commandActive = false;
		}

		public override void AfterInitialize() {
			base.AfterInitialize();
			pathTraverser = entity.GetBehavior<EntityBehaviorTraverser>()?.waypointsTraverser;
		}

		public override bool ShouldExecute() {
			if (!commandActive) {
				return false;
			}
			if (targetEntity is null || !targetEntity.Alive || targetEntity.ShouldDespawn || !targetEntity.IsInteractable) {
				return false;
			}
			return DataUtility.IsAFriend(entity, targetEntity);
		}

		public override void StartExecute() {
			base.StartExecute();
			float size = targetEntity.SelectionBox.XSize;
			pathTraverser.NavigateTo_Async(targetEntity.ServerPos.XYZ, moveSpeed, size + 0.2f, OnGoalReached, () => stuck = true, null, 1000, 1);
			targetOffset.Set(entity.World.Rand.NextDouble() * 2 - 1, 0, entity.World.Rand.NextDouble() * 2 - 1);
			stuck = false;
			UpdatedXYZ();
			// Overridden base method to avoid constant teleporting when stuck.
			if (allowTeleport && targetD > teleportAfterRange * teleportAfterRange) {
				tryTeleport();
			}
		}

		public override bool ContinueExecute(float dt) {
			UpdatedXYZ();
			pathTraverser.CurrentTarget.X = targetX;
			pathTraverser.CurrentTarget.Y = targetY;
			pathTraverser.CurrentTarget.Z = targetZ;
			if (targetD < 3 * 3) {
				pathTraverser.Stop();
				return false;
			}
			if (allowTeleport && targetD > teleportAfterRange * teleportAfterRange) {
				tryTeleport();
			}
			return !stuck && pathTraverser.Active && commandActive;
		}

		public override void OnNoPath(Vec3d target) {
			// Do nothing.
		}

		public void SetTraverser(EntityBehaviorTraverser traverser) {
			pathTraverser = traverser.waypointsTraverser;
		}

		public void SetActive(bool active, Entity leader) {
			commandActive = active;
			targetEntity = leader;
		}

		private void UpdatedXYZ() {
			targetX = targetEntity.ServerPos.X + targetOffset.X;
			targetY = targetEntity.ServerPos.Y;
			targetZ = targetEntity.ServerPos.Z + targetOffset.Z;
			targetD = entity.ServerPos.SquareDistanceTo(targetX, targetY, targetZ);
		}
	}
}