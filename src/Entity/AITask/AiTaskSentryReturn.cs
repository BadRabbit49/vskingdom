using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VSKingdom {
	public class AiTaskSentryReturn : AiTaskBase {
		public AiTaskSentryReturn(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		protected bool cancelReturn = false;
		protected long lastCheckTotalMs;
		protected long lastCheckCooldown = 500L;
		private BlockPos outpostXYZD { get => entity.Loyalties.GetBlockPos("outpost_xyzd"); }


		public override bool ShouldExecute() {
			if (lastCheckTotalMs + lastCheckCooldown > entity.World.ElapsedMilliseconds) {
				return false;
			}
			lastCheckTotalMs = entity.World.ElapsedMilliseconds;
			return CheckDistance();
		}

		public override void StartExecute() {
			if (outpostXYZD is null) {
				cancelReturn = true;
				return;
			}
			cancelReturn = !pathTraverser.NavigateTo(outpostXYZD.ToVec3d(), (float)entity.moveSpeed, (float)entity.postRange, OnStuck, OnGoals, true, 10000);
			base.StartExecute();
		}

		public override bool ContinueExecute(float dt) {
			if (entity.ruleOrder[1] || CheckDistance()) {
				cancelReturn = true;
			}
			if (lastCheckCooldown + 500 < entity.World.ElapsedMilliseconds && outpostXYZD is not null && entity.MountedOn is null) {
				lastCheckCooldown = entity.World.ElapsedMilliseconds;
			}
			return cancelReturn;
		}

		public override void FinishExecute(bool cancelled) {
			base.FinishExecute(cancelled);
			pathTraverser.Stop();
			if (cancelReturn && entity.ruleOrder[6] && entity.ServerPos.DistanceTo(outpostXYZD.ToVec3d()) < entity.postRange) {
				entity.ServerAPI?.World.GetEntityById(entity.EntityId)?.GetBehavior<EntityBehaviorLoyalties>()?.SetCommand("command_return", false);
				entity.ServerAPI?.Network.BroadcastEntityPacket(entity.EntityId, 1503);
			}
		}

		private void OnStuck() {
			cancelReturn = true;
			pathTraverser.Stop();
		}

		private void OnGoals() {
			cancelReturn = true;
			pathTraverser.Stop();
		}

		private bool CheckDistance() {
			double boundaries = entity.postRange;
			if (entity.ruleOrder[3]) {
				boundaries = entity.postRange * 10;
			}
			// Set command to return if the outpost is further away than the boundaries allowed, and entity isn't following player.
			if (entity.ServerPos.DistanceTo(outpostXYZD.ToVec3d()) > boundaries && !entity.ruleOrder[1]) {
				entity.ServerAPI?.World.GetEntityById(entity.EntityId)?.GetBehavior<EntityBehaviorLoyalties>()?.SetCommand("command_return", true);
				entity.ServerAPI?.Network.BroadcastEntityPacket(entity.EntityId, 1503);
				return false;
			}
			return !entity.ruleOrder[6];
		}
	}
}