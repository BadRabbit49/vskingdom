using System;
using System.Linq;
using System.Collections.Generic;
using Vintagestory.API.Util;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

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
		protected long totalDurationMs;
		protected long releasesAtMs = 1000L;
		protected long durationOfMs = 1200L;
		protected long lastAttackMs;
		protected float maximumRange = 20f;
		protected float minimumRange = 6f;
		protected float accum = 0;
		protected float minTurnAnglePerSec;
		protected float maxTurnAnglePerSec;
		protected float curTurnAnglePerSec;
		protected AnimationMetaData drawBowsMeta;
		protected AnimationMetaData fireBowsMeta;
		protected AnimationMetaData loadBowsMeta;
		protected EntityProperties projectileType;
		protected AssetLocation drawingsound;
		protected AssetLocation hittingsound;
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
			if (totalDurationMs + releasesAtMs < entity.World.ElapsedMilliseconds) {
				if (targetEntity == null || !targetEntity.Alive) {
					goto IL_0095;
				}
			}
			if (totalDurationMs + releasesAtMs * 5 < entity.World.ElapsedMilliseconds) {
				goto IL_0095;
			}
			goto IL_00d6;
			IL_0095:
			totalDurationMs = entity.World.ElapsedMilliseconds;
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
			if (ent.WatchedAttributes.HasAttribute(king_GUID)) {
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
			drawBowsMeta = new AnimationMetaData() {
				Code = new string(entity.cachedData.drawAnims),
				Animation = new string(entity.cachedData.drawAnims),
				BlendMode = EnumAnimationBlendMode.Add,
				ElementWeight = new Dictionary<string, float> {
					{ "UpperArmR", 90f },
					{ "LowerArmR", 90f },
					{ "UpperArmL", 90f },
					{ "LowerArmL", 90f },
					{ "ItemAnchor", 90f }
				},
				ElementBlendMode = new Dictionary<string, EnumAnimationBlendMode> {
					{ "UpperArmR", EnumAnimationBlendMode.AddAverage },
					{ "LowerArmR", EnumAnimationBlendMode.AddAverage },
					{ "UpperArmL", EnumAnimationBlendMode.AddAverage },
					{ "LowerArmL", EnumAnimationBlendMode.AddAverage },
					{ "ItemAnchor", EnumAnimationBlendMode.AddAverage }
				}
			}.Init();
			fireBowsMeta = new AnimationMetaData() {
				Code = new string(entity.cachedData.fireAnims),
				Animation = new string(entity.cachedData.fireAnims),
				BlendMode = EnumAnimationBlendMode.Add,
				EaseInSpeed = 999f,
				ElementWeight = new Dictionary<string, float> {
					{ "UpperArmR", 20f },
					{ "LowerArmR", 20f },
					{ "UpperArmL", 20f },
					{ "LowerArmL", 20f }
				},
				ElementBlendMode = new Dictionary<string, EnumAnimationBlendMode> {
					{ "UpperArmR", EnumAnimationBlendMode.AddAverage },
					{ "LowerArmR", EnumAnimationBlendMode.AddAverage },
					{ "UpperArmL", EnumAnimationBlendMode.AddAverage },
					{ "LowerArmL", EnumAnimationBlendMode.AddAverage }
				}
			};
			loadBowsMeta = new AnimationMetaData() {
				Code = new string(entity.cachedData.loadAnims),
				Animation = new string(entity.cachedData.loadAnims),
				BlendMode = EnumAnimationBlendMode.Add,
				ElementWeight = new Dictionary<string, float> {
					{ "UpperArmR", 90f },
					{ "LowerArmR", 90f },
					{ "UpperArmL", 90f },
					{ "LowerArmL", 90f },
					{ "ItemAnchor", 90f }
				},
				ElementBlendMode = new Dictionary<string, EnumAnimationBlendMode> {
					{ "UpperArmR", EnumAnimationBlendMode.AddAverage },
					{ "LowerArmR", EnumAnimationBlendMode.AddAverage },
					{ "UpperArmL", EnumAnimationBlendMode.AddAverage },
					{ "LowerArmL", EnumAnimationBlendMode.AddAverage },
					{ "ItemAnchor", EnumAnimationBlendMode.AddAverage }
				}
			};
			// Get and initialize the item's attributes to the weapon.
			AssetLocation[] drawAudio = WeaponProperties[entity.cachedData.weapCodes].drawAudio;
			drawingsound = drawAudio.Length > 1 ? drawAudio[rand.Next(0, drawAudio.Length - 1)] : drawAudio[0];
			AssetLocation[] fireAudio = WeaponProperties[entity.cachedData.weapCodes].fireAudio;
			hittingsound = fireAudio.Length > 1 ? fireAudio[rand.Next(0, fireAudio.Length - 1)] : fireAudio[0];
			ammoLocation = entity.GearInventory[18]?.Itemstack?.Collectible?.Code;
			// Start switching the renderVariant to change to aiming.
			entity.RightHandItemSlot?.Itemstack?.Attributes?.SetInt("renderVariant", 1);
			entity.RightHandItemSlot?.MarkDirty();
			// Get whatever the asset entity type is based on the item's code path.
			projectileType = entity.World.GetEntityType(ammoLocation);
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
			searchTask.SetTargetEnts(targetEntity);
		}

		public override bool ContinueExecute(float dt) {
			if (cancelAttack || (!targetEntity?.Alive ?? true) || !entity.cachedData.usesMelee || entity.Swimming) {
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
				entity.AnimManager.StartAnimation(drawBowsMeta.Init());
				if (drawingsound != null) {
					entity.World.PlaySoundAt(drawingsound, entity, null, false);
				}
			}

			accum += dt;

			// Draw back the weapon to its render variant if it has one. 
			if (!renderSwitch && accum > durationOfMs / 2000f) {
				entity.RightHandItemSlot.Itemstack?.Attributes?.SetInt("renderVariant", 3);
				entity.RightHandItemSlot.MarkDirty();
				renderSwitch = true;
			}
			// Do after aiming time is finished.
			if (accum > releasesAtMs / 1000f && !releasedShot && !EntityInTheWay() && entity.cachedData.weapReady) {
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
			entity.RightHandItemSlot?.Itemstack?.Attributes?.SetInt("renderVariant", 0);
			entity.RightHandItemSlot?.MarkDirty();
			entity.AnimManager.StopAnimation(new string(entity.cachedData.drawAnims));
			if (targetEntity == null || !targetEntity.Alive) {
				searchTask.ResetsTargets();
				searchTask.StopMovements();
			}
		}
		
		public override void OnEntityHurt(DamageSource source, float damage) {
			if (source.SourceEntity == null) {
				return;
			}
			// Ignore projectiles for the most part, only cancel attack.
			if (source.SourceEntity is EntityProjectile) {
				cancelAttack = true;
			}
			// Interrupt attack and flee! Enemy is close!
			if (source.GetCauseEntity().ServerPos.SquareDistanceTo(entity.ServerPos) < minimumRange * minimumRange) {
				cancelAttack = true;
				FinishExecute(true);
				searchTask.SetTargetEnts(targetEntity);
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
			bool infiniteAmmo = entity.Api.World.Config.GetAsBool("InfiniteAmmo");
			bool isModdedAmmo = entity.AmmoItemSlot.Itemstack.Item.Code.Domain != "game" && compatibleRange.Contains(entity.AmmoItemSlot.Itemstack.Item.Code.Domain);
			EntityProjectile projectile = (EntityProjectile)entity.World.ClassRegistry.CreateEntity(projectileType);
			projectile.FiredBy = entity;
			projectile.ProjectileStack = new ItemStack(entity.World.GetItem(ammoLocation));
			projectile.DropOnImpactChance = infiniteAmmo ? 0 : 1f - (entity.AmmoItemSlot.Itemstack.ItemAttributes["breakChanceOnImpact"].AsFloat(0.5f));
			projectile.World = entity.World;
			// Get damage according to type of projectile.	
			if (isModdedAmmo) {
				projectile.Damage = AmmunitionDamages[entity.gearInv[18].Itemstack.Item.Code.ToString()] * WeaponMultipliers[entity.gearInv[16].Itemstack.Item.Code.ToString()];
			} else {
				projectile.Damage = entity.gearInv[18].Itemstack.Collectible.Attributes["damage"].AsFloat() + entity.gearInv[16].Itemstack.Collectible.Attributes["damage"].AsFloat();
			}
			// Adjust projectile position and velocity to be used.
			Vec3d pos = entity.ServerPos.AheadCopy(0.5).XYZ.AddCopy(0, entity.LocalEyePos.Y, 0);
			Vec3d aheadPos = targetEntity.ServerPos.XYZ.AddCopy(0, targetEntity.LocalEyePos.Y, 0);
			double distf = Math.Pow(pos.SquareDistanceTo(aheadPos), 0.1);
			double curve = pos.DistanceTo(aheadPos) / 16;
			double speed = isModdedAmmo ? WeaponProperties[entity.cachedData.weapCodes].ammoSpeed : GameMath.Clamp(distf - 1f, 0.1f, 1f);
			Vec3d velocity = (aheadPos - pos + new Vec3d(0, curve, 0)).Normalize() * speed;
			// Set final projectile parameters, position, velocity, from point, and rotation.
			projectile.ServerPos.SetPos(entity.ServerPos.AheadCopy(0.5).XYZ.Add(0, entity.LocalEyePos.Y, 0));
			projectile.ServerPos.Motion.Set(velocity);
			projectile.Pos.SetFrom(projectile.ServerPos);
			projectile.SetRotation();
			// Spawn and fire the entity with given parameters.
			entity.RightHandItemSlot.Itemstack.Attributes.SetInt("renderVariant", 0);
			entity.World.SpawnEntity(projectile);
			if (!infiniteAmmo) {
				entity.GearInventory[18]?.TakeOut(1);
				entity.GearInventory[18]?.MarkDirty();
				entity.cachedData.UpdateReloads(entity.GearInventory);
			}
			return true;
		}

		private bool IsAnEnemy(Entity target) {
			if (banditPilled) {
				return entity.cachedData.kingdomGUID != target.WatchedAttributes.GetString("kingdomGUID");
			}
			if (target is EntitySentry sentry) {
				return entity.cachedData.enemiesLIST.Contains(sentry.cachedData.kingdomGUID) || sentry.cachedData.kingdomGUID == banditryGUID;
			}
			if (target is EntityPlayer player) {
				return entity.cachedData.outlawsLIST.Contains(player.PlayerUID) || entity.cachedData.enemiesLIST.Contains(player.WatchedAttributes.GetString("kingdomGUID"));
			}
			return entity.cachedData.enemiesLIST.Contains(target.WatchedAttributes.GetString("kingdomGUID"));
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

		private bool EntityInTheWay() {
			EntitySelection entitySel = new EntitySelection();
			BlockSelection blockSel = new BlockSelection();
			EntityFilter eFilter = (e) => (e.IsInteractable || e.EntityId != entity.EntityId || e.EntityId != targetEntity.EntityId);
			// Do a line Trace into the target, see if there are any entities in the way.
			entity.World.RayTraceForSelection(entity.ServerPos.XYZ.AddCopy(entity.LocalEyePos), targetEntity?.ServerPos?.XYZ.AddCopy(targetEntity?.LocalEyePos), ref blockSel, ref entitySel, null, eFilter);
			// Make sure the target isn't obstructed by other entities, but if it IS then make sure it's okay to hit them.
			if (entitySel?.Entity != null) {
				return !IsTargetableEntity(entitySel?.Entity, (float)entity.ServerPos.DistanceTo(entitySel.Position));
			}
			return false;
		}
	}
}