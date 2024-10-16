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
		protected bool isInEntRange = false;
		protected bool arrowSharing = false;
		protected int minduration;
		protected int maxduration;
		protected long idleUntilMs;
		protected long lastInRange;
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
		protected Entity lookatEnt;
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
					isInEntRange = InRange();
					lastInRange = elapsedMilliseconds;
				}
				if (isInEntRange) {
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
			arrowSharing = !entity.AmmoItemSlot.Empty && entity.AmmoItemSlot.StackSize > 10;
			if (maxduration < 0) {
				idleUntilMs = -1L;
			} else {
				idleUntilMs = entity.World.ElapsedMilliseconds + minduration + entity.World.Rand.Next(maxduration - minduration);
			}
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
			if (rand.NextDouble() < 0.3) {
				long elapsedMilliseconds = entity.World.ElapsedMilliseconds;
				if (elapsedMilliseconds - lastInRange > 1500) {
					isInEntRange = InRange();
					lastInRange = elapsedMilliseconds;
				}
				if (isInEntRange && (lookatEnt?.Alive ?? false)) {
					Vec3f targetVec = lookatEnt.ServerPos.XYZFloat.Sub(entity.ServerPos.XYZFloat);
					targetVec.Set((float)(lookatEnt.ServerPos.X - entity.ServerPos.X), (float)(lookatEnt.ServerPos.Y - entity.ServerPos.Y), (float)(lookatEnt.ServerPos.Z - entity.ServerPos.Z));
					float desiredYaw = (float)Math.Atan2(targetVec.X, targetVec.Z);
					float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);
					entity.ServerPos.Yaw += GameMath.Clamp(yawDist, -curTurnAnglePerSec * dt, curTurnAnglePerSec * dt);
					entity.ServerPos.Yaw = entity.ServerPos.Yaw % GameMath.TWOPI;
					isInEntRange = lookatEnt.ServerPos.SquareHorDistanceTo(entity.ServerPos.XYZ) <= 4;
					return true;
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

		private bool InRange() {
			bool found = false;
			partitionUtil.WalkEntities(entity.ServerPos.XYZ, 2, delegate (Entity ent) {
				if (ent.Alive && ent is EntityPlayer) {
					lookatEnt = ent;
					found = true;
					return true;
				}
				return false;
			}, EnumEntitySearchType.Creatures);
			return lookatEnt != null && found;
		}
	}
}