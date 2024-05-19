using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VSKingdom {
	public class AiTaskSoldierGuardingPos : AiTaskBase {
		public AiTaskSoldierGuardingPos(EntityAgent entity) : base(entity) { }

		public Vec3d PosXYZ;
		float maxDistance = 10f;
		bool stuck = false;

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) { }

		public override bool ShouldExecute() {
			// TODO: Setup guarding execution parameters.
			return false;
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

		private void OnStuck() {
			stuck = true;
		}

		private void OnGoalReached() {
			// Do nothing.
		}
	}
}