using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VSKingdom {
	public class AiTaskSentryReturn : AiTaskBase {
		public AiTaskSentryReturn(EntityAgent entity) : base(entity) { }

		protected bool finished = false;
		protected long lastCheckTotalMs;
		protected long lastCheckCooldown = 500L;
		protected BlockPos defaultPos = new BlockPos(0, 0, 0, 0);

		private ITreeAttribute loyalties;
		private bool returning { get => loyalties.GetBool("command_return"); }
		private bool following { get => loyalties.GetBool("command_follow"); }
		private double outpostSize { get => loyalties.GetDouble("outpost_size"); }
		private BlockPos outpostXYZD { get => loyalties.GetBlockPos("outpost_xyzd"); }
		
		public override void AfterInitialize() {
			base.AfterInitialize();
			loyalties = entity.WatchedAttributes.GetTreeAttribute("loyalties");
		}

		public override bool ShouldExecute() {
			if (!returning) {
				CheckDistance();
				return false;
			}
			if (lastCheckTotalMs + lastCheckCooldown > entity.World.ElapsedMilliseconds) {
				return false;
			}
			lastCheckTotalMs = entity.World.ElapsedMilliseconds;
			return entity.ServerPos.SquareDistanceTo(outpostXYZD.ToVec3d()) > outpostSize;
		}

		public override void StartExecute() {
			if (outpostXYZD is null) {
				finished = true;
				return;
			}
			finished = !pathTraverser.NavigateTo(outpostXYZD.ToVec3d(), 0.035f, 0.5f, ArrivedAtPost, ArrivedAtPost, true, 10000);
			if (finished) {
				ArrivedAtPost();
			} else {
				base.StartExecute();
			}
		}

		public override bool ContinueExecute(float dt) {
			if (following || !returning) {
				finished = true;
			}
			if (lastCheckCooldown + 500 < entity.World.ElapsedMilliseconds && outpostXYZD is not null && entity.MountedOn is null) {
				lastCheckCooldown = entity.World.ElapsedMilliseconds;
				if (entity.ServerPos.SquareDistanceTo(outpostXYZD.ToVec3d()) < 2) {
					ArrivedAtPost();
				}
			}
			return finished;
		}

		public override void FinishExecute(bool cancelled) {
			pathTraverser.Stop();
			entity.GetBehavior<EntityBehaviorLoyalties>()?.SetCommand("command_return", false);
			base.FinishExecute(cancelled);
		}

		private void CheckDistance() {
			if (outpostXYZD is not null && outpostXYZD != defaultPos) {
				if (entity.ServerPos.SquareDistanceTo(outpostXYZD.ToVec3d()) < outpostSize && !following) {
					entity.GetBehavior<EntityBehaviorLoyalties>()?.SetCommand("command_return", true);
				}
			}
		}

		private void ArrivedAtPost() {
			finished = true;
			pathTraverser.Stop();
			CheckDistance();
			entity.AnimManager.StopAnimation(animMeta.Code);
			entity.GetBehavior<EntityBehaviorLoyalties>()?.SetCommand("command_return", false);
		}
	}
}