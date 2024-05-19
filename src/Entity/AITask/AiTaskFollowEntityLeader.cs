using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class AiTaskFollowEntityLeader : AiTaskStayCloseToEntity {
		public AiTaskFollowEntityLeader(EntityAgent entity) : base(entity) { }

		protected double targetX;
		protected double targetY;
		protected double targetZ;
		protected float targetD;

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			allowTeleport &= entity.Api.World.Config.GetAsBool("AllowTeleport");
		}

		public override void AfterInitialize() {
			base.AfterInitialize();
		}

		public override bool ShouldExecute() {
			if (entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("currentCommand") != "FOLLOW") {
				return false;
			}
			if (targetEntity is null || !targetEntity.Alive || targetEntity.ShouldDespawn || !targetEntity.IsInteractable) {
				return false;
			}
			return SoldierUtility.CanFollowThis(entity, targetEntity);
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

			return !stuck && pathTraverser.Active;
		}

		public override void OnNoPath(Vec3d target) {
			// Do nothing.
		}

		private void UpdatedXYZ() {
			targetX = targetEntity.ServerPos.X + targetOffset.X;
			targetY = targetEntity.ServerPos.Y;
			targetZ = targetEntity.ServerPos.Z + targetOffset.Z;
			targetD = entity.ServerPos.SquareDistanceTo(targetX, targetY, targetZ);
		}
	}
}