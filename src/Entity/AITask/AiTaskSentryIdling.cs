using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class AiTaskSentryIdling : AiTaskBase {
		public AiTaskSentryIdling(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		protected int minduration;
		protected int maxduration;
		protected long idleUntilMs;
		protected float turnSpeedMul = 0.75f;
		protected float currentTemps => entity.Api.World.BlockAccessor.GetClimateAt(entity.ServerPos.AsBlockPos, EnumGetClimateMode.NowValues).Temperature;
		protected float currentHours => entity.World.Calendar.HourOfDay;
		protected float darkoutHours => (entity.World.Calendar.HoursPerDay / 24f) * 20f;
		protected float morningHours => (entity.World.Calendar.HoursPerDay / 24f) * 5f;

		protected AnimationMetaData basicIdle;
		protected AnimationMetaData[] animsIdle;
		protected AnimationMetaData[] animsHurt;
		protected AnimationMetaData[] animsLate;
		protected AnimationMetaData[] animsCold;
		protected AnimationMetaData[] animsSwim;
		protected EntityBehaviorHealth healthBehavior;

		public override void AfterInitialize() {
			base.AfterInitialize();
			healthBehavior = entity.GetBehavior<EntityBehaviorHealth>();
		}

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			this.minduration = (int)taskConfig["minduration"]?.AsInt(2000);
			this.maxduration = (int)taskConfig["maxduration"]?.AsInt(4000);
			this.turnSpeedMul = (float)taskConfig["turnSpeedMul"]?.AsFloat(0.75f);
			this.basicIdle = new AnimationMetaData() {
				Animation = (string)taskConfig["animation"]?.AsString("idle").ToLowerInvariant(),
				Code = (string)taskConfig["animation"]?.AsString("idle").ToLowerInvariant(),
				BlendMode = EnumAnimationBlendMode.Average
			}.Init();
			string[] animIdleCodes = (string[])taskConfig["animsIdle"]?.AsArray<string>(new string[] { "idle1", "idle2" });
			this.animsIdle = new AnimationMetaData[animIdleCodes.Length];
			for (int i = 0; i < animIdleCodes.Length; i++) {
				animsIdle[i] = new AnimationMetaData() {
					Animation = animIdleCodes[i].ToLowerInvariant(),
					Code = animIdleCodes[i].ToLowerInvariant(),
					BlendMode = EnumAnimationBlendMode.Average
				}.Init();
			}
			string[] animHurtCodes = taskConfig["animsHurt"].AsArray<string>(new string[] { "hurtidle" });
			this.animsHurt = new AnimationMetaData[animHurtCodes.Length];
			for (int i = 0; i < animHurtCodes.Length; i++) {
				animsHurt[i] = new AnimationMetaData() {
					Animation = animHurtCodes[i].ToLowerInvariant(),
					Code = animHurtCodes[i].ToLowerInvariant(),
					BlendMode = EnumAnimationBlendMode.Average
				}.Init();
			}
			string[] animLateCodes = taskConfig["animsLate"].AsArray<string>(new string[] { "yawn", "stretch" });
			this.animsLate = new AnimationMetaData[animLateCodes.Length];
			for (int i = 0; i < animLateCodes.Length; i++) {
				animsLate[i] = new AnimationMetaData() {
					Animation = animLateCodes[i].ToLowerInvariant(),
					Code = animLateCodes[i].ToLowerInvariant(),
					BlendMode = EnumAnimationBlendMode.Average
				}.Init();
			}
			string[] animColdCodes = taskConfig["animsCold"].AsArray<string>(new string[] { "coldidle" });
			this.animsCold = new AnimationMetaData[animColdCodes.Length];
			for (int i = 0; i < animColdCodes.Length; i++) {
				animsCold[i] = new AnimationMetaData() {
					Animation = animColdCodes[i].ToLowerInvariant(),
					Code = animColdCodes[i].ToLowerInvariant(),
					BlendMode = EnumAnimationBlendMode.Average
				}.Init();
			}
			string[] animSwimCodes = taskConfig["animsSwim"].AsArray<string>(new string[] { "swimidle" });
			this.animsSwim = new AnimationMetaData[animSwimCodes.Length];
			for (int i = 0; i < animSwimCodes.Length; i++) {
				animsSwim[i] = new AnimationMetaData() {
					Animation = animSwimCodes[i].ToLowerInvariant(),
					Code = animSwimCodes[i].ToLowerInvariant(),
					BlendMode = EnumAnimationBlendMode.Average
				}.Init();
			}
			idleUntilMs = entity.World.ElapsedMilliseconds + minduration + entity.World.Rand.Next(maxduration - minduration);
			base.LoadConfig(taskConfig, aiConfig);
		}

		public override bool ShouldExecute() {
			return !pathTraverser.Active && entity.Alive && cooldownUntilMs < entity.World.ElapsedMilliseconds;
		}

		public override void StartExecute() {
			animMeta = basicIdle.Clone();
			if (entity.Swimming) {
				animMeta = animsSwim[rand.Next(0, animsSwim.Length - 1)].Clone();
			} else if (currentTemps < 10f) {
				animMeta = animsCold[rand.Next(0, animsCold.Length - 1)].Clone();
			} else if (rand.NextDouble() < 0.1 && (currentHours > darkoutHours || currentHours < morningHours)) {
				animMeta = animsLate[rand.Next(0, animsLate.Length - 1)].Clone();
			} else if (healthBehavior?.Health < healthBehavior?.MaxHealth / 4f) {
				animMeta = animsHurt[rand.Next(0, animsHurt.Length - 1)].Clone();
			} else if (rand.NextDouble() < 0.1) {
				animMeta = animsIdle[rand.Next(0, animsIdle.Length - 1)].Clone();
			}
			if (animMeta != null) {
				entity.AnimManager.StartAnimation(animMeta);
			}
			idleUntilMs = entity.World.ElapsedMilliseconds + minduration + entity.World.Rand.Next(maxduration - minduration);
			if (!(sound != null) || !(entity.World.Rand.NextDouble() <= (double)soundChance)) {
				return;
			}
			if (soundStartMs > 0) {
				entity.World.RegisterCallback(delegate {
					entity.World.PlaySoundAt(sound, entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z, null, randomizePitch: true, soundRange);
					lastSoundTotalMs = entity.World.ElapsedMilliseconds;
				}, soundStartMs);
			} else {
				entity.World.PlaySoundAt(sound, entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z, null, randomizePitch: true, soundRange);
				lastSoundTotalMs = entity.World.ElapsedMilliseconds;
			}
			entity.ServerPos.Yaw = (float)GameMath.Clamp(
				entity.World.Rand.NextDouble() * GameMath.TWOPI,
				entity.ServerPos.Yaw - GameMath.PI / 4 * GlobalConstants.OverallSpeedMultiplier * turnSpeedMul,
				entity.ServerPos.Yaw + GameMath.PI / 4 * GlobalConstants.OverallSpeedMultiplier * turnSpeedMul
			);
		}

		public override bool ContinueExecute(float dt) {
			return !pathTraverser.Active && entity.Alive && entity.World.ElapsedMilliseconds < idleUntilMs;
		}

		public override void FinishExecute(bool cancelled) {
			cooldownUntilMs = entity.World.ElapsedMilliseconds + mincooldown + entity.World.Rand.Next(maxcooldown - mincooldown);
			cooldownUntilTotalHours = entity.World.Calendar.TotalHours + mincooldownHours + entity.World.Rand.NextDouble() * (maxcooldownHours - mincooldownHours);
			if (animMeta != null && animMeta.Code != "idle") {
				entity.AnimManager.StopAnimation(animMeta.Code);
			}
			if (finishSound != null) {
				entity.World.PlaySoundAt(finishSound, entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z, null, randomizePitch: true, soundRange);
			}
		}
	}
}