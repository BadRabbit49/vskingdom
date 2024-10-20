using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using static VSKingdom.Utilities.GenericUtil;

namespace VSKingdom {
	public class AiTaskSentryIdling : AiTaskBase {
		public AiTaskSentryIdling(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		protected bool cancelIdling = false;
		protected bool arrowSharing = false;
		protected float turnSpeedMul = 0.75f;
		protected float currentTemps => entity.Api.World.BlockAccessor.GetClimateAt(entity.ServerPos.AsBlockPos, EnumGetClimateMode.NowValues).Temperature;
		protected float currentHours => entity.World.Calendar.HourOfDay;
		protected float darkoutHours => (entity.World.Calendar.HoursPerDay / 24f) * 20f;
		protected float morningHours => (entity.World.Calendar.HoursPerDay / 24f) * 5f;
		protected float minTurnAnglePerSec;
		protected float maxTurnAnglePerSec;
		protected float curTurnAnglePerSec;
		protected string currAnims;
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
			this.mincooldown = taskConfig["mincooldown"].AsInt(2000);
			this.maxcooldown = taskConfig["maxcooldown"].AsInt(4000);
			this.turnSpeedMul = taskConfig["turnSpeedMul"].AsFloat(0.75f);
			this.animsIdle = taskConfig["animsIdle"].AsArray<string>(new string[] { "idle1", "idle2" });
			this.animsHurt = taskConfig["animsHurt"].AsArray<string>(new string[] { "hurtidle" });
			this.animsLate = taskConfig["animsLate"].AsArray<string>(new string[] { "yawn", "stretch" });
			this.animsCold = taskConfig["animsCold"].AsArray<string>(new string[] { "coldidle" });
			this.animsSwim = taskConfig["animsSwim"].AsArray<string>(new string[] { "swimidle" });
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
			arrowSharing = !entity.AmmoItemSlot.Empty && entity.AmmoItemSlot.StackSize > 10;
			if (entity.World.Rand.NextDouble() < 0.1) {
				if (currentTemps < 0) {
					entity.AnimManager.StartAnimation(currAnims = animsCold[entity.World.Rand.Next(0, animsCold.Length - 1)]);
					if (entity.World.Rand.Next(100) < 30) {
						entity.PlayEntitySound("argue");
					}
				} else if (currentHours > darkoutHours || currentHours < morningHours) {
					entity.AnimManager.StartAnimation(currAnims = animsLate[entity.World.Rand.Next(0, animsLate.Length - 1)]);
					if (entity.World.Rand.NextDouble() < 0.0001) {
						entity.PlayEntitySound("idle" + entity.World.Rand.Next(1, 2));
					}
				} else if (healthBehavior?.Health < healthBehavior?.MaxHealth / 4f) {
					entity.AnimManager.StartAnimation(currAnims = animsHurt[entity.World.Rand.Next(0, animsHurt.Length - 1)]);
					if (entity.World.Rand.Next(100) < 10) {
						entity.PlayEntitySound("argue");
					}
				} else {
					entity.AnimManager.StartAnimation(currAnims = animsIdle[entity.World.Rand.Next(0, animsIdle.Length - 1)]);
				}
			}
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
			if (entity.Properties.Server?.Attributes != null) {
				ITreeAttribute pathfinder = entity.Properties.Server.Attributes.GetTreeAttribute("pathfinder");
				if (pathfinder != null) {
					minTurnAnglePerSec = pathfinder.GetFloat("minTurnAnglePerSec", 250f);
					maxTurnAnglePerSec = pathfinder.GetFloat("maxTurnAnglePerSec", 450f);
				}
			} else {
				minTurnAnglePerSec = 250f;
				maxTurnAnglePerSec = 450f;
			}
			curTurnAnglePerSec = minTurnAnglePerSec + (float)entity.World.Rand.NextDouble() * (maxTurnAnglePerSec - minTurnAnglePerSec);
			curTurnAnglePerSec *= GameMath.DEG2RAD * 50 * 0.02f;
		}

		public override bool ContinueExecute(float dt) {
			if (!cancelIdling) {
				if (cooldownUntilMs >= 0) {
					return entity.World.ElapsedMilliseconds < cooldownUntilMs;
				}
				return entity.ServerPos.Motion.Length() > 2;
			}
			return false;
		}

		public override void FinishExecute(bool cancelled) {
			cooldownUntilMs = entity.World.ElapsedMilliseconds + mincooldown + entity.World.Rand.Next(maxcooldown - mincooldown);			
			if (currAnims != null) {
				entity.AnimManager.StopAnimation(currAnims);
			}
			if (arrowSharing) {
				string thisKingdom = entity.WatchedAttributes.GetString(KingdomGUID);
				var sentsAround = world.GetEntitiesAround(entity.ServerPos.XYZ, 8f, 4f, match => match is EntitySentry);
				for (int i = 0; i < sentsAround.Length; i++) {
					try {
						if (sentsAround[i].WatchedAttributes.GetString(KingdomGUID) != thisKingdom || sentsAround[i].Properties.Attributes["baseClass"].AsString() != "range" || !CanSeeEnt(entity, sentsAround[i])) {
							continue;
						}
						EntitySentry sentry = sentsAround[i] as EntitySentry;
						if (sentry.AmmoItemSlot.Empty || (sentry.AmmoItemSlot.Itemstack.Item.Code == entity.AmmoItemSlot.Itemstack.Item.Code && sentry.AmmoItemSlot.StackSize < (entity.AmmoItemSlot.StackSize / 2))) {
							entity.AmmoItemSlot.TryPutInto(world, sentry.AmmoItemSlot, 4);
							entity.AmmoItemSlot.MarkDirty();
						}
					} catch { }
				}
			}
		}

		public override void OnEntityHurt(DamageSource source, float damage) {
			cancelIdling = true;
		}
	}
}