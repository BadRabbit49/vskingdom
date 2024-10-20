using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class AiTaskSentryDoctor : AiTaskBaseTargetable {
		public AiTaskSentryDoctor(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		protected bool cancelDoctor = false;
		protected bool animsRunning = false;
		protected bool needToRevive = false;
		protected long durationOfMs = 8000L;
		protected long lastHealedMs;
		protected float maximumRange = 20f;
		protected float minimumRange = 0.5f;
		protected float curTurnAngle;
		protected string lastAnimCode;
		protected string currAnimCode;
		protected string[] animations;
		protected AiTaskManager tasksManager;
		protected AiTaskSentrySearch searchTask => tasksManager.GetTask<AiTaskSentrySearch>();

		public override void AfterInitialize() {
			world = entity.World;
			tasksManager = entity.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
		}

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			this.animations = taskConfig["animations"]?.AsArray<string>(new string[] { "bandageself" });
			this.mincooldown = taskConfig["mincooldown"].AsInt(500);
			this.maxcooldown = taskConfig["maxcooldown"].AsInt(8000);
			this.sound = new AssetLocation("game:sounds/player/poultice");
		}

		public override bool ShouldExecute() {
			if (cooldownUntilMs > entity.World.ElapsedMilliseconds) {
				return false;
			}
			Vec3d position = entity.ServerPos.XYZ.Add(0.0, entity.SelectionBox.Y2 / 2f, 0.0).Ahead(entity.SelectionBox.XSize / 2f, 0f, entity.ServerPos.Yaw);
			maximumRange = entity.WatchedAttributes.GetFloat("engageRange", 16f);
			targetEntity = TriagePatients(entity.World.GetEntitiesAround(position, maximumRange, maximumRange / 2f, (Entity ent) => IsTargetableEntity(ent, maximumRange) && hasDirectContact(ent, maximumRange, maximumRange / 2f)));
			cooldownUntilMs = entity.World.ElapsedMilliseconds + durationOfMs;
			return targetEntity != null;
		}

		public override bool IsTargetableEntity(Entity ent, float range, bool ignoreEntityCode = false) {
			if (ent is null || ent == entity || !ent.Alive) {
				return false;
			}
			if (ent.WatchedAttributes.HasAttribute(KingdomGUID) && !IsAnEnemy(ent) && ent.WatchedAttributes.HasAttribute("health")) {
				var healthTree = targetEntity.WatchedAttributes.GetTreeAttribute("health");
				float curHealth = healthTree.GetFloat("currenthealth") / healthTree.GetFloat("basemaxhealth");
				return !ent.Alive || curHealth < 0.5f;
			}
			return false;
		}

		public override void StartExecute() {
			// Record last animMeta so we can stop it if we need to.
			lastAnimCode = currAnimCode;
			cancelDoctor = false;
			animsRunning = false;
			needToRevive = !targetEntity?.Alive ?? false;
			curTurnAngle = pathTraverser.curTurnRadPerSec;
			searchTask.SetTargetEnts(targetEntity);
			searchTask.DoMovePattern(EnumAttackPattern.DirectAttack);
		}

		public override void OnEntityHurt(DamageSource source, float damage) {
			if (source.GetCauseEntity() != null && IsAnEnemy(source.GetCauseEntity())) {
				pathTraverser.Stop();
				pathTraverser.Retarget();
				searchTask.SetTargetEnts(source.GetCauseEntity());
				searchTask.DoMovePattern(EnumAttackPattern.TacticalRetreat);
			}
		}

		public override bool ContinueExecute(float dt) {
			try {
				if (cancelDoctor || (!targetEntity?.Alive ?? true)) {
					return false;
				}
				EntityPos ownPos = entity.ServerPos;
				EntityPos hisPos = targetEntity.ServerPos;
				float num = GameMath.AngleRadDistance(entity.ServerPos.Yaw, (float)Math.Atan2(hisPos.X - ownPos.X, hisPos.Z - ownPos.Z));
				entity.ServerPos.Yaw += GameMath.Clamp(num, (0f - curTurnAngle) * dt * GlobalConstants.OverallSpeedMultiplier, curTurnAngle * dt * GlobalConstants.OverallSpeedMultiplier);
				entity.ServerPos.Yaw %= (MathF.PI * 2f);
				bool flag = Math.Abs(num) < maximumRange * (MathF.PI / 180f);
				animsRunning = lastAnimCode != null ? entity.AnimManager.GetAnimationState(lastAnimCode).Running : false;
				if (!animsRunning && flag) {
					animsRunning = TendToTarget();
					targetEntity.Revive();
				}
				return lastHealedMs + durationOfMs > entity.World.ElapsedMilliseconds;
			} catch {
				return false;
			}
		}
		
		public override void FinishExecute(bool cancelled) {
			cooldownUntilMs = entity.World.ElapsedMilliseconds + mincooldown + entity.World.Rand.Next(maxcooldown - mincooldown);
			if ((!targetEntity?.Alive ?? true) || !IsTargetableEntity(targetEntity, (float)targetEntity.ServerPos.DistanceTo(entity.ServerPos))) {
				searchTask?.ResetsTargets();
				searchTask?.StopMovements();
			}
			if (currAnimCode != null) {
				entity.AnimManager.StopAnimation(currAnimCode);
			}
		}

		private bool TendToTarget() {
			if (!hasDirectContact(targetEntity, 1, 0)) {
				return false;
			}
			entity.AnimManager.StopAnimation(lastAnimCode);
			entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = currAnimCode, Code = currAnimCode }.Init());
			entity.World.PlaySoundAt(sound, entity, null, true, soundRange);
			var healthTree = targetEntity.WatchedAttributes.GetTreeAttribute("health");
			float curHealth = healthTree.GetFloat("currenthealth") / healthTree.GetFloat("basemaxhealth");
			float healthDmg = curHealth < 0.75f ? (entity.HealItemSlot?.Itemstack.Collectible.Attributes["health"].AsFloat() ?? 0.01f) : 0.01f;
			bool usedItems = curHealth < 0.75f || !entity.HealItemSlot.Empty;
			if (needToRevive) {
				targetEntity.Revive();
				targetEntity.WatchedAttributes.GetTreeAttribute("health").SetFloat("currenthealth", entity.HealItemSlot.Itemstack.ItemAttributes["health"].AsFloat(1));
				targetEntity.WatchedAttributes.MarkPathDirty("health");
			} else {
				targetEntity.ReceiveDamage(new DamageSource {
					Source = EnumDamageSource.Entity,
					SourceEntity = entity,
					Type = EnumDamageType.Heal
				}, healthDmg);
			}
			if (usedItems) {
				entity.HealItemSlot.TakeOut(1);
				entity.HealItemSlot.MarkDirty();
			}
			// Only jump back if they killing blow was not dealt.
			searchTask.ResetsTargets();
			searchTask.StopMovements();
			cancelDoctor = true;
			return false;
		}

		private bool IsAnEnemy(Entity target) {
			if (target is EntitySentry sentry) {
				return entity.cachedData.enemiesLIST.Contains(sentry.cachedData.kingdomGUID) || sentry.cachedData.kingdomGUID == BanditrysID;
			}
			if (target is EntityPlayer player) {
				return entity.cachedData.outlawsLIST.Contains(player.PlayerUID) || entity.cachedData.enemiesLIST.Contains(player.WatchedAttributes.GetString(KingdomGUID));
			}
			return entity.cachedData.enemiesLIST.Contains(target.WatchedAttributes.GetString(KingdomGUID));
		}

		private Entity TriagePatients(Entity[] targetList) {
			Entity topPriority = null;
			float topPriorityHp = 1f;
			foreach (var target in targetList) {
				var healthTree = target.WatchedAttributes.GetTreeAttribute("health");
				float curHealth = healthTree.GetFloat("currenthealth") / healthTree.GetFloat("basemaxhealth");
				if (curHealth < topPriorityHp && entity.World.BlockAccessor.IsValidPos(target.ServerPos.AsBlockPos)) {
					if (!target.Alive) {
						if (!entity.HealItemSlot.Empty) {
							topPriority = target;
							topPriorityHp = curHealth;
						}
						continue;
					}
					topPriority = target;
					topPriorityHp = curHealth;
				}
			}
			return topPriority ?? null;
		}
	}
}