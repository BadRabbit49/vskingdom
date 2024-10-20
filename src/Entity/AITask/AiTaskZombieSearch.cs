using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class AiTaskZombieSearch : AiTaskBaseTargetable {
		public AiTaskZombieSearch(EntityZombie entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntityZombie entity;
		#pragma warning restore CS0108
		protected bool cancelSearch;
		protected bool lastPathfind;
		protected Int64 lastAttackMs;
		protected Int64 lastHurtAtMs;
		protected Int64 lastFinishMs;
		protected Int64 doorBashedMs;
		protected float currentFollowTime;
		protected float maximumFollowTime;
		protected float seekingRange;
		protected float currentSpeed;
		protected Vec3d curTargetPos;
		protected Vec3d lastGoalReachedAt;
		protected string prvAnimation;
		protected Dictionary<long, int> futilityCounters;
		protected EnumAttackPattern attackPattern;
		protected AiTaskManager tasksManager;
		protected AiTaskZombieAttack attackerTask { get => tasksManager.GetTask<AiTaskZombieAttack>(); }
		protected bool RecentlyTookDamages => entity.World.ElapsedMilliseconds - lastHurtAtMs < 10000;
		protected bool RemainInOffenseMode => entity.World.ElapsedMilliseconds - lastAttackMs < 20000;

		public override void AfterInitialize() {
			world = entity.World;
			bhPhysics = entity.GetBehavior<EntityBehaviorControlledPhysics>();
			bhEmo = entity.GetBehavior<EntityBehaviorEmotionStates>();
			pathTraverser = entity.GetBehavior<EntityBehaviorTaskAI>().PathTraverser;
			tasksManager = entity.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
		}

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			this.partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();
			this.retaliateAttacks = taskConfig["retaliateAttacks"].AsBool(true);
			this.maximumFollowTime = taskConfig["maximumFollowTime"].AsFloat(60f);
			this.seekingRange = taskConfig["seekingRange"].AsFloat(25f);
		}

		public override bool ShouldExecute() {
			return targetEntity?.Alive ?? false;
		}

		public override void StartExecute() {
			cancelSearch = false;
			currentFollowTime = 0f;
			doorBashedMs = 0;
			curTargetPos = targetEntity.ServerPos.XYZ;
			DoDirect();
		}

		public override bool CanContinueExecute() {
			if (pathTraverser.Ready) {
				lastAttackMs = entity.World.ElapsedMilliseconds;
				lastPathfind = true;
				return true;
			}
			return false;
		}

		public override bool ContinueExecute(float dt) {
			if (targetEntity?.Alive ?? false || cancelSearch) {
				return false;
			}
			if (currentFollowTime == 0f && world.Rand.Next(100) < 25) {
				base.StartExecute();
			}
			currentFollowTime += dt;
			Vec3d vec3 = entity.ServerPos.XYZ.Add(0.0, entity.SelectionBox.Y2 / 2f, 0.0).Ahead(entity.SelectionBox.XSize / 2f, 0f, entity.ServerPos.Yaw);
			double dist = targetEntity.SelectionBox.ToDouble().Translate(targetEntity.ServerPos.XYZ).ShortestDistanceFrom(vec3);
			bool flag = targetEntity != null && targetEntity.Alive && !cancelSearch && pathTraverser.Active;
			if (flag && currentFollowTime < maximumFollowTime && dist < seekingRange) {
				if (!(dist > 32f)) {
					if (targetEntity is EntityAgent entityAgent) {
						return entityAgent?.ServerControls.TriesToMove ?? false;
					}
					return false;
				}
				return true;
			}
			return false;
		}

		public override void FinishExecute(bool cancelled) {
			cooldownUntilMs = entity.World.ElapsedMilliseconds + mincooldown + entity.World.Rand.Next(maxcooldown - mincooldown);
			lastFinishMs = entity.World.ElapsedMilliseconds;
			if (!targetEntity?.Alive ?? true) {
				ResetsTargets();
				StopMovements();
			}
		}

		public override bool Notify(string key, object data) {
			if (key == "seekEntity" && data != null) {
				targetEntity = (Entity)data;
				curTargetPos = targetEntity.ServerPos.XYZ;
				return true;
			}
			return false;
		}

		public bool IsBarricaded(BlockPos pos) {
			if (pos == null || pos.HorizontalManhattenDistance(entity.ServerPos.AsBlockPos) > 3) {
				return false;
			}
			return BlockBehaviorDoor.getDoorAt(world, pos) != null;
		}

		public bool HitBarricade(BlockPos pos) {
			if (world.ElapsedMilliseconds > doorBashedMs) {
				doorBashedMs = world.ElapsedMilliseconds + 2500L;
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
				world.BlockAccessor.DamageBlock(pos, pos.FacingFrom(entity.ServerPos.AsBlockPos), 6f);
			}
			return world.BlockAccessor.GetBlock(pos).BlockId != 0;
		}

		public void SetTargetVec3(Vec3d target) {
			curTargetPos = target ?? curTargetPos;
			DoDirect();
		}

		public void SetTargetEnts(Entity target) {
			targetEntity = target;
			curTargetPos = target?.ServerPos.XYZ ?? curTargetPos;
			DoDirect();
		}

		public void ResetsTargets() {
			cancelSearch = true;
			targetEntity = null;
			curTargetPos = null;
		}

		public void StopMovements() {
			pathTraverser.Stop();
			Animate(false);
		}

		private void DoDirect() {
			// Just go forward towards the target!
			currentSpeed = entity.moveSpeed;
			Animate(true);
			pathTraverser.NavigateTo_Async(curTargetPos.OffsetCopy(rand.Next(-1, 1), currentSpeed, rand.Next(-1, 1)), 1, 1.5f, OnGoals, OnStuck, DoSieged, world.Rand.Next(3500, 10000), 0);
		}

		private void DoSieged() {
			// Unable to perform direct attack pattern, trying sieged!
			currentSpeed = entity.walkSpeed;
			Animate(false);
			pathTraverser.NavigateTo_Async(curTargetPos, currentSpeed, 1.5f, OnGoals, OnStuck, NoPaths, world.Rand.Next(1500, 3500), 2);
		}

		private void OnGoals() {
			if (cancelSearch || targetEntity == null) {
				return;
			}
			if (lastGoalReachedAt != null && lastGoalReachedAt.SquareDistanceTo(entity.ServerPos) < 0.005f) {
				if (futilityCounters == null) {
					futilityCounters = new Dictionary<long, int>();
				} else {
					futilityCounters.TryGetValue(targetEntity.EntityId, out var value);
					value++;
					futilityCounters[targetEntity.EntityId] = value;
					if (value > 19) {
						return;
					}
				}
			}
			lastGoalReachedAt = new Vec3d(entity.Pos);
			pathTraverser.Retarget();
		}

		private void OnStuck() {
			// If a door is present, try breaking into it (if not iron or reinforced!)
			if (IsBarricaded(entity.ServerPos.HorizontalAheadCopy(1).AsBlockPos)) {
				attackerTask.SetBarricade(entity.ServerPos.HorizontalAheadCopy(1).AsBlockPos);
			}
		}

		private void NoPaths() {
			cancelSearch = true;
		}

		private void Animate(bool forcedSprint) {
			String anims = entity.idleAnims;
			if (!pathTraverser.Active) {
				entity.AnimManager.StopAnimation(prvAnimation);
			} else if (entity.Swimming) {
				anims = entity.swimAnims;
			} else if (entity.FeetInLiquid) {
				anims = entity.walkAnims;
			} else if (entity.Controls.Sneak) {
				anims = entity.duckAnims;
			} else if (forcedSprint) {
				anims = entity.moveAnims;
			} else if (currentSpeed > 0.01f) {
				anims = entity.walkAnims;
			} else if (currentSpeed < 0.01f) {
				entity.AnimManager.StopAnimation(prvAnimation);
			}
			if (!entity.AnimManager.IsAnimationActive(anims)) {
				entity.AnimManager.StartAnimation(new AnimationMetaData() {
					Animation = anims,
					Code = anims,
					BlendMode = EnumAnimationBlendMode.AddAverage,
					MulWithWalkSpeed = anims != entity.idleAnims,
					EaseInSpeed = 999f,
					EaseOutSpeed = 999f,
					ElementWeight = new Dictionary<string, float> {
						{ "UpperFootR", 2f },
						{ "UpperFootL", 2f },
						{ "LowerFootR", 2f },
						{ "LowerFootL", 2f }
					},
				}.Init());
			}
			prvAnimation = anims;
		}
	}
}