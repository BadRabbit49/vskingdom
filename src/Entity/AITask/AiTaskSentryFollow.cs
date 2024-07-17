using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class AiTaskSentryFollow : AiTaskStayCloseToEntity {
		public AiTaskSentryFollow(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		
		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			allowTeleport &= entity.Api.World.Config.GetAsBool("AllowTeleport");
		}

		public override bool ShouldExecute() {
			if (!entity.ruleOrder[1]) {
				return false;
			}
			targetEntity = GetGuardedEntity();
			return targetEntity != null && targetEntity.Alive && targetEntity.ShouldDespawn == false && targetEntity.IsInteractable;
		}

		public override void StartExecute() {
			base.StartExecute();
			long[] followers = (targetEntity.WatchedAttributes.GetAttribute("followerEntityUids") as LongArrayAttribute)?.value;
			float size = targetEntity.SelectionBox.XSize;
			for (int i = 0; i < followers.Length; i++) {
				size += entity.World.GetEntityById(followers[i])?.SelectionBox.XSize ?? 0;
			}
			pathTraverser.NavigateTo_Async(targetEntity.ServerPos.XYZ, (float)entity.moveSpeed, size + 0.2f, OnGoalReached, () => stuck = true, null, 1000, 1);
			targetOffset.Set(entity.World.Rand.NextDouble() * 2 - 1, 0, entity.World.Rand.NextDouble() * 2 - 1);
			stuck = false;
			// Overridden base method to avoid constant teleporting when stuck.
			if (allowTeleport && entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos.X + targetOffset.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z + targetOffset.Z) > teleportAfterRange * teleportAfterRange) {
				tryTeleport();
			}
		}
		
		public Entity GetGuardedEntity() {
			long entityGUID = entity.WatchedAttributes.GetLong("guardedEntityId", 0);
			if (entityGUID != 0) {
				return entity.ServerAPI?.World.GetEntityById(entityGUID);
			}
			return null;
		}
	}
}