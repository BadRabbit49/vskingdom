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
		
		protected bool animsStarted = false;
		protected bool cancelAttack = false;
		protected bool damageDealed = false;
		protected bool turnToTarget = true;
		protected long durationOfMs = 1500L;
		protected long lastSearchMs;
		protected float maxDist = 20f;
		protected float minDist = 0.5f;
		protected float weapRange = 1f;
		protected float moveSpeed = 0.035f;
		protected float curTurn;

		protected AnimationMetaData basicSwingMeta;
		protected AnimationMetaData swordSwingMeta;
		protected AnimationMetaData swordSmashMeta;
		protected AnimationMetaData swordSlashMeta;
		protected AnimationMetaData swordStabsMeta;
		protected AnimationMetaData spearStabsMeta;
		
		private ITreeAttribute loyalties;
		private bool holdfire { get => !loyalties.GetBool("command_attack"); }
		private bool nopursue { get => !loyalties.GetBool("command_pursue"); }
		private string entKingdom { get => loyalties?.GetString("kingdom_guid"); }
		
		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			maxDist = taskConfig["maxDist"].AsFloat(20f);
			minDist = taskConfig["minDist"].AsFloat(0.5f);
			moveSpeed = taskConfig["movespeed"].AsFloat(0.035f);
			basicSwingMeta = new AnimationMetaData() {
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
		}

		public override void AfterInitialize() {
			base.AfterInitialize();
			// We are using loyalties attribute tree to get our kingdomGUID.
			loyalties = entity.WatchedAttributes?.GetTreeAttribute("loyalties");
		}

		public override bool ShouldExecute() {
			if (holdfire) {
				return false;
			}
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
			if (ignoreEntityCode || IsTargetEntity(ent.Code.Path)) {
				return CanSense(ent, range);
			}
			return false;
		}
		
		public override void StartExecute() {
			// Initialize a random attack animation and sounds!
			if (entity.RightHandItemSlot.Empty) {
				animMeta = basicSwingMeta;
			} else {
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
					case 1: sound = new AssetLocation("game:sounds/player/strike1"); break;
					case 2: sound = new AssetLocation("game:sounds/player/strike2"); break;
				}
			}
			weapRange = entity.RightHandItemSlot?.Itemstack?.Item?.AttackRange ?? 1.5f;
			animsStarted = false;
			cancelAttack = false;
			damageDealed = false;
			curTurn = pathTraverser.curTurnRadPerSec;
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
			}
			// Get closer if target is too far, but if they're super far then give up!
			if (serverPos1.SquareDistanceTo(serverPos2) >= weapRange) {
				// Do not pursue if not being told to pursue endlessly and outside range.
				if (nopursue && serverPos1.SquareDistanceTo(serverPos2) >= maxDist * maxDist) {
					targetEntity = null;
					cancelAttack = true;
					animsStarted = false;
					entity.Controls.StopAllMovement();
					entity.AnimManager.StopAnimation(animMeta.Code);
					return false;
				}
				Vec3d targetPos = new Vec3d(serverPos2);
				pathTraverser.WalkTowards(targetPos, moveSpeed, weapRange, OnGoalReached, OnStuck);
				pathTraverser.CurrentTarget.X = targetPos.X;
				pathTraverser.CurrentTarget.Y = targetPos.Y;
				pathTraverser.CurrentTarget.Z = targetPos.Z;
				pathTraverser.Retarget();
			} else {
				entity.Controls.StopAllMovement();
				entity.AnimManager.StopAnimation(animMeta.Code);
				pathTraverser.Stop();
			}
			if (!animsStarted && !damageDealed && flag) {
				AttackTarget();
			}
			if (lastSearchMs + durationOfMs > entity.World.ElapsedMilliseconds) {
				return true;
			}
			return false;
		}

		public override void OnEntityHurt(DamageSource source, float damage) {
			if ((damage > 1 && source.CauseEntity is not EntityHumanoid) || !DataUtility.IsAFriend(entKingdom, source.CauseEntity)) {
				targetEntity = source.GetCauseEntity();
				return;
			}
			base.OnEntityHurt(source, damage);
		}

		protected virtual void AttackTarget() {
			if (!hasDirectContact(targetEntity, weapRange, weapRange)) {
				return;
			}
			animsStarted = true;
			entity.AnimManager.StartAnimation(animMeta);
			entity.World.PlaySoundAt(sound, entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z, null, randomizePitch: true, soundRange);
			float damage = entity.RightHandItemSlot?.Itemstack?.Item?.AttackPower ?? 1f;
			bool alive = targetEntity.Alive;
			targetEntity.ReceiveDamage(new DamageSource {
				Source = EnumDamageSource.Entity,
				SourceEntity = entity,
				Type = EnumDamageType.BluntAttack,
				DamageTier = 3,
				KnockbackStrength = 1f
			}, damage * GlobalConstants.CreatureDamageModifier);
			damageDealed = true;
			// Only jump back if they killing blow was not dealt.
			if (alive && !targetEntity.Alive) {
				return;
			}
			StepBackwards();
		}

		public void OnAllyAttacked(Entity targetEnt) {
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
			entity.Controls.StopAllMovement();
		}

		private void StepBackwards() {
			// Take a single step back, try to face the enemy though.
			entity.Controls.Forward = false;
			entity.ServerControls.Forward = false;
			entity.Controls.Backward = true;
			entity.ServerControls.Backward = true;
			Vec3d behindPos = entity.ServerPos.BehindCopy(1).XYZ;
			pathTraverser.WalkTowards(behindPos, moveSpeed, weapRange, OnGoalReached, OnStuck);
			pathTraverser.CurrentTarget.X = behindPos.X;
			pathTraverser.CurrentTarget.Y = behindPos.Y;
			pathTraverser.CurrentTarget.Z = behindPos.Z;
			pathTraverser.Retarget();
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