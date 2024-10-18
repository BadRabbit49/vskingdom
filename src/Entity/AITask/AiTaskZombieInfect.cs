using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using Vintagestory.API.Client;
using VSKingdom.Utilities;

namespace VSKingdom {
	public class AiTaskZombieInfect : AiTaskBaseTargetable {
		public AiTaskZombieInfect(EntityZombie entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntityZombie entity;
		#pragma warning restore CS0108
		protected bool cancelAttack = false;
		protected bool animsRunning = false;
		protected bool killedTarget = false;
		protected bool hittingDoors = false;
		protected Int32 stuckCounter = 0;
		protected Int64 durationOfMs = 1500;
		protected Int32 targetMemory = 50000;
		protected Int64 cooldownAtMs = 0;
		protected Int64 lastEngageMs = 0;
		protected Int64 lastHelpedMs = 0;
		protected Int64 nextAttackMs = 0;
		protected Int64 doorBashedMs = 0;
		protected float curTurnAngle = 0;
		protected string lastAnimCode;
		protected string currAnimCode;
		protected string[] animations;
		protected BlockPos barricaded;
		protected AiTaskManager tasksManager;

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
			if (cooldownUntilMs > world.ElapsedMilliseconds || world.ElapsedMilliseconds - cooldownAtMs < durationOfMs) {
				return false;
			}
			if (world.ElapsedMilliseconds - attackedByEntityMs > 30000) {
				attackedByEntity = null;
			}
			if (retaliateAttacks && attackedByEntity != null && attackedByEntity.Alive && attackedByEntity.IsInteractable && IsTargetableEntity(attackedByEntity, 32f, ignoreEntityCode: true) && hasDirectContact(attackedByEntity, 32f, 32f / 2f)) {
				targetEntity = attackedByEntity;
			} else {
				Vec3d position = entity.ServerPos.XYZ.Add(0.0, entity.SelectionBox.Y2 / 2f, 0.0).Ahead(entity.SelectionBox.XSize / 2f, 0f, entity.ServerPos.Yaw);
				if (rand.Next(0, 1) == 0) {
					targetEntity = world.GetNearestEntity(position, 0.5f, 32f / 2f, (Entity ent) => IsTargetableEntity(ent, 32f) && hasDirectContact(ent, 32f, 32f / 2f));
				} else {
					var targetList = world.GetEntitiesAround(position, 0.5f, 32f / 2f, (Entity ent) => IsTargetableEntity(ent, 32f) && hasDirectContact(ent, 32f, 32f / 2f));
					targetEntity = targetList[rand.Next(0, targetList.Length - 1)];
				}
			}
			cooldownAtMs = world.ElapsedMilliseconds;
			return targetEntity?.Alive ?? false;
		}

		public override void StartExecute() {
			// Record last animMeta so we can stop it if we need to.
			lastAnimCode = currAnimCode;
			// Initialize a random attack animation and sounds!
			currAnimCode = animations[world.Rand.Next(1, animations.Length - 1)];
			switch (world.Rand.Next(1, 2)) {
				case 1: sound = new AssetLocation("game:sounds/player/strike1"); break;
				case 2: sound = new AssetLocation("game:sounds/player/strike2"); break;
			}
			cancelAttack = false;
			animsRunning = false;
			killedTarget = false;
			hittingDoors = IsBarricaded(barricaded ?? null);
			entity.Controls.Sprint = !hittingDoors;
			doorBashedMs = 0;
			curTurnAngle = pathTraverser.curTurnRadPerSec;
		}
		
		public override bool ContinueExecute(float dt) {
			try {
				if (cancelAttack) {
					return false;
				}
				if (hittingDoors) {
					pathTraverser.WalkTowards(barricaded.ToVec3d(), 0.02f, 0, null, null);
					hittingDoors = HitBarricade();
					lastEngageMs = world.ElapsedMilliseconds + targetMemory;
					if (TracingUtil.CanSeeEntity(entity, targetEntity)) {
						hittingDoors = false;
						lastEngageMs = world.ElapsedMilliseconds + targetMemory;
						FollowTarget();
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
				if (TracingUtil.CanSeeEntity(entity, targetEntity)) {
					lastEngageMs = world.ElapsedMilliseconds + targetMemory;
					FollowTarget();
				}
				return !killedTarget && world.ElapsedMilliseconds < lastEngageMs;
			} catch {
				return false;
			}
		}

		public override void FinishExecute(bool cancelled) {
			cooldownUntilMs = world.ElapsedMilliseconds + mincooldown + world.Rand.Next(maxcooldown - mincooldown);
			entity.Controls.Sprint = false;
			if ((!targetEntity?.Alive ?? true) || !IsTargetableEntity(targetEntity, (float)targetEntity.ServerPos.DistanceTo(entity.ServerPos))) {
				pathTraverser.Stop();
			}
			if (currAnimCode != null) {
				entity.AnimManager.StopAnimation(currAnimCode);
			}
		}

		public override bool Notify(string key, object data) {
			if (key == "seekEntity" && data != null) {
				targetEntity = (Entity)data;
				return true;
			}
			return false;
		}

		public override void OnEntityHurt(DamageSource source, float damage) {
			if (damage < 1 || source?.GetCauseEntity() == null) {
				return;
			}
			if (source.GetCauseEntity() != null && lastHelpedMs + 5000 < world.ElapsedMilliseconds) {
				targetEntity = source.GetCauseEntity();
				lastHelpedMs = world.ElapsedMilliseconds;
				// Alert all surrounding units! We're under attack!
				foreach (EntityZombie zombie in world.GetEntitiesAround(entity.ServerPos.XYZ, 20f, 4f, entity => (entity is EntityZombie))) {
					var taskManager = zombie.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
					taskManager.GetTask<AiTaskZombieInfect>()?.OnAllyAttacked(source.SourceEntity);
				}
			}
			base.OnEntityHurt(source, damage);
		}

		public void OnAllyAttacked(Entity targetEnt) {
			// Prioritize attacks of other people. Assess threat level in future.
			if (!targetEntity?.Alive ?? true) {
				targetEntity = targetEnt;
			}
		}

		private bool HitBarricade() {
			if (world.ElapsedMilliseconds > doorBashedMs) {
				doorBashedMs = world.ElapsedMilliseconds + 2500;
				entity.AnimManager.StopAnimation("hit");
				entity.AnimManager.StartAnimation(new AnimationMetaData() {
					Animation = "hit",
					Code = "hit",
					BlendMode = EnumAnimationBlendMode.AddAverage,
					ElementWeight = new Dictionary<string, float> {
						{ "UpperTorso", 5f },
						{ "UpperArmR", 10f },
						{ "LowerArmR", 10f },
						{ "UpperArmL", 10f },
						{ "LowerArmL", 10f }
					}
				}.Init());
				world.PlaySoundAt(sound, entity, null, true, soundRange);
				world.BlockAccessor.DamageBlock(barricaded, barricaded.FacingFrom(entity.ServerPos.AsBlockPos), 6f);
			}
			return world.BlockAccessor.GetBlock(barricaded).BlockId != 0;
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
			world.PlaySoundAt(sound, entity, null, true, soundRange);
			bool alive = targetEntity.Alive;
			targetEntity.ReceiveDamage(new DamageSource {
				Source = EnumDamageSource.Entity,
				SourceEntity = entity,
				Type = EnumDamageType.SlashingAttack,
				DamageTier = 3,
				KnockbackStrength = -1.5f
			}, 3f * GlobalConstants.CreatureDamageModifier);
			if (alive && !targetEntity.Alive) {
				InfectTarget();
			}
			return true;
		}

		private bool IsBarricaded(BlockPos pos) {
			if (pos == null || pos.HorizontalManhattenDistance(entity.ServerPos.AsBlockPos) > 3) {
				return false;
			}
			return BlockBehaviorDoor.getDoorAt(world, pos) != null;
		}

		protected void OnGoals() {
			entity.Controls.Sprint = pathTraverser.Active;
			entity.Controls.Forward = pathTraverser.Active;
			pathTraverser.Retarget();
		}

		protected void OnStuck() {
			stuckCounter++;
			// If a door is present, try breaking into it (if not iron or reinforced!)
			if (IsBarricaded(entity.ServerPos.HorizontalAheadCopy(1).AsBlockPos)) {
				barricaded = entity.ServerPos.HorizontalAheadCopy(1).AsBlockPos;
			}
			if (stuckCounter > 5) {
				cancelAttack = true;
			}
			entity.Controls.Sprint = pathTraverser.Active;
			entity.Controls.Forward = pathTraverser.Active;
		}
		
		protected void NoPaths() {
			cancelAttack = true;
			pathTraverser.Stop();
			entity.Controls.Sprint = pathTraverser.Active;
			entity.Controls.Forward = pathTraverser.Active;
		}

		protected void FollowTarget() {
			stuckCounter = 0;
			entity.Controls.Sprint = true;
			int searchDepth = world.Rand.Next(3500, 10000);
			pathTraverser.NavigateTo_Async(targetEntity.ServerPos.XYZ.OffsetCopy(rand.Next(-1, 1), 0, rand.Next(-1, 1)), 1, entity.SelectionBox.XSize, OnGoals, OnStuck, NoPaths, searchDepth, 0);
		}
		
		protected void InfectTarget() {
			if (targetEntity is EntitySentry sentry) {
				Entity zombieEnt = entity.Api.World.ClassRegistry.CreateEntity("vskingdom:zombie-masc");
				EntityZombie zombie = zombieEnt as EntityZombie;
				zombie.zombified = true;
				zombie.canRevive = true;
				zombie.ServerPos.SetFrom(sentry.ServerPos);
				zombie.Pos.SetFrom(sentry.ServerPos);
				//zombie.TryGiveItemStack
				entity.Api.World.SpawnEntity(zombieEnt);
				for (int i = 0; i < sentry.gearInv.Count; i++) {
					if (!sentry.gearInv[i].Empty) {
						sentry.gearInv[i].TryPutInto(entity.Api.World, zombie.gearInv[i], sentry.gearInv[i].StackSize);
					}
				}
				targetEntity.Die(EnumDespawnReason.Removed);
			}
		}
	}
}