using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace VSKingdom {
	public class AiTaskSoldierReturningTo : AiTaskBase {
		public AiTaskSoldierReturningTo(EntityAgent entity) : base(entity) { }

		public float moveSpeed = 0.035f;

		public bool completed;
		public bool canReturn;

		long lastCheckTotalMs { get; set; }
		long lastCheckCooldown { get; set; } = 500;

		private BlockEntityPost post;
		private SoldierWaypointsTraverser soldierPathTraverser;

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			post = null;
			completed = false;
			canReturn = false;
		}

		public override void AfterInitialize() {
			base.AfterInitialize();
		}

		public override bool ShouldExecute() {
			if (entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("currentCommand") != "RETURN") {
				CheckDistance();
				return false;
			}
			if (!canReturn) {
				return false;
			}
			if (lastCheckTotalMs + lastCheckCooldown > entity.World.ElapsedMilliseconds) {
				return false;
			}
			lastCheckTotalMs = entity.World.ElapsedMilliseconds;
			return entity.ServerPos.SquareDistanceTo(post.Position) > post.areasize;
		}

		public override void StartExecute() {
			if (post != null) {
				completed = !soldierPathTraverser.NavigateTo(post.Pos.ToVec3d(), moveSpeed, 0.5f, ArrivedAtPost, ArrivedAtPost, true, 10000);
			} else {
				completed = true;
			}
			if (completed) {
				ArrivedAtPost();
			} else {
				base.StartExecute();
			}
		}

		public override bool ContinueExecute(float dt) {
			if (lastCheckCooldown + 500 < entity.World.ElapsedMilliseconds && post != null && entity.MountedOn is null) {
				lastCheckCooldown = entity.World.ElapsedMilliseconds;
				if (entity.ServerPos.SquareDistanceTo(post.Pos.ToVec3d()) < 2) {
					ArrivedAtPost();
				}
			}
			return completed;
		}

		public override void FinishExecute(bool cancelled) {
			soldierPathTraverser.Stop();
			base.FinishExecute(cancelled);
		}

		public void UpdatePostEnt(BlockEntityPost soldierPost) {
			post = soldierPost;
			canReturn = true;
		}

		public void SetTraverser(EntityBehaviorTraverser traverser) {
			soldierPathTraverser = traverser.waypointsTraverser;
		}

		private void CheckDistance() {
			if (post != null) {
				if (entity.ServerPos.SquareDistanceTo(post.Pos.ToVec3d()) < post.areasize) {
					// Arrived, now set back to wandering mode.
					entity.WatchedAttributes.GetTreeAttribute("loyalties")?.SetString("currentCommand", "RETURN");
					entity.WatchedAttributes.MarkPathDirty("loyalties");
				}
			}
		}

		private void ArrivedAtPost() {
			completed = true;
			soldierPathTraverser.Stop();
			CheckDistance();
			entity.AnimManager.StopAnimation(animMeta.Code);
			entity.WatchedAttributes.GetTreeAttribute("loyalties")?.SetString("currentCommand", "WANDER");
			entity.WatchedAttributes.MarkPathDirty("loyalties");
		}
	}
}