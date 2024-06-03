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
		protected ITreeAttribute loyalties;

		protected virtual bool returning => loyalties.GetBool("command_return");
		protected virtual bool following => loyalties.GetBool("command_follow");
		protected virtual double outpostAreasize => loyalties.GetDouble("outpost_size");
		protected virtual BlockPos outpostBlockPos => loyalties.GetBlockPos("outpost_xyzd");
		
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
			return entity.ServerPos.SquareDistanceTo(outpostBlockPos.ToVec3d()) > outpostAreasize;
		}

		public override void StartExecute() {
			if (outpostBlockPos is null) {
				finished = true;
				return;
			}
			finished = !pathTraverser.NavigateTo(outpostBlockPos.ToVec3d(), 0.035f, 0.5f, ArrivedAtPost, ArrivedAtPost, true, 10000);
			if (finished) {
				ArrivedAtPost();
			} else {
				base.StartExecute();
			}
			entity.World.Logger.Notification("Started RETURNING...");
		}

		public override bool ContinueExecute(float dt) {
			if (following || !returning) {
				finished = true;
			}
			if (lastCheckCooldown + 500 < entity.World.ElapsedMilliseconds && outpostBlockPos is not null && entity.MountedOn is null) {
				lastCheckCooldown = entity.World.ElapsedMilliseconds;
				if (entity.ServerPos.SquareDistanceTo(outpostBlockPos.ToVec3d()) < 2) {
					ArrivedAtPost();
				}
			}
			return finished;
		}

		public override void FinishExecute(bool cancelled) {
			pathTraverser.Stop();
			loyalties.SetBool("command_return", false);
			base.FinishExecute(cancelled);
			entity.World.Logger.Notification("Stopped RETURNING...");
		}

		private void CheckDistance() {
			if (outpostBlockPos is not null && outpostBlockPos != defaultPos) {
				if (entity.ServerPos.SquareDistanceTo(outpostBlockPos.ToVec3d()) < outpostAreasize && !following) {
					loyalties.SetBool("command_return", true);
				}
			}
		}

		private void ArrivedAtPost() {
			finished = true;
			pathTraverser.Stop();
			CheckDistance();
			entity.AnimManager.StopAnimation(animMeta.Code);
			entity.WatchedAttributes.SetBool("command_return", false);
		}
	}
}