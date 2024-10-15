using System;
using System.Linq;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using VSKingdom.Utilities;
using Vintagestory.API.Common.Entities;

namespace VSKingdom {
	public class AiTaskSentryFollow : AiTaskStayCloseToGuardedEntity {
		public AiTaskSentryFollow(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		public EntityPlayer targetEntity;
		#pragma warning restore CS0108
		protected bool isAPassenger;
		protected Int32 stuckCounter;
		protected Int64 castCooldown;
		protected EntityBoatSeat boatSeatsEnt;
		protected float  followsRange { get => entity.WatchedAttributes.GetFloat("followRange", 2f); }
		protected Vec3d curTargetPos { get => pathTraverser.CurrentTarget.Clone(); }
		protected long[] getFollowers { get => (targetEntity.WatchedAttributes.GetAttribute("followerEntityUids") as LongArrayAttribute)?.value; }

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			allowTeleport &= entity.Api.World.Config.GetAsBool(Teleporting);
		}

		public override bool ShouldExecute() {
			if (!entity.ruleOrder[1]) {
				return false;
			}
			return base.ShouldExecute();
		}

		public override void StartExecute() {
			RetargetFormation();
			stuck = false;
			isAPassenger = false;
			stuckCounter = 0;
			// Overridden base method to avoid constant teleporting when stuck.
			if (allowTeleport && entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos.X + targetOffset.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z + targetOffset.Z) > teleportAfterRange * teleportAfterRange) {
				tryTeleport();
			}
		}

		public override bool ContinueExecute(float dt) {
			if (!entity.ruleOrder[1]) {
				return false;
			}
			if (isAPassenger) {
				if (targetEntity.WatchedAttributes["mountedOn"] == null || boatSeatsEnt == null) {
					isAPassenger = !entity.TryUnmount();
					boatSeatsEnt = null;
					entity.passenger = new bool[2] { isAPassenger, false };
				} else {
					// REPLACE THIS IF TRAVERSER IS WORKING!
					entity.TeleportTo(boatSeatsEnt.MountPosition);
				}
				return isAPassenger;
			}
			if (stuckCounter > 9) {
				return false;
			}
			if (entity.World.ElapsedMilliseconds > castCooldown) {
				castCooldown = entity.World.ElapsedMilliseconds + 4000;
				RetargetFormation();
			}
			entity.Controls.Sprint = targetEntity.Controls.Sprint;
			entity.Controls.Sneak = targetEntity.Controls.Sneak;
			double x = targetEntity.ServerPos.X + (targetOffset.X * 2);
			double y = targetEntity.ServerPos.Y;
			double z = targetEntity.ServerPos.Z + (targetOffset.Z * 2);
			pathTraverser.CurrentTarget.X = x;
			pathTraverser.CurrentTarget.Y = y;
			pathTraverser.CurrentTarget.Z = z;
			float num = entity.ServerPos.SquareDistanceTo(x, y, z);
			if (!isAPassenger && targetEntity.WatchedAttributes["mountedOn"] != null && entity.WatchedAttributes["mountedOn"] == null && targetEntity is EntityAgent player) {
				if (player.MountedOn.MountSupplier != null && player.MountedOn.MountSupplier is EntityBoat boat && boat.Seats.Length > 1) {
					foreach (var seat in boat.Seats) {
						if (seat.Passenger is null && entity.TryMount(seat)) {
							seat.Passenger = entity;
							seat.PassengerEntityIdForInit = entity.EntityId;
							boatSeatsEnt = seat;
							entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = seat.SuggestedAnimation, Code = seat.SuggestedAnimation, MulWithWalkSpeed = true, BlendMode = EnumAnimationBlendMode.Average, EaseInSpeed = 999f, EaseOutSpeed = 1f }.Init());
							isAPassenger = true;
							pathTraverser.Stop();
							return true;
						}
					}
				}
			}
			if (allowTeleport && num > teleportAfterRange * teleportAfterRange && entity.World.Rand.NextDouble() < 0.05) {
				tryTeleport();
			}
			if (!stuck) {
				pathTraverser.NavigateTo_Async(targetEntity.ServerPos.XYZ, 0.02f, 0.5f, OnGoals, IsStuck, NoPaths);
				return pathTraverser.Active;
			}
			return false;
		}

		public override void FinishExecute(bool cancelled) {
			base.FinishExecute(cancelled);
			if (!entity.ruleOrder[1]) {
				List<long> followers = getFollowers.ToList();
				followers.Remove(entity.EntityId);
				targetEntity.WatchedAttributes.SetAttribute("followerEntityUids", new LongArrayAttribute(followers.ToArray()));
				entity.WatchedAttributes.SetBool(OrderFollow, false);
				entity.WatchedAttributes.SetString("guardedPlayerUid", "");
				pathTraverser.Stop();
			}
			if (isAPassenger && (entity.WatchedAttributes["mountedOn"] == null || boatSeatsEnt == null)) {
				isAPassenger = !entity.TryUnmount();
				boatSeatsEnt = null;
			}
		}

		public virtual void RetargetFormation() {
			try { targetEntity = GetGuardedPlayer(); } catch { return; }
			if (!targetEntity.WatchedAttributes.HasAttribute("followerEntityUids")) {
				targetEntity.WatchedAttributes.SetAttribute("followerEntityUids", new LongArrayAttribute(new long[] { entity.EntityId }));
			}
			List<long> followers = getFollowers.ToList();
			if (!followers.Contains(entity.EntityId)) {
				followers.Add(entity.EntityId);
				targetEntity.WatchedAttributes.SetAttribute("followerEntityUids", new LongArrayAttribute(followers.ToArray()));
			}
			float distance = targetEntity.OriginCollisionBox.XSize + (entity.OriginCollisionBox.XSize + followers.IndexOf(entity.EntityId)) * 1.5f;
			targetOffset = ParadesUtil.FormsOffset(followers.Count, followers.IndexOf(entity.EntityId), targetEntity.ServerPos.Yaw);
			targetOffset.Mul(distance);
			pathTraverser.NavigateTo_Async(targetEntity.ServerPos.XYZ.AddCopy(targetOffset), 0.04f, 0.5f, OnGoals, IsStuck, NoPaths);
		}

		protected void OnGoals() {
			stuck = false;
			stuckCounter = 0;
		}

		protected void IsStuck() {
			stuck = true;
			stuckCounter++;
			if (stuckCounter > 9) {
				pathTraverser.Retarget();
			}
		}
		
		protected void NoPaths() {
			if (isAPassenger) {
				return;
			}
			if (CantSeeTarget() && entity.ServerPos.DistanceTo(targetEntity.ServerPos.XYZ) > followsRange) {
				pathTraverser.Retarget();
			}
			pathTraverser.WalkTowards(entity.ServerPos.XYZ, 0.04f, 0.1f, OnGoals, OnStuck);
		}

		private bool CantSeeTarget() {
			EntitySelection eSelect = new EntitySelection();
			BlockSelection bSelect = new BlockSelection();
			BlockFilter bFilter = (pos, block) => (block == null || block.Replaceable > 6000);
			entity.World.RayTraceForSelection(entity.ServerPos.XYZ.AddCopy(0, 0.5, 0), pathTraverser.CurrentTarget.AddCopy(0, 0.5, 0), ref bSelect, ref eSelect, bFilter);
			if (bSelect == null) {
				return false;
			}
			return bSelect?.Block != null;
		}

		private EntityPlayer GetGuardedPlayer() {
			var playerID = entity.WatchedAttributes.GetString("guardedPlayerUid");
			if (playerID != null) {
				return entity.World.PlayerByUid(playerID)?.Entity;
			}
			var entityID = entity.WatchedAttributes.GetLong("guardedEntityId", 0L);
			return entity.World.GetEntityById(entityID) as EntityPlayer;
		}
	}
}