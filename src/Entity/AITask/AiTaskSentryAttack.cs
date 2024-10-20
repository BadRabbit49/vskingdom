using System;
using System.Linq;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using VSKingdom.Utilities;

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
		protected char shieldedHand = 'L';
		protected long durationOfMs = 1500L;
		protected long lastHelpedMs = 0L;
		protected long nextAttackMs = 0L;
		protected float maximumRange = 20f;
		protected float minimumRange = 0.5f;
		protected float curTurnAngle = 0f;
		protected string lastAnimCode;
		protected string currAnimCode;
		protected string shieldAnimsL;
		protected string shieldAnimsR;
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
			this.shieldAnimsL = taskConfig["shieldAnimsL"]?.AsString("raiseshield-left");
			this.shieldAnimsR = taskConfig["shieldAnimsR"]?.AsString("raiseshield-right");
			this.mincooldown = taskConfig["mincooldown"].AsInt(500);
			this.maxcooldown = taskConfig["maxcooldown"].AsInt(1500);
			this.maximumRange = taskConfig["maximumRange"].AsFloat(20f);
			this.minimumRange = taskConfig["minimumRange"].AsFloat(0.5f);
		}

		public override bool ShouldExecute() {
			if (cooldownUntilMs > entity.World.ElapsedMilliseconds) {
				return false;
			}
			cooldownUntilMs = entity.World.ElapsedMilliseconds + durationOfMs;
			if (!entity.cachedData.usesMelee || !entity.cachedData.weapReady || !entity.ruleOrder[2]) {
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
			return targetEntity?.Alive ?? false;
		}

		public override bool IsTargetableEntity(Entity ent, float range, bool ignoreEntityCode = false) {
			if (!ent.Alive) {
				return false;
			}
			if (ent is EntityProjectile projectile && projectile.FiredBy != null) {
				targetEntity = projectile.FiredBy;
			}
			if (ent.WatchedAttributes.HasAttribute(KingdomGUID)) {
				return IsAnEnemy(ent);
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
			usingAShield = false;
			if (!entity.LeftHandItemSlot.Empty && entity.LeftHandItemSlot.Itemstack?.Item is ItemShield) {
				usingAShield = true;
				shieldedHand = 'L';
			} else if (!entity.RightHandItemSlot.Empty && entity.LeftHandItemSlot.Itemstack?.Item is ItemShield) {
				usingAShield = true;
				shieldedHand = 'R';
			}
			cancelAttack = false;
			animsRunning = false;
			attackFinish = true;
			curTurnAngle = pathTraverser.curTurnRadPerSec;
			searchTask.SetTargetEnts(targetEntity);
		}
		
		public override bool ContinueExecute(float dt) {
			try {
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
				animsRunning = lastAnimCode != null && entity.AnimManager.IsAnimationActive(lastAnimCode);
				if (!animsRunning && entity.World.ElapsedMilliseconds > nextAttackMs && flag) {
					nextAttackMs = entity.World.ElapsedMilliseconds + 1500L;
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
					bool animationsOn = entity.AnimManager.IsAnimationActive(shieldedHand == 'L' ? shieldAnimsL : shieldAnimsR);
					if (shouldShield && !animationsOn) {
						string armsToUse = "Arm" + shieldedHand;
						string codeToUse = shieldedHand == 'L' ? shieldAnimsL : shieldAnimsR;
						entity.AnimManager.StartAnimation(new AnimationMetaData() {
								Animation = codeToUse,
								Code = codeToUse,
								BlendMode = EnumAnimationBlendMode.Add,
								EaseInSpeed = 1f,
								EaseOutSpeed = 999f,
								ElementWeight = new Dictionary<string, float> {
									{ "Upper" + armsToUse, 100f },
									{ "Lower" + armsToUse, 100f }
								},
								ElementBlendMode = new Dictionary<string, EnumAnimationBlendMode> {
									{ "Upper" + armsToUse, EnumAnimationBlendMode.AddAverage },
									{ "Lower" + armsToUse, EnumAnimationBlendMode.AddAverage }
								}
							}.Init());
					} else if (!shouldShield && animationsOn) {
						entity.AnimManager.StopAnimation(shieldAnimsL);
						entity.AnimManager.StopAnimation(shieldAnimsR);
					}
				}
				return cooldownUntilMs > entity.World.ElapsedMilliseconds;
			} catch {
				return false;
			}
		}

		public override void FinishExecute(bool cancelled) {
			durationOfMs = mincooldown + entity.World.Rand.Next(maxcooldown - mincooldown);
			cooldownUntilMs = entity.World.ElapsedMilliseconds + durationOfMs;
			if ((!targetEntity?.Alive ?? true) || !IsTargetableEntity(targetEntity, (float)targetEntity.ServerPos.DistanceTo(entity.ServerPos))) {
				searchTask?.ResetsTargets();
				searchTask?.StopMovements();
			}
			if (currAnimCode != null) {
				entity.AnimManager.StopAnimation(currAnimCode);
			}
			entity.AnimManager.StopAnimation(shieldAnimsL);
			entity.AnimManager.StopAnimation(shieldAnimsR);
		}

		public override void OnEntityHurt(DamageSource source, float damage) {
			if (damage < 1 || source?.GetCauseEntity() == null) {
				return;
			}
			if (entity.World.Rand.Next(100) < 20) {
				entity.PlayEntitySound("hurt" + entity.World.Rand.Next(1, 2));
			}
			if (source.GetCauseEntity().WatchedAttributes.HasAttribute(KingdomGUID) && source.GetCauseEntity().WatchedAttributes.GetKingdom() == entity.cachedData.kingdomGUID) {
				if (entity.WatchedAttributes.GetKingdom() != CommonersID) {
					return;
				}
			}
			if (source.GetCauseEntity() != null && source.Type != EnumDamageType.Heal && lastHelpedMs + 5000 < entity.World.ElapsedMilliseconds) {
				targetEntity = source.GetCauseEntity();
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
			entity.AnimManager.StartAnimation(new AnimationMetaData() {
				Animation = currAnimCode,
				Code = currAnimCode,
				BlendMode = EnumAnimationBlendMode.Add,
				ElementWeight = new Dictionary<string, float> {
					{ "ItemAnchor", 100f },
					{ "UpperTorso", 5f },
					{ "UpperArmR", 10f },
					{ "LowerArmR", 10f },
					{ "UpperArmL", 10f },
					{ "LowerArmL", 10f }
				},
				ElementBlendMode = new Dictionary<string, EnumAnimationBlendMode> {
					{ "ItemAnchor", EnumAnimationBlendMode.Add },
					{ "UpperTorso", EnumAnimationBlendMode.AddAverage },
					{ "LowerTorso", EnumAnimationBlendMode.AddAverage }
				}
			}.Init());
			if (entity.World.Rand.Next(100) < 25) {
				entity.PlayEntitySound("stabs");
			}
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
				if (entity.World.Rand.Next(100) < 5) {
					entity.PlayEntitySound("laugh");
				}
				return false;
			}
			return true;
		}

		private bool IsAnEnemy(Entity target) {
			if (banditPilled) {
				return entity.cachedData.kingdomGUID != target.WatchedAttributes.GetString(KingdomGUID);
			}
			if (target is EntitySentry sentry) {
				return entity.cachedData.enemiesLIST.Contains(sentry.cachedData.kingdomGUID) || sentry.cachedData.kingdomGUID == BanditrysID;
			}
			if (target is EntityPlayer player) {
				return entity.cachedData.outlawsLIST.Contains(player.PlayerUID) || entity.cachedData.enemiesLIST.Contains(player.WatchedAttributes.GetString(KingdomGUID));
			}
			return entity.cachedData.enemiesLIST.Contains(target.WatchedAttributes.GetString(KingdomGUID));
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