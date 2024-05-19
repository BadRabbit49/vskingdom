using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using System.Linq;

namespace VSKingdom {
	public class AiTaskSoldierTargetables : AiTaskBaseTargetable {
		public AiTaskSoldierTargetables(EntityAgent entity) : base(entity) { }

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) { }

		public override bool ShouldExecute() {
			if (cooldownUntilMs > entity.World.ElapsedMilliseconds) {
				return false;
			}
			if (targetEntity == entity) {
				return false;
			}
			// STEP 1: FIND A TARGETABLE ENTITY.
			// STEP 2: DETERMINE ACTION BASED ON GEAR.
			// STEP 3: EXECUTE AITASK WITH ENTITY.
			if (IsTargetableEntity(targetEntity, 18)) {
				AiTaskManager taskManager = entity.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
				taskManager.GetTask<AiTaskSoldierSeeksEntity>()?.OnEnemySpotted(targetEntity);
				taskManager.GetTask<AiTaskSoldierMeleeAttack>()?.OnEnemySpotted(targetEntity);
				taskManager.GetTask<AiTaskSoldierRangeAttack>()?.OnEnemySpotted(targetEntity);
			}
			// Never fully execute this function.
			return false;
		}
		
		public override bool IsTargetableEntity(Entity ent, float range, bool ignoreEntityCode = false) {
			if (ent is null) {
				return false;
			}
			if (!ent.IsCreature) {
				return false;
			}
			if (!ent.Alive) {
				return false;
			}
			if (ent is EntityDrifter) {
				return true;
			}
			if (ent is EntityBell) {
				return true;
			}
			if (ent is EntityHumanoid) {
				return DataUtility.IsAnEnemy(entity, ent);
			}
			return base.IsTargetableEntity(ent, range, ignoreEntityCode);
		}

		public override void OnEntityHurt(DamageSource damageSource, float damage) {
			Entity attacker = damageSource.SourceEntity;
			// Avoid friendly fire, but don't go after one another for revenge if it happens!
			if (attacker is EntityProjectile && damageSource.CauseEntity != null) {
				attacker = damageSource.CauseEntity;
			}
			attackedByEntity = attacker;
			attackedByEntityMs = entity.World.ElapsedMilliseconds;
			return;
		}

		public virtual void ClearTargetHistory() {
			targetEntity = null;
			attackedByEntity = null;
			attackedByEntityMs = 0;
		}
	}
}