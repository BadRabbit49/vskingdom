using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VSKingdom {
	public class AiTaskSoldierSeeksEntity : AiTaskSeekEntity {
		public AiTaskSoldierSeeksEntity(EntityAgent entity) : base(entity) { }

		protected long lastCheckTotalMs { get; set; }
		protected long lastCheckCooldown { get; set; } = 500L;
		protected long lastCallForHelp { get; set; }

		protected float minRange;

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
		}

		public override bool ShouldExecute() {
			if (whenInEmotionState is null) {
				return false;
			}
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
			if (ent is EntityHumanoid) {
				return DataUtility.IsAnEnemy(entity, ent);
			}
			if (ent == attackedByEntity && ent != null && ent.Alive) {
				return true;
			}
			return base.IsTargetableEntity(ent, range, ignoreEntityCode);
		}

		public override void StartExecute() {
			base.StartExecute();
			world.Logger.Chat("Started Seeking Execute on: " + targetEntity.ToString());
		}

		public override bool ContinueExecute(float dt) {
			if (targetEntity != null && EntityInReach(targetEntity)) {
				return base.ContinueExecute(dt);
			}
			return false;
		}

		public override void OnEntityHurt(DamageSource source, float damage) {
			base.OnEntityHurt(source, damage);
			if (source.Type != EnumDamageType.Heal && lastCallForHelp + 5000 < entity.World.ElapsedMilliseconds) {
				lastCallForHelp = entity.World.ElapsedMilliseconds;
				// Alert all surrounding units! We're under attack!
				foreach (var soldier in entity.World.GetEntitiesAround(entity.ServerPos.XYZ, 20, 4, entity => (entity is EntityArcher))) {
					var taskManager = soldier.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
					taskManager.GetTask<AiTaskSoldierSeeksEntity>()?.OnAllyAttacked(source.SourceEntity);
					taskManager.GetTask<AiTaskSoldierMeleeAttack>()?.OnAllyAttacked(source.SourceEntity);
					taskManager.GetTask<AiTaskSoldierRangeAttack>()?.OnAllyAttacked(source.SourceEntity);
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
				if (HasRanged()) {
					return num > (8d * 8d);
				}
				return num > (double)(minRange * minRange);
			}
			return false;
		}

		private bool HasRanged() {
			return RegisteredItems.AcceptedRange.Contains(entity.RightHandItemSlot?.Itemstack?.Collectible?.Code);
		}
	}
}