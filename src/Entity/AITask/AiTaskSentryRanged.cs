﻿using Vintagestory.GameContent;
using Vintagestory.API.Util;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using System;
using System.Collections.Generic;
using Vintagestory.API.Config;

namespace VSKingdom {
	public class AiTaskSentryRanged : AiTaskBaseTargetable {
		public AiTaskSentryRanged(EntityAgent entity) : base(entity) { }

		protected bool animsStarted = false;
		protected bool cancelAttack = false;
		protected bool didRenderSwitch = false;
		protected bool projectileFired = false;
		protected int durationOfMs = 1200;
		protected int releasesAtMs = 1000;
		protected long totalDurationMs;
		protected long totalCooldownMs = 1000L;
		protected float maxDist;
		protected float minDist;
		protected float moveSpeed = 0.035f;
		protected float accum = 0;
		protected float minTurnAnglePerSec;
		protected float maxTurnAnglePerSec;
		protected float curTurnAnglePerSec;
		
		protected AnimationMetaData drawBowsMeta;
		protected AnimationMetaData fireBowsMeta;
		protected AnimationMetaData loadBowsMeta;
		protected EntityProperties projectileType;
		protected AssetLocation drawingsound = null;
		protected AssetLocation hittingsound = null;
		protected AssetLocation ammoLocation = null;

		private ITreeAttribute loyalties;
		private string entKingdom => loyalties?.GetString("kingdom_guid");
		
		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			maxDist = taskConfig["maxDist"].AsFloat(20f);
			minDist = taskConfig["minDist"].AsFloat(10f);
			moveSpeed = taskConfig["movespeed"].AsFloat(0.035f);
			drawBowsMeta = new AnimationMetaData() {
				Code = "bowdraw",
				Animation = "bowdraw",
			}.Init();
			fireBowsMeta = new AnimationMetaData() {
				Code = "bowfire",
				Animation = "bowfire",
			}.Init();
			loadBowsMeta = new AnimationMetaData() {
				Code = "bowload",
				Animation = "bowload",
			}.Init();
		}

		public override void AfterInitialize() {
			base.AfterInitialize();
			// We are using loyalties attribute tree to get our kingdomGUID.
			loyalties = entity.WatchedAttributes.GetTreeAttribute("loyalties");
			pathTraverser = entity.GetBehavior<EntityBehaviorTaskAI>().PathTraverser;
		}

		public override bool ShouldExecute() {
			if (cooldownUntilMs > entity.World.ElapsedMilliseconds || !HasRanged()) {
				return false;
			}
			if (totalDurationMs + totalCooldownMs < entity.World.ElapsedMilliseconds) {
				Entity target = targetEntity;
				if (target is null || !target.Alive) {
					goto IL_0095;
				}
			}
			if (totalDurationMs + totalCooldownMs * 5 < entity.World.ElapsedMilliseconds) {
				goto IL_0095;
			}
			goto IL_00d6;
		IL_0095:
			totalDurationMs = entity.World.ElapsedMilliseconds;
			targetEntity = partitionUtil.GetNearestInteractableEntity(entity.ServerPos.XYZ, maxDist * 3f, (Entity ent) => IsTargetableEntity(ent, maxDist * 4f) && hasDirectContact(ent, maxDist * 4f, minDist));
			goto IL_00d6;
		IL_00d6:
			return targetEntity?.Alive ?? false;
		}

		public override bool IsTargetableEntity(Entity ent, float range, bool ignoreEntityCode = false) {
			if (ent == entity) {
				return false;
			}
			if (!ent.Alive || ent is null) {
				return false;
			}
			if (ent is EntityProjectile projectile) {
				targetEntity = projectile.FiredBy;
			}
			if (ent is EntityHumanoid) {
				return DataUtility.IsAnEnemy(entKingdom, ent);
			}
			if (ignoreEntityCode) {
				return CanSense(ent, range);
			}
			if (IsTargetEntity(ent.Code.Path)) {
				return CanSense(ent, range);
			}
			return false;
		}

		public override void StartExecute() {
			accum = 0;
			animsStarted = false;
			cancelAttack = false;
			didRenderSwitch = false;
			projectileFired = false;
			// Get and initialize the item's attributes to the weapon.
			drawingsound = ItemsProperties.wepnAimAudio.Get(entity.RightHandItemSlot?.Itemstack?.Collectible?.Code);
			List<AssetLocation> hitAudio = ItemsProperties.wepnHitAudio.Get(entity.RightHandItemSlot?.Itemstack?.Collectible?.Code);
			Random rnd = new Random();
			hittingsound = hitAudio[rnd.Next(0, hitAudio.Count - 1)];
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
			entity.ServerControls.IsAiming = true;
		}

		public override bool ContinueExecute(float dt) {
			// Can't shoot if there is no target, the attack has been cancelled, the shooter is swimming, or the target is too close!
			if (cancelAttack || targetEntity is null || !targetEntity.Alive || entity.Swimming) {
				return false;
			}
			// Retreat if target is too close!
			if (entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos.XYZ) <= 4 * 4) {
				Retreat(false);
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
				entity.AnimManager.StartAnimation(drawBowsMeta);
				if (drawingsound != null) {
					entity.World.PlaySoundAt(drawingsound, entity, null, false);
				}
			}

			accum += dt;

			// Draw back the weapon to its render variant if it has one. 
			if (!didRenderSwitch && accum > durationOfMs / 2000f) {
				entity.RightHandItemSlot?.Itemstack?.Attributes?.SetInt("renderVariant", 3);
				entity.RightHandItemSlot.MarkDirty();
				didRenderSwitch = true;
			}
			// Do after aiming time is finished.
			if (accum > releasesAtMs / 1000f && !projectileFired && !EntityInTheWay()) {
				FireProjectile();
				// Don't play anything when the hittingSound is incorrectly set.
				if (hittingsound != null) {
					entity.World.PlaySoundAt(hittingsound, entity, null, false);
				}
			}
			return accum < (float)durationOfMs / 1000f && !cancelAttack;
		}

		public override void FinishExecute(bool cancelled) {
			base.FinishExecute(cancelled);
			entity.RightHandItemSlot?.Itemstack?.Attributes?.SetInt("renderVariant", 0);
			entity.RightHandItemSlot?.MarkDirty();
			entity.CurrentControls = EnumEntityActivity.Idle;
			entity.StopAnimation(drawBowsMeta.Code);
			entity.ServerControls.IsAiming = false;
		}
		
		public override void OnEntityHurt(DamageSource source, float damage) {
			base.OnEntityHurt(source, damage);
			// Interrupt attack and flee.
			cancelAttack = true;
			FinishExecute(true);
			Retreat(true);
		}

		public void OnAllyAttacked(Entity byEntity) {
			if (byEntity != entity) {
				if (targetEntity is null || !targetEntity.Alive) {
					targetEntity = byEntity;
				}
			}
			ShouldExecute();
		}

		private void FireProjectile() {
			EntityProjectile projectile = (EntityProjectile)entity.World.ClassRegistry.CreateEntity(projectileType);
			projectile.FiredBy = entity;
			projectile.Damage = GetDamage();
			projectile.ProjectileStack = new ItemStack(entity.World.GetItem(ammoLocation));
			// We don't want unfair duplicates of ammo if infinite ammo is on.
			if (entity.Api.World.Config.GetAsBool("InfiniteAmmo")) {
				projectile.DropOnImpactChance = 0;
			} else if (entity.GearInventory[18].Itemstack.ItemAttributes != null) {
				projectile.DropOnImpactChance = 1f - (entity.GearInventory[18].Itemstack.ItemAttributes["breakChanceOnImpact"].AsFloat(0.5f));
			}
			projectile.World = entity.World;
			Vec3d pos = entity.ServerPos.AheadCopy(0.5).XYZ.AddCopy(0, entity.LocalEyePos.Y, 0);
			Vec3d aheadPos = targetEntity.ServerPos.XYZ.AddCopy(0, targetEntity.LocalEyePos.Y, 0);
			double distf = Math.Pow(pos.SquareDistanceTo(aheadPos), 0.1);
			Vec3d velocity = (aheadPos - pos + new Vec3d(0, pos.DistanceTo(aheadPos) / 16, 0)).Normalize() * GameMath.Clamp(distf - 1f, 0.1f, 1f);
			// Set final projectile parameters, position, velocity, from point, and rotation.
			projectile.ServerPos.SetPos(entity.ServerPos.AheadCopy(0.5).XYZ.Add(0, entity.LocalEyePos.Y, 0));
			projectile.ServerPos.Motion.Set(velocity);
			projectile.Pos.SetFrom(projectile.ServerPos);
			projectile.SetRotation();
			// Spawn and fire the entity with given parameters.
			entity.RightHandItemSlot?.Itemstack?.Attributes?.SetInt("renderVariant", 0);
			entity.World.SpawnEntity(projectile);
			projectileFired = true;
			if (!entity.Api.World.Config.GetAsBool("InfiniteAmmo")) {
				entity.GearInventory[18].TakeOut(1);
				entity.GearInventory[18].MarkDirty();
			}
		}
		
		private void OnStuck() {
			updateTargetPosFleeMode(entity.Pos.XYZ);
		}

		private void OnGoalReached() {
			entity.CurrentControls = EnumEntityActivity.Idle;
			entity.Controls.Backward = false;
			entity.ServerControls.Backward = false;
			entity.Controls.Forward = true;
			entity.ServerControls.Forward = true;
			pathTraverser.Retarget();
		}

		private void Retreat(bool full) {
			Vec3d targetPos = new Vec3d();
			updateTargetPosFleeMode(targetPos);
			entity.ServerControls.Sprint = full;
			entity.ServerControls.Forward = full;
			entity.ServerControls.Backward = !full;
			if (full) {
				entity.CurrentControls = EnumEntityActivity.SprintMode;
				pathTraverser.WalkTowards(targetPos, moveSpeed * (float)GlobalConstants.SprintSpeedMultiplier, targetEntity.SelectionBox.XSize + 0.2f, OnGoalReached, OnStuck);
			} else {
				entity.CurrentControls = EnumEntityActivity.Move;
				pathTraverser.WalkTowards(targetPos, moveSpeed, targetEntity.SelectionBox.XSize + 0.2f, OnGoalReached, OnStuck);
			}
			pathTraverser.CurrentTarget.X = targetPos.X;
			pathTraverser.CurrentTarget.Y = targetPos.Y;
			pathTraverser.CurrentTarget.Z = targetPos.Z;
			pathTraverser.Retarget();
		}

		private float GetDamage() {
			if (HasRanged()) {
				float dmg1 = 0f;
				float dmg2 = 0f;
				if (entity.RightHandItemSlot.Itemstack.Collectible.Attributes != null) {
					dmg1 += entity.RightHandItemSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
				}
				if (entity.GearInventory[18].Itemstack.Collectible.Attributes != null) {
					dmg2 = entity.GearInventory[18].Itemstack.Collectible.Attributes["damage"].AsFloat(0);
				}
				return dmg1 + dmg2;
			} else {
				return 2f;
			}
		}

		private bool HasRanged() {
			if (entity.GearInventory[18].Empty || entity.RightHandItemSlot.Empty) {
				return false;
			}
			if (entity.RightHandItemSlot.Itemstack.Item is ItemBow) {
				return entity.GearInventory[18].Itemstack.Item is ItemArrow || entity.GearInventory[18].Itemstack.Collectible.Code.PathStartsWith("arrow-");
			}
			if (entity.RightHandItemSlot.Itemstack.Item is ItemSling) {
				return entity.GearInventory[18].Itemstack.Item is ItemStone || entity.GearInventory[18].Itemstack.Collectible.Code.PathStartsWith("thrownstone-");
			}
			return false;
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
			// Do a line Trace into the target, see if there are any entities in the way.
			entity.World.RayTraceForSelection(entity.ServerPos.XYZ.AddCopy(entity.LocalEyePos), targetEntity?.ServerPos?.XYZ.AddCopy(targetEntity?.LocalEyePos), ref blockSel, ref entitySel);
			// Make sure the target isn't obstructed by other entities, but if it IS then make sure it's okay to hit them.
			if (entitySel?.Entity != entity && entitySel?.Entity != targetEntity) {
				// Fuck all drifters, locusts, and bells, a shot well placed I say. Infact, switch targets to kill IT.
				if (entitySel?.Entity is EntityDrifter || entitySel?.Entity is EntityLocust || entitySel?.Entity is EntityBell) {
					targetEntity = entitySel.Entity;
					return false;
				}
				// Determine if the entity in the way is a friend or foe, if they're an enemy then disregard and shoot anyway.
				if (entitySel?.Entity is EntityHumanoid) {
					return !DataUtility.IsAnEnemy(entKingdom, entitySel.Entity);
				}
				return true;
			}
			return false;
		}
	}
}