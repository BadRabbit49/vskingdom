using System;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config;

namespace VSKingdom {
	public class AiTaskSentryAttack : AiTaskBaseTargetable {
		public AiTaskSentryAttack(EntityAgent entity) : base(entity) { }
		
		protected bool animsStarted;
		protected bool cancelAttack;
		protected bool turnToTarget;
		protected bool damageDealed;
		protected long durationOfMs = 1500L;
		protected long lastSearchMs;
		protected float maxDist;
		protected float minDist;
		protected float moveSpeed = 0.035f;
		protected float curTurn;

		protected AnimationMetaData swordSwingMeta;
		protected AnimationMetaData swordSmashMeta;
		protected AnimationMetaData swordSlashMeta;
		protected AnimationMetaData swordStabsMeta;
		protected AnimationMetaData spearStabsMeta;

		private ITreeAttribute loyalties;
		private bool pursuing { get => loyalties.GetBool("command_pursue"); }
		private string entKingdom { get => loyalties?.GetString("kingdom_guid"); }
		
		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			maxDist = taskConfig["maxDist"].AsFloat(20f);
			minDist = taskConfig["minDist"].AsFloat(0.5f);
			moveSpeed = taskConfig["movespeed"].AsFloat(0.035f);
			animMeta = new AnimationMetaData() {
				Animation = "hit",
				Code = "hit",
			}.Init();
			swordSwingMeta = new AnimationMetaData() {
				Animation = "swordswing",
				Code = "swordswing",
			}.Init();
			swordSmashMeta = new AnimationMetaData() {
				Animation = "swordsmash",
				Code = "swordsmash",
			}.Init();
			swordSlashMeta = new AnimationMetaData() {
				Animation = "swordslash",
				Code = "swordslash",
			}.Init();
			swordStabsMeta = new AnimationMetaData() {
				Animation = "swordstabs",
				Code = "swordstabs",
			}.Init();
			spearStabsMeta = new AnimationMetaData() {
				Animation = "spearstabs",
				Code = "spearstabs",
			}.Init();
			turnToTarget = true;
		}

		public override void AfterInitialize() {
			base.AfterInitialize();
			// We are using loyalties attribute tree to get our kingdomGUID.
			loyalties = entity.WatchedAttributes?.GetTreeAttribute("loyalties");
		}

		public override bool ShouldExecute() {
			long elapsedMilliseconds = entity.World.ElapsedMilliseconds;
			if (elapsedMilliseconds - lastSearchMs < durationOfMs || cooldownUntilMs > elapsedMilliseconds) {
				return false;
			}
			if (entity.World.ElapsedMilliseconds - attackedByEntityMs > 30000) {
				attackedByEntity = null;
			}
			if (retaliateAttacks && attackedByEntity != null && attackedByEntity.Alive && attackedByEntity.IsInteractable && IsTargetableEntity(attackedByEntity, maxDist, ignoreEntityCode: true) && hasDirectContact(attackedByEntity, maxDist, maxDist / 2f)) {
				targetEntity = attackedByEntity;
			} else {
				Vec3d position = entity.ServerPos.XYZ.Add(0.0, entity.SelectionBox.Y2 / 2f, 0.0).Ahead(entity.SelectionBox.XSize / 2f, 0f, entity.ServerPos.Yaw);
				targetEntity = entity.World.GetNearestEntity(position, maxDist, maxDist / 2f, (Entity ent) => IsTargetableEntity(ent, maxDist) && hasDirectContact(ent, maxDist, maxDist / 2f));
			}
			lastSearchMs = entity.World.ElapsedMilliseconds;
			damageDealed = false;
			return targetEntity != null;
		}

		public override bool IsTargetableEntity(Entity ent, float range, bool ignoreEntityCode = false) {
			if (ent == entity || !ent.Alive || ent is null) {
				return false;
			}
			if (ent is EntityProjectile projectile && projectile.FiredBy is not null) {
				targetEntity = projectile.FiredBy;
			}
			if (ent is EntityHumanoid) {
				return DataUtility.IsAnEnemy(entKingdom, ent);
			}
			if (ignoreEntityCode) {
				return CanSense(ent, range);
			}
			if (IsTargetEntity(ent.Code.Path)) {
				return CanSense(ent, range);
			}
			return false;
		}
		
		public override void StartExecute() {
			// Initialize a random attack animation and sounds!
			if (entity.RightHandItemSlot != null && !entity.RightHandItemSlot.Empty) {
				Random rnd = new Random();
				if (entity.RightHandItemSlot.Itemstack.Item.Code.PathStartsWith("spear-")) {
					animMeta = spearStabsMeta;
				} else {
					switch (rnd.Next(1, 6)) {
						case 1: animMeta = swordSmashMeta; break;
						case 2: animMeta = swordSlashMeta; break;
						case 3: animMeta = swordStabsMeta; break;
						default: animMeta = swordSwingMeta; break;
					}
				}
				switch (rnd.Next(1, 2)) {
					case 1: entity.World.PlaySoundAt(new AssetLocation("game:sounds/player/strike1"), entity, null, false); break;
					case 2: entity.World.PlaySoundAt(new AssetLocation("game:sounds/player/strike2"), entity, null, false); break;
				}
			}
			animsStarted = false;
			cancelAttack = false;
			curTurn = entity.GetBehavior<EntityBehaviorTaskAI>().PathTraverser.curTurnRadPerSec;
			entity.CurrentControls = EnumEntityActivity.SprintMode;
			if (!turnToTarget) {
				base.StartExecute();
			}
		}

		public override bool ContinueExecute(float dt) {
			// Don't pursue if there is no target, the target is dead, or the attack has been called off!
			if (cancelAttack || targetEntity is null || !targetEntity.Alive) {
				cancelAttack = true;
				return false;
			}
			EntityPos serverPos1 = entity.ServerPos;
			EntityPos serverPos2 = targetEntity.ServerPos;
			bool flag = true;
			if (turnToTarget) {
				float num = GameMath.AngleRadDistance(entity.ServerPos.Yaw, (float)Math.Atan2(serverPos2.X - serverPos1.X, serverPos2.Z - serverPos1.Z));
				entity.ServerPos.Yaw += GameMath.Clamp(num, (0f - curTurn) * dt * GlobalConstants.OverallSpeedMultiplier, curTurn * dt * GlobalConstants.OverallSpeedMultiplier);
				entity.ServerPos.Yaw = entity.ServerPos.Yaw % (MathF.PI * 2f);
				flag = Math.Abs(num) < 20f * (MathF.PI / 180f);
				if (flag && !animsStarted) {
					animsStarted = true;
					base.StartExecute();
				}
			}
			// Get closer if target is too far, but if they're super far then give up!
			if (serverPos1.SquareDistanceTo(serverPos2) >= minDist) {
				// Do not pursue if not being told to pursue endlessly and outside range.
				if (pursuing == false && serverPos1.SquareDistanceTo(serverPos2) >= maxDist * maxDist) {
					targetEntity = null;
					cancelAttack = true;
					animsStarted = false;
					entity.StopAnimation(animMeta.Code);
					return false;
				}
				Vec3d targetPos = new Vec3d(serverPos2);
				pathTraverser.WalkTowards(targetPos, moveSpeed, minDist, OnGoalReached, OnStuck);
				pathTraverser.CurrentTarget.X = targetPos.X;
				pathTraverser.CurrentTarget.Y = targetPos.Y;
				pathTraverser.CurrentTarget.Z = targetPos.Z;
				pathTraverser.Retarget();
			} else {
				entity.StopAnimation(animMeta.Code);
				pathTraverser.Stop();
			}
			if (lastSearchMs + 500L > entity.World.ElapsedMilliseconds) {
				return true;
			}
			if (!damageDealed && flag) {
				AttackTarget();
				damageDealed = true;
			}
			if (lastSearchMs + durationOfMs > entity.World.ElapsedMilliseconds) {
				return true;
			}
			return false;
		}

		public override void OnEntityHurt(DamageSource source, float damage) {
			if (damage > 1 && source.CauseEntity is not EntityHumanoid) {
				targetEntity = source.GetCauseEntity();
				return;
			}
			if (!DataUtility.IsAFriend(entKingdom, source.CauseEntity)) {
				targetEntity = source.GetCauseEntity();
				return;
			}
			base.OnEntityHurt(source, damage);
		}

		protected virtual void AttackTarget() {
			if (!hasDirectContact(targetEntity, minDist, 1f)) {
				return;
			}
			if (animMeta != null) {
				entity.AnimManager.StartAnimation(animMeta);
			}
			float damage = 1f;
			bool alive = targetEntity.Alive;
			if (!entity.RightHandItemSlot.Empty) {
				damage = entity.RightHandItemSlot.Itemstack.Item.AttackPower;
			}
			targetEntity.ReceiveDamage(new DamageSource {
				Source = EnumDamageSource.Entity,
				SourceEntity = entity,
				Type = EnumDamageType.BluntAttack,
				DamageTier = 3,
				KnockbackStrength = 1f
			}, damage * GlobalConstants.CreatureDamageModifier);
			if (alive && !targetEntity.Alive) {
				if (!(targetEntity is EntityPlayer)) {
					entity.WatchedAttributes.SetDouble("lastMealEatenTotalHours", entity.World.Calendar.TotalHours);
				}
				bhEmo?.TryTriggerState("saturated", targetEntity.EntityId);
			}
		}

		public void OnAllyAttacked(Entity targetEnt) {
			if (targetEntity is null || !targetEntity.Alive) {
				targetEntity = targetEnt;
			}
			ShouldExecute();
		}
		
		public void OnEnemySpotted(Entity targetEnt) {
			if (targetEntity is null || !targetEntity.Alive) {
				targetEntity = targetEnt;
			}
			ShouldExecute();
		}

		private void OnStuck() {
			updateTargetPosFleeMode(entity.Pos.XYZ);
		}

		private void OnGoalReached() {
			pathTraverser.Retarget();
			entity.CurrentControls = EnumEntityActivity.Idle;
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