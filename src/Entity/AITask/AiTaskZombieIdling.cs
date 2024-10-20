using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using static VSKingdom.Utilities.GenericUtil;

namespace VSKingdom {
	public class AiTaskZombieIdling : AiTaskBase {
		public AiTaskZombieIdling(EntityZombie entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntityZombie entity;
		#pragma warning restore CS0108
		protected bool cancelIdling = false;
		protected bool arrowSharing = false;
		protected Int32 minDuration;
		protected Int32 maxDuration;
		protected Int64 idleUntilMs;
		protected float turnSpeedMul = 0.75f;
		protected float currentTemps => entity.Api.World.BlockAccessor.GetClimateAt(entity.ServerPos.AsBlockPos, EnumGetClimateMode.NowValues).Temperature;
		protected float currentHours => entity.World.Calendar.HourOfDay;
		protected float darkoutHours => (entity.World.Calendar.HoursPerDay / 24f) * 20f;
		protected float morningHours => (entity.World.Calendar.HoursPerDay / 24f) * 5f;
		protected float minTurnAnglePerSec;
		protected float maxTurnAnglePerSec;
		protected float curTurnAnglePerSec;
		protected string currAnims;
		protected string[] idleAnimations;
		protected string[] idleSoundCodes;
		protected EntityBehaviorHealth healthBehavior;
		protected EntityPartitioning partitionUtil;
		protected AssetLocation onBlockBelowCode;

		public override void AfterInitialize() {
			base.AfterInitialize();
			healthBehavior = entity.GetBehavior<EntityBehaviorHealth>();
		}

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			this.partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();
			this.minDuration = taskConfig["mincooldown"].AsInt(2000);
			this.maxDuration = taskConfig["maxcooldown"].AsInt(4000);
			this.turnSpeedMul = taskConfig["turnSpeedMul"].AsFloat(0.75f);
			this.idleAnimations = taskConfig["idlingCodes"].AsArray<string>(new string[] { "idle1", "idle2" });
			this.idleSoundCodes = taskConfig["soundsCodes"].AsArray<string>(new string[] { "game:creature/drifter-idle" });
			idleUntilMs = entity.World.ElapsedMilliseconds + minDuration + entity.World.Rand.Next(maxDuration - minDuration);
			base.LoadConfig(taskConfig, aiConfig);
		}

		public override bool ShouldExecute() {
			if (cooldownUntilMs > entity.World.ElapsedMilliseconds) {
				return false;
			}
			cooldownUntilMs = entity.World.ElapsedMilliseconds + mincooldown + entity.World.Rand.Next(maxcooldown - mincooldown);
			if (!EmotionStatesSatisifed()) {
				return false;
			}
			Block block = entity.World.BlockAccessor.GetBlock(new BlockPos((int)entity.ServerPos.X, (int)entity.ServerPos.Y - 1, (int)entity.ServerPos.Z, (int)entity.ServerPos.Dimension), 1);
			if (!block.SideSolid[BlockFacing.UP.Index]) {
				return false;
			}
			if (onBlockBelowCode == null) {
				return true;
			}
			Block block2 = entity.World.BlockAccessor.GetBlock(entity.ServerPos.AsBlockPos);
			if (!block2.WildCardMatch(onBlockBelowCode)) {
				if (block2.Replaceable >= 6000) {
					return block.WildCardMatch(onBlockBelowCode);
				}
				return false;
			}
			return entity.Alive;
		}

		public override void StartExecute() {
			cancelIdling = false;
			idleUntilMs = entity.World.ElapsedMilliseconds + minDuration + entity.World.Rand.Next(maxDuration - minDuration);
			if (entity.World.Rand.NextDouble() < 0.1) {
				entity.AnimManager.StartAnimation(GetRandom(idleAnimations));
			}
			if (entity.World.Rand.Next(100) > 80 && soundStartMs > 0) {
				sound = new AssetLocation(GetRandom(idleSoundCodes));
				entity.World.RegisterCallback(delegate {
					entity.World.PlaySoundAt(sound, entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z, null, randomizePitch: true, soundRange);
					lastSoundTotalMs = entity.World.ElapsedMilliseconds;
				}, soundStartMs);
			}
			entity.ServerPos.Yaw = (float)GameMath.Clamp(
				entity.World.Rand.NextDouble() * GameMath.TWOPI,
				entity.ServerPos.Yaw - GameMath.PI / 4 * GlobalConstants.OverallSpeedMultiplier * turnSpeedMul,
				entity.ServerPos.Yaw + GameMath.PI / 4 * GlobalConstants.OverallSpeedMultiplier * turnSpeedMul
			);
		}

		public override bool ContinueExecute(float dt) {
			if (cancelIdling || !EmotionStatesSatisifed()) {
				return false;
			}
			if (cooldownUntilMs >= 0) {
				return entity.World.ElapsedMilliseconds < cooldownUntilMs;
			}
			return entity.World.ElapsedMilliseconds < idleUntilMs;
		}

		public override void FinishExecute(bool cancelled) {
			cooldownUntilMs = entity.World.ElapsedMilliseconds + mincooldown + entity.World.Rand.Next(0, maxcooldown);			
			if (currAnims != null) {
				entity.AnimManager.StopAnimation(currAnims);
			}
		}

		public override void OnEntityHurt(DamageSource source, float damage) {
			cancelIdling = true;
		}
	}
}