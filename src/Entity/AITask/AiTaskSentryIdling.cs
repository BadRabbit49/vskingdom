using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class AiTaskSentryIdling : AiTaskBase {
		public AiTaskSentryIdling(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		protected bool cancelIdling = false;
		protected bool entityWasInRange;
		protected int minduration;
		protected int maxduration;
		protected long idleUntilMs;
		protected long lastInRange;
		protected float turnSpeedMul = 0.75f;
		protected float currentTemps => entity.Api.World.BlockAccessor.GetClimateAt(entity.ServerPos.AsBlockPos, EnumGetClimateMode.NowValues).Temperature;
		protected float currentHours => entity.World.Calendar.HourOfDay;
		protected float darkoutHours => (entity.World.Calendar.HoursPerDay / 24f) * 20f;
		protected float morningHours => (entity.World.Calendar.HoursPerDay / 24f) * 5f;
		protected string currAnims;
		protected string basicIdle;
		protected string[] animsIdle;
		protected string[] animsHurt;
		protected string[] animsLate;
		protected string[] animsCold;
		protected string[] animsSwim;
		protected EntityBehaviorHealth healthBehavior;
		protected EntityPartitioning partitionUtil;
		protected AssetLocation onBlockBelowCode;

		public override void AfterInitialize() {
			base.AfterInitialize();
			healthBehavior = entity.GetBehavior<EntityBehaviorHealth>();
		}

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			this.partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();
			this.minduration = taskConfig["minduration"].AsInt(2000);
			this.maxduration = taskConfig["maxduration"].AsInt(4000);
			this.turnSpeedMul = taskConfig["turnSpeedMul"].AsFloat(0.75f);
			this.basicIdle = taskConfig["animation"].AsString("idle");
			this.animsIdle = taskConfig["animsIdle"].AsArray<string>(new string[] { "idle1", "idle2" });
			this.animsHurt = taskConfig["animsHurt"].AsArray<string>(new string[] { "hurtidle" });
			this.animsLate = taskConfig["animsLate"].AsArray<string>(new string[] { "yawn", "stretch" });
			this.animsCold = taskConfig["animsCold"].AsArray<string>(new string[] { "coldidle" });
			this.animsSwim = taskConfig["animsSwim"].AsArray<string>(new string[] { "swimidle" });
			idleUntilMs = entity.World.ElapsedMilliseconds + minduration + entity.World.Rand.Next(maxduration - minduration);
			base.LoadConfig(taskConfig, aiConfig);
		}

		public override bool ShouldExecute() {
			long elapsedMilliseconds = entity.World.ElapsedMilliseconds;
			if (cooldownUntilMs < elapsedMilliseconds && entity.World.Rand.NextDouble() < 1.1) {
				if (!EmotionStatesSatisifed()) {
					return false;
				}
				if (elapsedMilliseconds - lastInRange > 2000) {
					entityWasInRange = InRange();
					lastInRange = elapsedMilliseconds;
				}
				if (entityWasInRange) {
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
				return true;
			}
			return entity.Alive && cooldownUntilMs < entity.World.ElapsedMilliseconds;
		}

		public override void StartExecute() {
			cancelIdling = false;
			if (maxduration < 0) {
				idleUntilMs = -1L;
			} else {
				idleUntilMs = entity.World.ElapsedMilliseconds + minduration + entity.World.Rand.Next(maxduration - minduration);
			}
			entity.IdleSoundChanceModifier = 0f;
			currAnims = basicIdle;
			bool doSpecial = entity.World.Rand.NextDouble() < 0.1;
			if (entity.Swimming) {
				currAnims = animsSwim[entity.World.Rand.Next(0, animsSwim.Length - 1)];
			} else if (doSpecial) {
				if (currentTemps < 0) {
					currAnims = animsCold[entity.World.Rand.Next(0, animsCold.Length - 1)];
				} else if (currentHours > darkoutHours || currentHours < morningHours) {
					currAnims = animsLate[entity.World.Rand.Next(0, animsLate.Length - 1)];
				} else if (healthBehavior?.Health < healthBehavior?.MaxHealth / 4f) {
					currAnims = animsHurt[entity.World.Rand.Next(0, animsHurt.Length - 1)];
				} else {
					currAnims = animsIdle[entity.World.Rand.Next(0, animsIdle.Length - 1)];
				}	
			}
			if (currAnims != null) {
				entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = currAnims, Code = currAnims, SupressDefaultAnimation = true }.Init());
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
			if (rand.NextDouble() < 0.3) {
				long elapsedMilliseconds = entity.World.ElapsedMilliseconds;
				if (elapsedMilliseconds - lastInRange > 1500) {
					entityWasInRange = InRange();
					lastInRange = elapsedMilliseconds;
				}
				if (entityWasInRange) {
					return false;
				}
			}
			if (!cancelIdling) {
				if (idleUntilMs >= 0) {
					return entity.World.ElapsedMilliseconds < idleUntilMs;
				}
				return entity.ServerPos.Motion.Length() > 2;
			}
			return false;
		}

		public override void FinishExecute(bool cancelled) {
			cooldownUntilMs = entity.World.ElapsedMilliseconds + mincooldown + entity.World.Rand.Next(maxcooldown - mincooldown);
			if (currAnims != null && currAnims != "idle") {
				entity.AnimManager.StopAnimation(currAnims);
			}
		}

		public override void OnEntityHurt(DamageSource source, float damage) {
			cancelIdling = true;
		}

		private bool InRange() {
			bool found = false;
			partitionUtil.WalkEntities(entity.ServerPos.XYZ, 1, delegate (Entity ent) {
				if (!ent.Alive || ent.EntityId == entity.EntityId || !ent.IsInteractable || ent is not EntityPlayer) {
					return true;
				}
				found = true;
				return false;
			}, EnumEntitySearchType.Creatures);
			return found;
		}
	}
}