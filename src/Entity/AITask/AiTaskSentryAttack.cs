using System;
using System.Linq;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class AiTaskSentryAttack : AiTaskBaseTargetable {
		public AiTaskSentryAttack(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		protected bool cancelAttack = false;
		protected bool animsRunning = false;
		protected bool turnToTarget = true;
		protected bool banditPilled = false;
		protected bool attackFinish = false;
		protected bool usingAShield = false;
		protected long shieldedHand = 0;
		protected long durationOfMs = 1500L;
		protected long lastAttackMs;
		protected long lastHelpedMs;
		protected float maximumRange = 20f;
		protected float minimumRange = 0.5f;
		protected float curTurnAngle;
		protected string lastAnimCode;
		protected string currAnimCode;
		protected string[] shieldAnim;
		protected string[] animations;
		protected HashSet<long> allyCaches = new HashSet<long>();
		protected AiTaskManager tasksManager;
		protected AiTaskSentrySearch searchTask => tasksManager.GetTask<AiTaskSentrySearch>();

		public override void AfterInitialize() {
			world = entity.World;
			tasksManager = entity.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
		}

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			this.banditPilled = taskConfig["isBandit"].AsBool(false);
			this.animations = taskConfig["animations"]?.AsArray<string>(new string[] { "hit", "spearstabs" });
			this.shieldAnim = taskConfig["shieldAnim"]?.AsArray<string>(new string[] { "raiseshield-left", "raiseshield-right" });
			this.mincooldown = taskConfig["mincooldown"].AsInt(500);
			this.maxcooldown = taskConfig["maxcooldown"].AsInt(1500);
			this.maximumRange = taskConfig["maximumRange"].AsFloat(20f);
			this.minimumRange = taskConfig["minimumRange"].AsFloat(0.5f);
		}

		public override bool ShouldExecute() {
			if (!entity.cachedData.usesMelee || !entity.cachedData.weapReady || !entity.ruleOrder[2]) {
				return false;
			}
			if (cooldownUntilMs > entity.World.ElapsedMilliseconds || entity.World.ElapsedMilliseconds - lastAttackMs < durationOfMs) {
				return false;
			}
			if (entity.World.ElapsedMilliseconds - attackedByEntityMs > 30000) {
				attackedByEntity = null;
			}
			if (retaliateAttacks && attackedByEntity != null && attackedByEntity.Alive && attackedByEntity.IsInteractable && IsTargetableEntity(attackedByEntity, maximumRange, ignoreEntityCode: true) && hasDirectContact(attackedByEntity, maximumRange, maximumRange / 2f)) {
				targetEntity = attackedByEntity;
			} else {
				Vec3d position = entity.ServerPos.XYZ.Add(0.0, entity.SelectionBox.Y2 / 2f, 0.0).Ahead(entity.SelectionBox.XSize / 2f, 0f, entity.ServerPos.Yaw);
				maximumRange = entity.WatchedAttributes.GetFloat("engageRange", 16f);
				minimumRange = entity.cachedData?.weapRange ?? 0.5f;
				if (rand.Next(0, 1) == 0) {
					targetEntity = entity.World.GetNearestEntity(position, maximumRange, maximumRange / 2f, (Entity ent) => IsTargetableEntity(ent, maximumRange) && hasDirectContact(ent, maximumRange, maximumRange / 2f));
				} else {
					var targetList = entity.World.GetEntitiesAround(position, maximumRange, maximumRange / 2f, (Entity ent) => IsTargetableEntity(ent, maximumRange) && hasDirectContact(ent, maximumRange, maximumRange / 2f));
					targetEntity = targetList[rand.Next(0, targetList.Length - 1)];
				}
			}
			lastAttackMs = entity.World.ElapsedMilliseconds;
			return targetEntity?.Alive ?? false;
		}

		public override bool IsTargetableEntity(Entity ent, float range, bool ignoreEntityCode = false) {
			if (ent == null || ent == entity || !ent.Alive) {
				return false;
			}
			if (allyCaches.Contains(ent.EntityId)) {
				return false;
			}
			if (ent is EntityProjectile projectile && projectile.FiredBy != null) {
				targetEntity = projectile.FiredBy;
			}
			if (ent.WatchedAttributes.HasAttribute(king_GUID)) {
				if (banditPilled) {
					return ent is EntityPlayer || (ent is EntitySentry sentry && sentry.cachedData.kingdomGUID != banditryGUID);
				}
				if (ent.WatchedAttributes.HasAttribute(lead_GUID) && entity.cachedData.leadersGUID == ent.WatchedAttributes.GetString(lead_GUID)) {
					allyCaches.Add(ent.EntityId);
					return false;
				}
				if (ent is EntitySentry sent) {
					return entity.cachedData.enemiesLIST.Contains(sent.cachedData.kingdomGUID) || entity.cachedData.outlawsLIST.Contains(sent.cachedData.leadersGUID) || sent.cachedData.kingdomGUID == banditryGUID;
				}
				return entity.cachedData.enemiesLIST.Contains(ent.WatchedAttributes.GetString(king_GUID));
			}
			if (ent.WatchedAttributes.HasAttribute("domesticationstatus")) {
				if (!ent.WatchedAttributes.GetTreeAttribute("domesticationstatus")?.HasAttribute("owner") ?? false) {
					return true;
				}
				string ownerGUID = ent.WatchedAttributes.GetTreeAttribute("domesticationstatus")?.GetString("owner");
				if (ownerGUID != null && !ent.WatchedAttributes.HasAttribute(lead_GUID)) {
					ent.WatchedAttributes.SetString(lead_GUID, ownerGUID);
				}
				if (entity.WatchedAttributes.GetString(lead_GUID) == ownerGUID) {
					allyCaches.Add(ent.EntityId);
					return false;
				}
				if (entity.cachedData.outlawsLIST.Contains(ownerGUID)) {
					return true;
				}
				return false;
			}
			if (ignoreEntityCode || IsTargetEntity(ent.Code.Path)) {
				return CanSense(ent, range);
			}
			return false;
		}

		public override void StartExecute() {
			// Record last animMeta so we can stop it if we need to.
			lastAnimCode = currAnimCode;
			// Initialize a random attack animation and sounds!
			if (entity.RightHandItemSlot.Empty) {
				currAnimCode = animations[0];
			} else {
				if (entity.RightHandItemSlot.Itemstack.Item is ItemSpear) {
					currAnimCode = animations[1];
				} else {
					currAnimCode = animations[entity.World.Rand.Next(1, animations.Length - 1)];
				}
				switch (entity.World.Rand.Next(1, 2)) {
					case 1: sound = new AssetLocation("game:sounds/player/strike1"); break;
					case 2: sound = new AssetLocation("game:sounds/player/strike2"); break;
				}
			}
			if (entity.LeftHandItemSlot.Empty && entity.RightHandItemSlot.Empty) {
				usingAShield = false;
			} else if (!entity.LeftHandItemSlot.Empty && entity.LeftHandItemSlot.Itemstack?.Item is ItemShield) {
				usingAShield = true;
				shieldedHand = 0;
			} else if (!entity.RightHandItemSlot.Empty && entity.LeftHandItemSlot.Itemstack?.Item is ItemShield) {
				usingAShield = true;
				shieldedHand = 1;
			}
			cancelAttack = false;
			animsRunning = false;
			attackFinish = true;
			curTurnAngle = pathTraverser.curTurnRadPerSec;
			searchTask?.SetTargetEnts(targetEntity);
		}
		
		public override bool ContinueExecute(float dt) {
			if (cancelAttack || (!targetEntity?.Alive ?? true) || !entity.cachedData.usesMelee) {
				return false;
			}
			EntityPos ownPos = entity.ServerPos;
			EntityPos hisPos = targetEntity.ServerPos;
			bool flag = true;
			if (turnToTarget) {
				float num = GameMath.AngleRadDistance(entity.ServerPos.Yaw, (float)Math.Atan2(hisPos.X - ownPos.X, hisPos.Z - ownPos.Z));
				entity.ServerPos.Yaw += GameMath.Clamp(num, (0f - curTurnAngle) * dt * GlobalConstants.OverallSpeedMultiplier, curTurnAngle * dt * GlobalConstants.OverallSpeedMultiplier);
				entity.ServerPos.Yaw %= (MathF.PI * 2f);
				flag = Math.Abs(num) < maximumRange * (MathF.PI / 180f);
			}
			if (!attackFinish) {
				attackFinish = currAnimCode != null && !entity.AnimManager.IsAnimationActive(currAnimCode);
			}
			animsRunning = lastAnimCode != null ? entity.AnimManager.GetAnimationState(lastAnimCode).Running : false;
			if (!animsRunning && attackFinish && flag) {
				attackFinish = false;
				animsRunning = AttackTarget();
			}
			if (usingAShield) {
				bool closeToTarget = entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos) < 16f;
				bool targetsRanged = false;
				if (targetEntity != null && targetEntity is EntityHumanoid humanoidEnt && !humanoidEnt.RightHandItemSlot.Empty) {
					Item targetWeapon = humanoidEnt.RightHandItemSlot.Itemstack.Item;
					targetsRanged = (targetWeapon is ItemBow || targetWeapon is ItemSling);
				}
				bool shouldShield = (closeToTarget && !targetsRanged) || (!closeToTarget && targetsRanged);
				bool animationsOn = entity.AnimManager.IsAnimationActive(shieldAnim[shieldedHand]);
				if (shouldShield && !animationsOn) {
					string ArmToUse = shieldedHand.Equals(0) ? "ArmL" : "ArmR";
					entity.AnimManager.StartAnimation(new AnimationMetaData() {
							Animation = shieldAnim[shieldedHand],
							Code = shieldAnim[shieldedHand],
							BlendMode = EnumAnimationBlendMode.Add,
							EaseInSpeed = 1f,
							ElementWeight = new Dictionary<string, float> {
								{ "Upper" + ArmToUse, 100f },
								{ "Lower" + ArmToUse, 100f }
							},
							ElementBlendMode = new Dictionary<string, EnumAnimationBlendMode> {
								{ "Upper" + ArmToUse, EnumAnimationBlendMode.AddAverage },
								{ "Lower" + ArmToUse, EnumAnimationBlendMode.AddAverage }
							}
						}.Init());
				} else if (!shouldShield && animationsOn) {
					entity.AnimManager.StopAnimation(shieldAnim[0]);
					entity.AnimManager.StopAnimation(shieldAnim[1]);
				}
			}
			return lastAttackMs + durationOfMs > entity.World.ElapsedMilliseconds;
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
			entity.AnimManager.StopAnimation(shieldAnim[0]);
			entity.AnimManager.StopAnimation(shieldAnim[1]);
		}

		public override void OnEntityHurt(DamageSource source, float damage) {
			if (damage < 1 || !IsTargetableEntity(source.GetCauseEntity(), (float)source.GetSourcePosition().DistanceTo(entity.ServerPos.XYZ))) {
				return;
			}
			if (source.GetCauseEntity() != null && source.Type != EnumDamageType.Heal && lastHelpedMs + 5000 < entity.World.ElapsedMilliseconds) {
				targetEntity = source?.GetCauseEntity();
				lastHelpedMs = entity.World.ElapsedMilliseconds;
				// Alert all surrounding units! We're under attack!
				foreach (EntitySentry sentry in entity.World.GetEntitiesAround(entity.ServerPos.XYZ, 20f, 4f, entity => (entity is EntitySentry))) {
					if (entity.cachedData.kingdomGUID == sentry.cachedData.kingdomGUID) {
						var taskManager = sentry.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
						taskManager.GetTask<AiTaskSentryAttack>()?.OnAllyAttacked(source.SourceEntity);
						taskManager.GetTask<AiTaskSentryRanged>()?.OnAllyAttacked(source.SourceEntity);
					}
				}
			}
			base.OnEntityHurt(source, damage);
		}

		public void OnAllyAttacked(Entity targetEnt) {
			// Prioritize attacks of other people. Assess threat level in future.
			if ((!targetEntity?.Alive ?? true) || (targetEnt is EntityHumanoid && targetEntity is not EntityHumanoid)) {
				targetEntity = targetEnt;
				searchTask.SetTargetEnts(targetEnt);
			}
			ShouldExecute();
		}

		private bool AttackTarget() {
			if (!hasDirectContact(targetEntity, entity.cachedData.weapRange, entity.cachedData.weapRange)) {
				return false;
			}
			entity.AnimManager.StopAnimation(lastAnimCode);
			entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = currAnimCode, Code = currAnimCode }.Init());
			entity.World.PlaySoundAt(sound, entity, null, true, soundRange);
			float damage = entity.RightHandItemSlot?.Itemstack?.Item?.AttackPower ?? 1f;
			bool alive = targetEntity.Alive;
			targetEntity.ReceiveDamage(new DamageSource {
				Source = EnumDamageSource.Entity,
				SourceEntity = entity,
				Type = EnumDamageType.BluntAttack,
				DamageTier = 3,
				KnockbackStrength = 1f
			}, damage * GlobalConstants.CreatureDamageModifier);
			// Only jump back if they killing blow was not dealt.
			if (alive && !targetEntity.Alive) {
				searchTask.ResetsTargets();
				searchTask.StopMovements();
				cancelAttack = true;
				return false;
			}
			return true;
		}

		private bool IsTargetEntity(string testPath) {
			if (targetEntityFirstLetters.Length == 0) {
				return true;
			}
			if (targetEntityFirstLetters.IndexOf(testPath[0]) < 0) {
				return false;
			}
			for (int i = 0; i < targetEntityCodesExact.Length; i++) {
				if (testPath == targetEntityCodesExact[i]) {
					return true;
				}
			}
			for (int j = 0; j < targetEntityCodesBeginsWith.Length; j++) {
				if (testPath.StartsWithFast(targetEntityCodesBeginsWith[j])) {
					return true;
				}
			}
			return false;
		}
	}
}