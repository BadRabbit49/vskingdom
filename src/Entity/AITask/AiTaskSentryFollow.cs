using Vintagestory.API.Common;
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
			if (targetEntity is null || !targetEntity.Alive || targetEntity.ShouldDespawn || !targetEntity.IsInteractable) {
				return false;
			}
			return true;
		}

		public override void StartExecute() {
			base.StartExecute();
			float size = targetEntity.SelectionBox.XSize;
			pathTraverser.NavigateTo_Async(targetEntity.ServerPos.XYZ, moveSpeed, size + 0.2f, OnGoalReached, () => stuck = true, null, 1000, 1);
			targetOffset.Set(entity.World.Rand.NextDouble() * 2 - 1, 0, entity.World.Rand.NextDouble() * 2 - 1);
			stuck = false;
			// Overridden base method to avoid constant teleporting when stuck.
			if (allowTeleport && entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos.X + targetOffset.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z + targetOffset.Z) > teleportAfterRange * teleportAfterRange) {
				tryTeleport();
			}
		}

		public override void FinishExecute(bool cancelled) {
			base.FinishExecute(cancelled);
		}

		public Entity GetGuardedEntity() {
			string @string = entity.WatchedAttributes.GetString("guardedPlayerUid");
			if (@string != null) {
				return entity.World.PlayerByUid(@string)?.Entity;
			}
			long @long = entity.WatchedAttributes.GetLong("guardedEntityId", 0L);
			return entity.World.GetEntityById(@long);
		}
	}
}