using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using VSKingdom.Utilities;
using Vintagestory.API.Server;

namespace VSKingdom {
	public class AiTaskZombieAttack : AiTaskBaseTargetable {
		public AiTaskZombieAttack(EntityZombie entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntityZombie entity;
		#pragma warning restore CS0108
		protected bool cancelAttack = false;
		protected bool animsRunning = false;
		protected bool killedTarget = false;
		protected bool hittingDoors = false;
		protected Int32 targetMemory = 50000;
		protected Int64 durationOfMs = 1500;
		protected Int64 cooldownAtMs = 0;
		protected Int64 lastEngageMs = 0;
		protected Int64 nextAlertsMs = 0;
		protected Int64 nextAttackMs = 0;
		protected float curTurnAngle = 0;
		protected string lastAnimCode;
		protected string currAnimCode;
		protected string[] animations;
		protected BlockPos barricaded;
		protected AiTaskManager tasksManager;
		protected AiTaskZombieSearch searcherTask => tasksManager.GetTask<AiTaskZombieSearch>();

		public override void AfterInitialize() {
			world = entity.Api.World;
			bhPhysics = entity.GetBehavior<EntityBehaviorControlledPhysics>();
			bhEmo = entity.GetBehavior<EntityBehaviorEmotionStates>();
			tasksManager = entity.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
			pathTraverser = entity.GetBehavior<EntityBehaviorTaskAI>().PathTraverser;
		}

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			this.partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();
			this.animations = taskConfig["animations"]?.AsArray<string>(new string[] { "hit" });
			this.mincooldown = taskConfig["mincooldown"].AsInt(500);
			this.maxcooldown = taskConfig["maxcooldown"].AsInt(1500);
		}

		public override bool ShouldExecute() {
			if (cooldownUntilMs > world.ElapsedMilliseconds) {
				return false;
			}
			cooldownUntilMs = world.ElapsedMilliseconds + durationOfMs;
			if (world.ElapsedMilliseconds - attackedByEntityMs > 30000) {
				attackedByEntity = null;
			}
			Vec3d position = entity.ServerPos.XYZ.Add(0.0, entity.SelectionBox.Y2 / 2f, 0.0).Ahead(entity.SelectionBox.XSize / 2f, 0f, entity.ServerPos.Yaw);
			if (rand.Next(0, 1) == 0) {
				targetEntity = world.GetNearestEntity(position, 0.5f, 32f / 2f, (Entity ent) => IsTargetableEntity(ent, 32f) && hasDirectContact(ent, 32f, 32f / 2f));
			} else {
				var targetList = world.GetEntitiesAround(position, 0.5f, 32f / 2f, (Entity ent) => IsTargetableEntity(ent, 32f) && hasDirectContact(ent, 32f, 32f / 2f));
				targetEntity = targetList[rand.Next(0, targetList.Length - 1)];
			}
			return targetEntity?.Alive ?? false;
		}

		public override void StartExecute() {
			// Record last animMeta so we can stop it if we need to.
			lastAnimCode = currAnimCode;
			// Initialize a random attack animation and sounds!
			currAnimCode = animations[world.Rand.Next(0, animations.Length - 1)];
			switch (world.Rand.Next(1, 2)) {
				case 1: sound = new AssetLocation("game:sounds/creature/drifter-hit"); break;
				case 2: sound = new AssetLocation("game:sounds/creature/fox/attack"); break;
			}
			cancelAttack = false;
			animsRunning = false;
			killedTarget = false;
			hittingDoors = searcherTask?.IsBarricaded(barricaded ?? null) ?? false;
			curTurnAngle = pathTraverser.curTurnRadPerSec;
			if (hittingDoors) {
				searcherTask.SetTargetVec3(barricaded.ToVec3d());
			} else {
				searcherTask.SetTargetEnts(targetEntity);
			}
		}
		
		public override bool ContinueExecute(float dt) {
			try {
				if (cancelAttack) {
					return false;
				}
				bool alreadyTrace = false;
				if (hittingDoors) {
					hittingDoors = searcherTask?.HitBarricade(barricaded) ?? false;
					lastEngageMs = world.ElapsedMilliseconds + targetMemory;
					if (TracingUtil.CanSeeEntity(entity, targetEntity)) {
						hittingDoors = false;
						alreadyTrace = true;
						lastEngageMs = world.ElapsedMilliseconds + targetMemory;
						searcherTask.SetTargetEnts(targetEntity);
					}
					if (hittingDoors) {
						return world.ElapsedMilliseconds < lastEngageMs;
					}
				}
				if (!targetEntity?.Alive ?? true) {
					return false;
				}
				EntityPos ownPos = entity.ServerPos;
				EntityPos hisPos = targetEntity.ServerPos;
				float num = GameMath.AngleRadDistance(entity.ServerPos.Yaw, (float)Math.Atan2(hisPos.X - ownPos.X, hisPos.Z - ownPos.Z));
				entity.ServerPos.Yaw += GameMath.Clamp(num, (0f - curTurnAngle) * dt * GlobalConstants.OverallSpeedMultiplier, curTurnAngle * dt * GlobalConstants.OverallSpeedMultiplier);
				entity.ServerPos.Yaw %= (MathF.PI * 2f);
				bool flag = Math.Abs(num) < 32f * (MathF.PI / 180f);
				animsRunning = lastAnimCode != null && entity.AnimManager.IsAnimationActive(lastAnimCode);
				if (!animsRunning && world.ElapsedMilliseconds > nextAttackMs && flag) {
					nextAttackMs = world.ElapsedMilliseconds + 1500L;
					killedTarget = AttackTarget();
				}
				if (alreadyTrace || TracingUtil.CanSeeEntity(entity, targetEntity)) {
					lastEngageMs = world.ElapsedMilliseconds + targetMemory;
					searcherTask.SetTargetEnts(targetEntity);
				}
				return !killedTarget && world.ElapsedMilliseconds < lastEngageMs;
			} catch {
				return false;
			}
		}

		public override void FinishExecute(bool cancelled) {
			cooldownUntilMs = world.ElapsedMilliseconds + mincooldown + world.Rand.Next(maxcooldown - mincooldown);
			if (!targetEntity?.Alive ?? true) {
				searcherTask?.ResetsTargets();
			}
			entity.AnimManager.StopAnimation(currAnimCode);
		}

		public override void OnEntityHurt(DamageSource source, float damage) {
			if (world.ElapsedMilliseconds < nextAlertsMs || source?.GetCauseEntity() == null) {
				return;
			}
			nextAlertsMs = world.ElapsedMilliseconds + 20000L;
			// Call the horde here! We're under attack!
			foreach (EntityZombie zombie in world.GetEntitiesAround(entity.ServerPos.XYZ, 20f, 4f, entity => (entity is EntityZombie))) {
				var taskManager = zombie.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
				taskManager.GetTask<AiTaskZombieAttack>()?.HordeAlerted(source.SourceEntity);
			}
			base.OnEntityHurt(source, damage);
		}

		public void HordeAlerted(Entity targetEnt) {
			if (!targetEntity?.Alive ?? true) {
				targetEntity = targetEnt;
			}
		}

		public void SetBarricade(BlockPos pos) {
			barricaded = pos.Copy();
		}
		
		private bool AttackTarget() {
			if (!hasDirectContact(targetEntity, 1.3f, 1.3f)) {
				return false;
			}
			entity.AnimManager.StopAnimation(lastAnimCode);
			entity.AnimManager.StartAnimation(new AnimationMetaData() {
				Animation = currAnimCode,
				Code = currAnimCode,
				BlendMode = EnumAnimationBlendMode.Add,
				ElementWeight = new Dictionary<string, float> {
					{ "UpperTorso", 5f },
					{ "UpperArmR", 10f },
					{ "LowerArmR", 10f },
					{ "UpperArmL", 10f },
					{ "LowerArmL", 10f }
				},
				ElementBlendMode = new Dictionary<string, EnumAnimationBlendMode> {
					{ "UpperTorso", EnumAnimationBlendMode.AddAverage },
					{ "LowerTorso", EnumAnimationBlendMode.AddAverage }
				}
			}.Init());
			int chances = rand.Next(0, 100);
			var hitType = EnumDamageType.BluntAttack;
			if (chances > 90) {
				// Bite attack!
				hitType = EnumDamageType.PiercingAttack;
			} else if (chances > 70) {
				// Scratch attack!
				hitType = EnumDamageType.SlashingAttack;
			}
			entity.World.PlaySoundAt(sound, entity, null, true, soundRange);
			bool alive = targetEntity.Alive;
			var damage = new DamageSource {
				Source = EnumDamageSource.Entity,
				SourceEntity = entity,
				Type = hitType,
				DamageTier = 3,
				KnockbackStrength = -1.5f
			};
			targetEntity.ReceiveDamage(damage, 3f * GlobalConstants.CreatureDamageModifier);
			if (alive && !targetEntity.Alive) {
				cancelAttack = true;
			}
			if (entity.Api.World.Config.GetBool("Zombies_EnableInfected")) {
				InfectTarget(targetEntity, damage);
			}
			return true;
		}

		private void InfectTarget(Entity target, DamageSource damage) {
			if (!target.HasBehavior<EntityBehaviorClockwins>()) {
				target.AddBehavior(new EntityBehaviorClockwins(target));
				return;
			}
			target.GetBehavior<EntityBehaviorClockwins>()?.Worsen(DamagesUtil.HandleDamaged(entity.Api, target as EntityAgent, 0.2f, damage));
		}
	}
}