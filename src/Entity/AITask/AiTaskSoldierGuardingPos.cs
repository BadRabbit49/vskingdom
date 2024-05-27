using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VSKingdom {
	public class AiTaskSoldierGuardingPos : AiTaskBase {
		public AiTaskSoldierGuardingPos(EntityAgent entity) : base(entity) { }

		public bool commandActive;
		public Vec3d PosXYZ;

		protected bool stuck = false;
		protected float maxDistance = 10f;

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			commandActive = false;
		}

		public override void AfterInitialize() {
			base.AfterInitialize();
			pathTraverser = entity.GetBehavior<EntityBehaviorTraverser>()?.waypointsTraverser;
		}

		public override bool ShouldExecute() {
			// TODO: Setup guarding execution parameters.
			return commandActive;
		}

		public override void StartExecute() {
			base.StartExecute();
			if (PosXYZ != null) {
				pathTraverser.WalkTowards(PosXYZ, 0.04f, maxDistance / 2, OnGoalReached, OnStuck);
			}
			stuck = false;
		}

		public override bool ContinueExecute(float dt) {
			if (PosXYZ is null) {
				return false;
			}
			if (entity.ServerPos.SquareDistanceTo(PosXYZ.X, PosXYZ.Y, PosXYZ.Z) < maxDistance * maxDistance / 4) {
				pathTraverser.Stop();
				return false;
			}
			return !stuck && pathTraverser.Active;
		}

		public void SetActive(bool active) {
			commandActive = active;
		}

		private void OnStuck() {
			stuck = true;
		}

		private void OnGoalReached() {
			// Do nothing.
		}
	}
}