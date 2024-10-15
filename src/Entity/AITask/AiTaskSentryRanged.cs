using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Util;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using VSKingdom.Utilities;
using System.Collections.Generic;

namespace VSKingdom {
	public class AiTaskSentryRanged : AiTaskBaseTargetable {
		public AiTaskSentryRanged(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		protected bool animsStarted = false;
		protected bool cancelAttack = false;
		protected bool renderSwitch = false;
		protected bool releasedShot = false;
		protected bool banditPilled = false;
		protected long durationAtMs;
		protected long releasesAtMs = 1000L;
		protected long durationOfMs = 1200L;
		protected long lastAttackMs;
		protected float maximumRange = 20f;
		protected float minimumRange = 6f;
		protected float accum = 0;
		protected float minTurnAnglePerSec;
		protected float maxTurnAnglePerSec;
		protected float curTurnAnglePerSec;
		protected AnimationMetaData drawWeapMeta;
		protected AnimationMetaData fireWeapMeta;
		protected AnimationMetaData loadWeapMeta;
		protected AssetLocation drawingsound;
		protected AssetLocation hittingsound;
		protected AssetLocation weapLocation;
		protected AssetLocation ammoLocation;
		protected AiTaskManager tasksManager;
		protected AiTaskSentrySearch searchTask => tasksManager.GetTask<AiTaskSentrySearch>();

		public override void AfterInitialize() {
			world = entity.World;
			tasksManager = entity.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
		}

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			this.mincooldown = taskConfig["mincooldown"].AsInt(500);
			this.maxcooldown = taskConfig["maxcooldown"].AsInt(1500);
			this.minimumRange = taskConfig["minimumRange"].AsFloat(4f);
			this.maximumRange = taskConfig["maximumRange"].AsFloat(20f);
			this.banditPilled = taskConfig["isBandit"].AsBool(false);
		}

		public override bool ShouldExecute() {
			if (!entity.cachedData.usesRange || !entity.cachedData.weapReady || !entity.ruleOrder[2]) {
				return false;
			}
			if (cooldownUntilMs > entity.World.ElapsedMilliseconds || entity.World.ElapsedMilliseconds - lastAttackMs < durationOfMs) {
				return false;
			}
			if (entity.World.ElapsedMilliseconds - attackedByEntityMs > 30000) {
				attackedByEntity = null;
			}
			if (retaliateAttacks && attackedByEntity != null && attackedByEntity.Alive && attackedByEntity.IsInteractable && IsTargetableEntity(attackedByEntity, maximumRange, ignoreEntityCode: true) && hasDirectContact(attackedByEntity, maximumRange, maximumRange / 2f)) {
				targetEntity = attackedByEntity;
			}
			if (durationAtMs + releasesAtMs < entity.World.ElapsedMilliseconds) {
				if (targetEntity == null || !targetEntity.Alive) {
					goto IL_0095;
				}
			}
			if (durationAtMs + releasesAtMs * 5 < entity.World.ElapsedMilliseconds) {
				goto IL_0095;
			}
			goto IL_00d6;
		IL_0095:
			durationAtMs = entity.World.ElapsedMilliseconds;
			if (rand.Next(0, 1) == 0) {
				targetEntity = partitionUtil.GetNearestInteractableEntity(entity.ServerPos.XYZ, maximumRange * 2f, (Entity ent) => IsTargetableEntity(ent, maximumRange) && hasDirectContact(ent, maximumRange, minimumRange));
			} else {
				var targetList = entity.World.GetEntitiesAround(entity.ServerPos.XYZ, maximumRange, maximumRange * 2f, (Entity ent) => IsTargetableEntity(ent, maximumRange) && hasDirectContact(ent, maximumRange, minimumRange));
				targetEntity = targetList[rand.Next(0, targetList.Length - 1)];
			}
			goto IL_00d6;
		IL_00d6:
			lastAttackMs = entity.World.ElapsedMilliseconds;
			return targetEntity?.Alive ?? false;
		}

		public override bool IsTargetableEntity(Entity ent, float range, bool ignoreEntityCode = false) {
			if (ent is null || ent == entity || !ent.Alive) {
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
			accum = 0;
			animsStarted = false;
			cancelAttack = false;
			renderSwitch = false;
			releasedShot = false;
			drawWeapMeta = new AnimationMetaData() {
				Code = new string(entity.cachedData.drawAnims),
				Animation = new string(entity.cachedData.drawAnims),
				BlendMode = EnumAnimationBlendMode.Average,
				ElementWeight = new Dictionary<string, float> {
					{ "UpperTorso", 5f },
					{ "ItemAnchor", 10f },
					{ "UpperArmR", 10f },
					{ "LowerArmR", 10f },
					{ "UpperArmL", 10f },
					{ "LowerArmL", 10f },
				}
			};
			fireWeapMeta = new AnimationMetaData() {
				Code = new string(entity.cachedData.fireAnims),
				Animation = new string(entity.cachedData.fireAnims),
				BlendMode = EnumAnimationBlendMode.Average,
				ElementWeight = new Dictionary<string, float> {
					{ "UpperTorso", 5f },
					{ "ItemAnchor", 10f },
					{ "UpperArmR", 10f },
					{ "LowerArmR", 10f },
					{ "UpperArmL", 10f },
					{ "LowerArmL", 10f },
				}
			};
			loadWeapMeta = new AnimationMetaData() {
				Code = new string(entity.cachedData.loadAnims),
				Animation = new string(entity.cachedData.loadAnims),
				BlendMode = EnumAnimationBlendMode.Average
			};
			// Get and initialize the item's attributes to the weapon.
			WeaponProperties properties = Constants.GlobalProps.WeaponProperties[entity.cachedData.weapCodes];
			AssetLocation[] drawAudio = properties.drawAudio;
			drawingsound = drawAudio.Length > 1 ? drawAudio[rand.Next(0, drawAudio.Length - 1)] : drawAudio[0];
			AssetLocation[] fireAudio = properties.fireAudio;
			hittingsound = fireAudio.Length > 1 ? fireAudio[rand.Next(0, fireAudio.Length - 1)] : fireAudio[0];
			durationOfMs = properties.loadSpeed;
			weapLocation = entity.RightHandItemSlot?.Itemstack?.Collectible?.Code;
			ammoLocation = entity.GearInventory[18]?.Itemstack?.Collectible?.Code;
			// Start switching the renderVariant to change to aiming.
			RenderVariants(1);
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
			// Only move if not above the target and safe!
			if (entity.ServerPos.Y - targetEntity.ServerPos.Y < 4 && entity.ServerPos.HorDistanceTo(targetEntity.ServerPos) < 3) {
				searchTask.SetTargetEnts(targetEntity);
			}
		}

		public override bool ContinueExecute(float dt) {
			if (cancelAttack || (!targetEntity?.Alive ?? true) || !entity.cachedData.usesRange || entity.Swimming) {
				return false;
			}
			// Calculate aiming at targetEntity!
			Vec3f targetVec = targetEntity.ServerPos.XYZFloat.Sub(entity.ServerPos.XYZFloat);
			targetVec.Set((float)(targetEntity.ServerPos.X - entity.ServerPos.X), (float)(targetEntity.ServerPos.Y - entity.ServerPos.Y), (float)(targetEntity.ServerPos.Z - entity.ServerPos.Z));
			float desiredYaw = (float)Math.Atan2(targetVec.X, targetVec.Z);
			float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);
			entity.ServerPos.Yaw += GameMath.Clamp(yawDist, -curTurnAnglePerSec * dt, curTurnAnglePerSec * dt);
			entity.ServerPos.Yaw = entity.ServerPos.Yaw % GameMath.TWOPI;
			if (Math.Abs(yawDist) > 0.02) {
				return true;
			}
			// Start animations if not already doing so.
			if (!animsStarted) {
				animsStarted = true;
				searchTask.StopMovements();
				entity.AnimManager.StartAnimation(drawWeapMeta.Init());
				if (drawingsound != null) {
					entity.World.PlaySoundAt(drawingsound, entity, null, false);
				}
			}

			accum += dt;

			// Draw back the weapon to its render variant if it has one. 
			if (!renderSwitch && accum > durationOfMs / 2000f) {
				RenderVariants(3);
				renderSwitch = true;
			}
			// Do after aiming time is finished.
			if (accum > releasesAtMs / 1000f && !releasedShot && !NothinInTheWay() && entity.cachedData.weapReady) {
				releasedShot = FireProjectile();
				// Don't play anything when the hittingSound is incorrectly set.
				if (hittingsound != null && releasedShot) {
					entity.World.PlaySoundAt(hittingsound, entity, null, false);
				}
			}
			return accum < durationOfMs / 1000f && !cancelAttack;
		}

		public override void FinishExecute(bool cancelled) {
			cooldownUntilMs = entity.World.ElapsedMilliseconds + mincooldown + entity.World.Rand.Next(maxcooldown - mincooldown);
			RenderVariants(0);
			entity.AnimManager.StopAnimation(new string(entity.cachedData.drawAnims));
			if (targetEntity == null || !targetEntity.Alive) {
				searchTask.ResetsTargets();
				searchTask.StopMovements();
			}
		}

		public override void OnEntityHurt(DamageSource source, float damage) {
			if (source?.GetCauseEntity() == null) {
				return;
			}
			if (source.GetCauseEntity().WatchedAttributes.HasAttribute(KingdomGUID) && source.GetCauseEntity().WatchedAttributes.GetKingdom() == entity.cachedData.kingdomGUID) {
				if (entity.WatchedAttributes.GetKingdom() != CommonersID) {
					return;
				}
			}
			// Ignore projectiles for the most part, only cancel attack.
			if (source?.SourceEntity is EntityProjectile && damage < 5) {
				cancelAttack = true;
			}
			attackedByEntity = source.GetCauseEntity();
			attackedByEntityMs = entity.World.ElapsedMilliseconds;
			// Interrupt attack and flee! Enemy is close!
			if (entity.ServerPos.Y - source.GetCauseEntity().ServerPos.Y < 4 && entity.ServerPos.HorDistanceTo(attackedByEntity.ServerPos) < minimumRange) {
				cancelAttack = true;
				FinishExecute(true);
				searchTask.SetTargetEnts(attackedByEntity);
			} else {
				base.OnEntityHurt(source, damage);
			}
		}

		public void OnAllyAttacked(Entity byEntity) {
			if (byEntity != entity) {
				if (targetEntity == null || !targetEntity.Alive) {
					targetEntity = byEntity;
				}
			}
			ShouldExecute();
		}

		private bool FireProjectile() {
			bool infiniteAmmo = entity.Api.World.Config.GetAsBool(NpcInfAmmos);
			bool isModdedAmmo = ammoLocation.Domain != "game" && CompatibleRange.Contains(ammoLocation.Domain);
			WeaponProperties weaponprop = Constants.GlobalProps.WeaponProperties[entity.cachedData.weapCodes];
			EntityProperties properties = entity.World.GetEntityType(isModdedAmmo ? new AssetLocation(weaponprop.ammoShape) : ammoLocation);
			EntityProjectile projectile = (EntityProjectile)entity.World.ClassRegistry.CreateEntity(properties);
			// Get damage according to type of projectile.
			projectile.ProjectileStack = new ItemStack(entity.World.GetItem(ammoLocation));
			projectile.FiredBy = entity;
			projectile.World = entity.World;
			projectile.DropOnImpactChance = infiniteAmmo ? 0 : 1f - (entity.AmmoItemSlot.Itemstack.ItemAttributes["breakChanceOnImpact"].AsFloat(0.5f));
			projectile.Damage = isModdedAmmo ? (float)AmmunitionDamages[ammoLocation.ToString()] * (float)WeaponMultipliers[weapLocation.ToString()] :
				entity.AmmoItemSlot.Itemstack.Collectible.Attributes["damage"].AsFloat() + entity.RightHandItemSlot.Itemstack.Collectible.Attributes["damage"].AsFloat();
			// Adjust projectile position and velocity to be used.
			Vec3d aheadPos = entity.ServerPos.AheadCopy(0.25).XYZ.AddCopy(0, entity.LocalEyePos.Y, 0);
			Vec3d tagetPos = targetEntity.ServerPos.XYZ.AddCopy(0, targetEntity.LocalEyePos.Y, 0);
			double distf = Math.Pow(aheadPos.SquareDistanceTo(tagetPos), 0.1);
			double curve = isModdedAmmo ? 0 : aheadPos.DistanceTo(tagetPos) / 16;
			double speed = isModdedAmmo ? weaponprop.ammoSpeed : GameMath.Clamp(distf - 1f, 0.1f, 1f);
			Vec3d velocity = (tagetPos - aheadPos + new Vec3d(0, curve, 0)).Normalize() * speed;
			// Set final projectile parameters, position, velocity, from point, and rotation.
			projectile.ServerPos.SetPos(entity.ServerPos.AheadCopy(0.5).XYZ.Add(0, entity.LocalEyePos.Y, 0));
			projectile.ServerPos.Motion.Set(velocity);
			projectile.Pos.SetFrom(projectile.ServerPos);
			projectile.SetRotation();
			// Spawn and fire the entity with given parameters.
			RenderVariants(0);
			entity.World.SpawnEntity(projectile);
			if (weaponprop.usesSmoke) {
				SpawnParticles();
			}
			if (!infiniteAmmo) {
				entity.GearInventory[18]?.TakeOut(1);
				entity.GearInventory[18]?.MarkDirty();
				entity.cachedData.UpdateReloads(entity.GearInventory);
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

		private void SpawnParticles() {
			Int32 smokeColours = ColorUtil.ToRgba(100, 245, 245, 245);
			Vec3d gunBarrelPos = entity.ServerPos.XYZ.AddCopy(entity.LocalEyePos);
			Vec3d gunTrailsPos = entity.ServerPos.XYZ.AddCopy(entity.LocalEyePos).AheadCopy(6, entity.ServerPos.Pitch, entity.ServerPos.Yaw + GameMath.PIHALF);
			Vec3f minTrailsVel = new Vec3f(1f, 0f, 0f);
			Vec3f maxTrailsVel = new Vec3f(1f, 0f, 0f);
			var smoke = new SimpleParticleProperties(5f, 40f, smokeColours, gunBarrelPos, gunTrailsPos, minTrailsVel, maxTrailsVel, rand.Next(4, 10), 0f, 1f, 6f, EnumParticleModel.Quad);
			smoke.ShouldDieInLiquid = true;
			smoke.ShouldSwimOnLiquid = true;
			smoke.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -2f);
			entity.World.SpawnParticles(smoke);
		}
		
		private bool NothinInTheWay() {
			var eSelect = new EntitySelection();
			var bSelect = new BlockSelection();
			var eFilter = new EntityFilter((e) => (e.IsInteractable || e.EntityId != entity.EntityId || e.EntityId != targetEntity.EntityId));
			var bFilter = new BlockFilter((pos, block) => (block.Replaceable < 6000));
			// Do a line Trace into the target, see if there are any entities in the way.
			entity.World.RayTraceForSelection(entity.ServerPos.XYZ.AddCopy(entity.LocalEyePos), targetEntity?.ServerPos.XYZ.AddCopy(targetEntity?.LocalEyePos), ref bSelect, ref eSelect, bFilter, eFilter);
			// Make sure the target isn't obstructed by other entities, but if it IS then make sure it's okay to hit them.
			if (eSelect?.Entity != null) {
				return !IsTargetableEntity(eSelect?.Entity, (float)entity.ServerPos.DistanceTo(eSelect.Position));
			}
			// Don't waste ammunition by shooting at the hecking ground.
			return bSelect?.Block == null;
		}

		private void RenderVariants(int variant) {
			if (entity.RightHandItemSlot?.Itemstack?.Attributes.HasAttribute("renderVariant") ?? false) {
				entity.RightHandItemSlot.Itemstack.Attributes.SetInt("renderVariant", variant);
				entity.RightHandItemSlot.MarkDirty();
			}
		}
	}
}