using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using System.Linq;

namespace VSKingdom {
	public class AiTaskSentrySearch : AiTaskSeekEntity {
		public AiTaskSentrySearch(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		protected long lastCheckTotalMs;
		protected long lastCheckForHelp;
		protected long lastCheckCooldown = 500L;
		protected float minRange;

		public override bool ShouldExecute() {
			if (lastCheckTotalMs + lastCheckCooldown > entity.World.ElapsedMilliseconds) {
				return false;
			}
			lastCheckTotalMs = entity.World.ElapsedMilliseconds;
			if (targetEntity != null && targetEntity.Alive && EntityInReach(targetEntity)) {
				targetPos = targetEntity.ServerPos.XYZ;
				return true;
			}
			targetEntity = null;
			if (attackedByEntity != null && attackedByEntity.Alive && EntityInReach(attackedByEntity) && attackedByEntity != entity) {
				targetEntity = attackedByEntity;
				targetPos = targetEntity.ServerPos.XYZ;
				return true;
			}
			attackedByEntity = null;
			if (lastSearchTotalMs + searchWaitMs < entity.World.ElapsedMilliseconds) {
				lastSearchTotalMs = entity.World.ElapsedMilliseconds;
				targetEntity = partitionUtil.GetNearestInteractableEntity(entity.ServerPos.XYZ, seekingRange, potentialTarget => potentialTarget != entity && IsTargetableEntity(potentialTarget, seekingRange));
				if (targetEntity != null && targetEntity.Alive && EntityInReach(targetEntity)) {
					targetPos = targetEntity.ServerPos.XYZ;
					return true;
				}
				targetEntity = null;
			}
			return false;
		}

		public override bool IsTargetableEntity(Entity ent, float range, bool ignoreEntityCode = false) {
			if (ent is null) {
				return false;
			}
			if (ent.WatchedAttributes.HasAttribute("loyalties")) {
				if (ent is EntitySentry sent) {
					return entity.enemiesID.Contains(sent.kingdomID);
				}
				return entity.enemiesID.Contains(ent.WatchedAttributes.GetTreeAttribute("loyalties").GetString("kingdom_guid"));
			}
			if (ent == attackedByEntity && ent != null && ent.Alive) {
				return true;
			}
			return base.IsTargetableEntity(ent, range, ignoreEntityCode);
		}

		public override bool ContinueExecute(float dt) {
			if (targetEntity != null && EntityInReach(targetEntity)) {
				return base.ContinueExecute(dt);
			}
			return false;
		}

		public override void OnEntityHurt(DamageSource source, float damage) {
			base.OnEntityHurt(source, damage);
			if (source.Type != EnumDamageType.Heal && lastCheckForHelp + 5000 < entity.World.ElapsedMilliseconds) {
				lastCheckForHelp = entity.World.ElapsedMilliseconds;
				// Alert all surrounding units! We're under attack!
				foreach (var soldier in entity.World.GetEntitiesAround(entity.ServerPos.XYZ, 20, 4, entity => (entity is EntitySentry))) {
					var taskManager = soldier.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
					taskManager.GetTask<AiTaskSentrySearch>()?.OnAllyAttacked(source.SourceEntity);
					taskManager.GetTask<AiTaskSentryAttack>()?.OnAllyAttacked(source.SourceEntity);
					taskManager.GetTask<AiTaskSentryRanged>()?.OnAllyAttacked(source.SourceEntity);
				}
			}
		}

		public void OnAllyAttacked(Entity byEntity) {
			if (targetEntity is null || !targetEntity.Alive) {
				targetEntity = byEntity;
			}
			ShouldExecute();
		}

		public void OnEnemySpotted(Entity targetEnt) {
			if (targetEntity is null || !targetEntity.Alive) {
				targetEntity = targetEnt;
			}
			ShouldExecute();
		}
		
		private bool EntityInReach(Entity candidate) {
			double num = candidate.ServerPos.SquareDistanceTo(entity.ServerPos.XYZ);
			if (num < (double)(seekingRange * seekingRange * 2f)) {
				if (candidate is EntitySentry thisEnt && entity.RightHandItemSlot?.Itemstack?.Item is ItemBow) {
					return num > (8d * 8d) && !thisEnt.AmmoItemSlot.Empty;
				}
				return num > (double)(minRange * minRange);
			}
			return false;
		}
	}
}