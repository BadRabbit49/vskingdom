using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VSKingdom {
	public class AiTaskSentryReturn : AiTaskBase {
		public AiTaskSentryReturn(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		protected bool cancelReturn = false;
		protected long lastCheckTotalMs;
		protected long lastWasInRangeMs;
		protected long lastCheckCooldown = 1500L;
		protected Vec3d postBlock { get => entity.Loyalties.GetBlockPos("outpost_xyzd").ToVec3d(); }

		public override bool ShouldExecute() {
			if (lastCheckTotalMs + lastCheckCooldown > entity.World.ElapsedMilliseconds) {
				return false;
			}
			lastCheckTotalMs = entity.World.ElapsedMilliseconds;
			if (entity.ruleOrder[6]) {
				return true;
			}
			return CheckDistance();
		}

		public override void StartExecute() {
			pathTraverser.NavigateTo(postBlock, (float)entity.moveSpeed, (float)entity.postRange, OnGoals, OnStuck, true);
			base.StartExecute();
		}

		public override bool ContinueExecute(float dt) {
			if (entity.ruleOrder[1] || CheckDistance() || cancelReturn) {
				cancelReturn = true;
				return false;
			}
			if (lastCheckCooldown + 500 < entity.World.ElapsedMilliseconds && postBlock != null && entity.MountedOn is null) {
				lastCheckCooldown = entity.World.ElapsedMilliseconds;
			}
			return true;
		}

		private void OnStuck() {
			cancelReturn = CheckTeleport();
		}

		private void OnGoals() {
			cancelReturn = true;
			pathTraverser.Retarget();
		}

		private void UpdateOrders(bool @returning) {
			SentryOrders updatedOrders = new SentryOrders() { entityUID = entity.EntityId, returning = @returning };
			IServerPlayer nearestPlayer = entity.ServerAPI.World.NearestPlayer(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z) as IServerPlayer;
			entity.ServerAPI?.Network.GetChannel("sentrynetwork").SendPacket<SentryOrders>(updatedOrders, nearestPlayer);
		}

		private bool CheckTeleport() {
			if (entity.ServerPos.XYZ.SquareDistanceTo(postBlock) > entity.postRange * entity.postRange) {
				// If after 2 minutes still not at spawn and no player nearby, teleport back home and set command return to false.
				var nearestPlayer = entity.World.NearestPlayer(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Entity;
				if (entity.Alive && entity.World.ElapsedMilliseconds - lastWasInRangeMs > 1000 * 60 * 2 && nearestPlayer.ServerPos.DistanceTo(entity.ServerPos) > 50) {
					entity.TeleportTo(postBlock);
					UpdateOrders(false);
					return true;
				}
			}
			return false;
		}

		private bool CheckDistance() {
			double boundaries = entity.postRange;
			if (entity.ruleOrder[3]) {
				boundaries = entity.postRange * 4;
			}
			// Set command to return if the outpost is further away than the boundaries allowed, and entity isn't following player.
			if (entity.Alive && entity.ServerPos.SquareDistanceTo(postBlock) > boundaries * boundaries && !entity.ruleOrder[1]) {
				lastWasInRangeMs = entity.World.ElapsedMilliseconds;
				UpdateOrders(true);
				return false;
			}
			return !entity.ruleOrder[6];
		}
	}
}