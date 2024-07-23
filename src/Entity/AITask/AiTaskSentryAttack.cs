using System;
using System.Linq;
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
		protected bool turnToTarget = true;
		protected bool doorIsBehind = false;
		protected bool banditryBehavior = false;
		protected long durationOfMs = 1500L;
		protected long lastSearchMs;
		protected float maxDist = 20f;
		protected float minDist = 0.5f;
		protected float curTurn;
		protected string prevAnims;
		protected AnimationMetaData[] animMetas;
		protected AiTaskSentrySearch searchTask => entity.GetBehavior<EntityBehaviorTaskAI>().TaskManager.GetTask<AiTaskSentrySearch>();

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			// Initialize ALL of the provided melee attacks.
			banditryBehavior = taskConfig["isBandit"].AsBool(false);
			string[] animCodes = taskConfig["animCodes"]?.AsArray<string>(new string[] { "hit", "spearstabs" });
			animMetas = new AnimationMetaData[animCodes.Length];
			for (int i = 0; i < animCodes.Length; i++) {
				animMetas[i] = new AnimationMetaData() {
					Animation = animCodes[i].ToLowerInvariant(),
					Code = animCodes[i].ToLowerInvariant(),
					BlendMode = EnumAnimationBlendMode.Average
				}.Init();
			}
			maxDist = taskConfig["maxDist"].AsFloat(20f);
			minDist = taskConfig["minDist"].AsFloat(0.5f);
		}

		public override bool ShouldExecute() {
			if (!entity.ruleOrder[2]) {
				return false;
			}
			long elapsedMilliseconds = entity.World.ElapsedMilliseconds;
			if (elapsedMilliseconds - lastSearchMs < durationOfMs || cooldownUntilMs > elapsedMilliseconds) {
				return false;
			}
			if (elapsedMilliseconds - attackedByEntityMs > 30000) {
				attackedByEntity = null;
			}
			if (retaliateAttacks && attackedByEntity != null && attackedByEntity.Alive && attackedByEntity.IsInteractable && IsTargetableEntity(attackedByEntity, maxDist, ignoreEntityCode: true) && hasDirectContact(attackedByEntity, maxDist, maxDist / 2f)) {
				targetEntity = attackedByEntity;
			} else {
				Vec3d position = entity.ServerPos.XYZ.Add(0.0, entity.SelectionBox.Y2 / 2f, 0.0).Ahead(entity.SelectionBox.XSize / 2f, 0f, entity.ServerPos.Yaw);
				if (rand.Next(0, 1) == 0) {
					targetEntity = entity.World.GetNearestEntity(position, maxDist, maxDist / 2f, (Entity ent) => IsTargetableEntity(ent, maxDist) && hasDirectContact(ent, maxDist, maxDist / 2f));
				} else {
					var targetList = entity.World.GetEntitiesAround(position, maxDist, maxDist / 2f, (Entity ent) => IsTargetableEntity(ent, maxDist) && hasDirectContact(ent, maxDist, maxDist / 2f));
					targetEntity = targetList[rand.Next(0, targetList.Length - 1)];
				}
			}
			lastSearchMs = entity.World.ElapsedMilliseconds;
			return targetEntity != null;
		}

		public override bool IsTargetableEntity(Entity ent, float range, bool ignoreEntityCode = false) {
			if (ent == null || ent == entity || !ent.Alive) {
				return false;
			}
			if (ent is EntityProjectile projectile && projectile.FiredBy is not null) {
				targetEntity = projectile.FiredBy;
			}
			if (ent.WatchedAttributes.HasAttribute("loyalties")) {
				if (banditryBehavior) {
					return ent is EntityPlayer || (ent is EntitySentry sentry && sentry.kingdomID != "xxxxxxxx");
				}
				if (ent is EntitySentry sent) {
					return entity.enemiesID.Contains(sent.kingdomID) || sent.kingdomID == "xxxxxxxx";
				}
				return entity.enemiesID.Contains(ent.WatchedAttributes.GetTreeAttribute("loyalties").GetString("kingdom_guid"));
			}
			if (ignoreEntityCode || IsTargetEntity(ent.Code.Path)) {
				return CanSense(ent, range);
			}
			return false;
		}
		
		public override void StartExecute() {
			// Record last animMeta so we can stop it if we need to.
			prevAnims = animMeta?.Code ?? "hit";
			// Initialize a random attack animation and sounds!
			if (entity.RightHandItemSlot.Empty) {
				animMeta = animMetas[0];
			} else {
				Random rnd = new Random();
				if (entity.RightHandItemSlot.Itemstack.Item.Code.PathStartsWith("spear-")) {
					animMeta = animMetas[1];
				} else {
					animMeta = animMetas[rnd.Next(0, animMetas.Length - 1)];
				}
				switch (rnd.Next(1, 2)) {
					case 1: sound = new AssetLocation("game:sounds/player/strike1"); break;
					case 2: sound = new AssetLocation("game:sounds/player/strike2"); break;
				}
			}
			cancelAttack = false;
			curTurn = pathTraverser.curTurnRadPerSec;
			searchTask.SetTargetEnts(targetEntity);
		}

		public override bool ContinueExecute(float dt) {
			// Don't pursue if there is no target, the target is dead, or the attack has been called off!
			if (cancelAttack || targetEntity == null || !targetEntity.Alive) {
				return false;
			}
			EntityPos serverPos1 = entity.ServerPos;
			EntityPos serverPos2 = targetEntity?.ServerPos;
			bool flag = true;
			if (turnToTarget) {
				float num = GameMath.AngleRadDistance(entity.ServerPos.Yaw, (float)Math.Atan2(serverPos2.X - serverPos1.X, serverPos2.Z - serverPos1.Z));
				entity.ServerPos.Yaw += GameMath.Clamp(num, (0f - curTurn) * dt * GlobalConstants.OverallSpeedMultiplier, curTurn * dt * GlobalConstants.OverallSpeedMultiplier);
				entity.ServerPos.Yaw = entity.ServerPos.Yaw % (MathF.PI * 2f);
				flag = Math.Abs(num) < maxDist * (MathF.PI / 180f);
			}
			// Get closer if target is too far, but if they're super far then give up!
			if (serverPos1.DistanceTo(serverPos2) > entity.weapRange) {
				// Do not pursue if not being told to pursue endlessly and outside range.
				if (!entity.ruleOrder[3] && entity.Loyalties.GetBlockPos("outpost_xyzd").DistanceTo(serverPos2.AsBlockPos) > entity.postRange) {
					targetEntity = null;
					cancelAttack = true;
					StopNow();
					return false;
				}
				// Try NavigateTo instead of WalkTowards?
				//pathTraverser.WalkTowards(serverPos2?.XYZ.Clone() ?? serverPos1.XYZ.Clone(), (float)entity.moveSpeed, (float)entity.weapRange, OnGoals, OnStuck);
			}
			if (!entity.AnimManager.GetAnimationState(prevAnims).Running && flag) {
				entity.StopAnimation(prevAnims);
				AttackTarget();
			}
			return lastSearchMs + durationOfMs > entity.World.ElapsedMilliseconds;
		}

		public override void OnEntityHurt(DamageSource source, float damage) {
			if (source.GetCauseEntity() is EntitySentry sentry && entity.kingdomID == sentry.kingdomID) {
				return;
			}
			if (damage > 1 && source.GetCauseEntity() is not EntityHumanoid) {
				targetEntity = source.GetCauseEntity();
			}
			base.OnEntityHurt(source, damage);
		}

		protected virtual void AttackTarget() {
			if (!hasDirectContact(targetEntity, (float)entity.weapRange, (float)entity.weapRange)) {
				return;
			}
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
			// Only jump back if they killing blow was not dealt.
			if (alive && !targetEntity.Alive) {
				targetEntity = null;
				cancelAttack = true;
				return;
			}
		}

		public void OnAllyAttacked(Entity targetEnt) {
			// Prioritize attacks of other people. Assess threat level in future.
			if (targetEntity == null || !targetEntity.Alive || (targetEnt is EntityHumanoid && targetEntity is not EntityHumanoid)) {
				targetEntity = targetEnt;
			}
			ShouldExecute();
		}

		private void OnStuck() {
			updateTargetPosFleeMode(entity.Pos.XYZ);
		}

		private void OnGoals() {
			pathTraverser.Retarget();
		}

		private void StopNow() {
			searchTask.SetTargetEnts(null);
			entity.ServerControls.StopAllMovement();
			entity.StopAnimation(prevAnims);
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
		
		/**private void CheckDoors(Vec3d target) {
			if (target != null) {
				// Check if there is a door in the way.
				BlockSelection blockSel = new BlockSelection();
				EntitySelection entitySel = new EntitySelection();

				entity.World.RayTraceForSelection(entity.ServerPos.XYZ.AddCopy(entity.LocalEyePos), entity.ServerPos.BehindCopy(4).XYZ, ref blockSel, ref entitySel);
				if (blockSel != null && doorIsBehind && blockSel.Block is BlockBaseDoor rearBlock && rearBlock.IsOpened()) {
					doorIsBehind = ToggleDoor(blockSel.Position);
				}

				entity.World.RayTraceForSelection(entity.ServerPos.XYZ.AddCopy(entity.LocalEyePos), target, ref blockSel, ref entitySel);
				if (blockSel != null && blockSel.Block is BlockBaseDoor baseBlock && !baseBlock.IsOpened()) {
					doorIsBehind = ToggleDoor(blockSel.Position);
				} else if (blockSel.Block is BlockDoor doorBlock && !doorBlock.IsOpened() && doorBlock.IsUpperHalf()) {
					BlockPos realBlock = new BlockPos(blockSel.Position.X, blockSel.Position.Y - 1, blockSel.Position.Z, blockSel.Position.dimension);
					doorIsBehind = ToggleDoor(realBlock);
				}
			}
		}

		private bool ToggleDoor(BlockPos pos) {
			if (pos.HorizontalManhattenDistance(entity.ServerPos.AsBlockPos) > 3) {
				return false;
			}
			if (entity.World.BlockAccessor.GetBlock(pos) is not BlockBaseDoor doorBlock) {
				return false;
			}

			var doorBehavior = BlockBehaviorDoor.getDoorAt(entity.World, pos);
			bool stateOpened = doorBlock.IsOpened();
			bool canBeOpened = TestAccess(pos);

			if (canBeOpened && doorBehavior != null) {
				doorBehavior.ToggleDoorState(null, stateOpened);
				doorBlock.OnBlockInteractStart(entity.World, null, new BlockSelection(pos, BlockFacing.UP, doorBlock));
				return true;
			}
			return false;
		}

		private bool DamageDoor(BlockPos pos) {
			// Break down door over time.
			return false;
		}

		private bool TestAccess(BlockPos pos) {
			if (entity.World.Claims.Get(pos).Length <= 0) {
				return true;
			}
			if (entity.World.Claims.TestAccess(entity.World.PlayerByUid(entity.leadersID), pos, EnumBlockAccessFlags.Use) == EnumWorldAccessResponse.Granted) {
				return true;
			}
			foreach (var claim in entity.World.Claims.Get(pos)) {
				if (entity.World.PlayerByUid(claim.OwnedByPlayerUid)?.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") == entity.kingdomID) {
					return true;
				}
			}
			return false;
		}**/
	}
}