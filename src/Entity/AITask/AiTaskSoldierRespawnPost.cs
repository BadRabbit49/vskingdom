using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace VSKingdom {
	public class AiTaskSoldierRespawnPost : AiTaskBase {
		public AiTaskSoldierRespawnPost(EntityAgent entity) : base(entity) { }
		
		protected BlockEntityPost post;

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) { }

		public override bool ShouldExecute() {
			// TODO: CHECK IF THEY HAVE ORDERS TO FOLLOW OR STAY AT THEIR POSITION. OTHERWISE LET THEM RETURN TO THEIR POST BLOCK.
			// Soldier must be dead and have their guardPost resupply have respawns available.
			if (!entity.Alive && post != null) {
				if (post.soldierIds.Contains(entity.EntityId) && post.respawns > 0) {
					return true;
				}
			}
			return false;
		}

		public override void StartExecute() {
			entity.TeleportTo(post.Position);
			post.UseRespawn();
			base.StartExecute();
		}

		public override void FinishExecute(bool cancelled) {
			base.FinishExecute(cancelled);
			entity.Revive();
		}

		public void SetBlockPost(BlockEntityPost soldierPost) {
			post = soldierPost;
		}
	}
}