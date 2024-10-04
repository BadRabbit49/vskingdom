using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using VSKingdom.Extension;
using VSKingdom.Utilities;

namespace VSKingdom {
	public class AiTaskSentryFollow : AiTaskStayCloseToGuardedEntity {
		public AiTaskSentryFollow(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		protected bool isAPassenger;
		protected long stuckCounter;
		protected long castCooldown;
		protected float prvMoveSpeed;
		protected float curMoveSpeed;
		protected string curAnimation;
		/** protected EntityRideableSeat rideSeatsEnt; (for version 1.20.0) **/
		protected EntityBoatSeat boatSeatsEnt;
		protected float  followsRange { get => entity.WatchedAttributes.GetFloat("followRange", 2f); }
		protected Vec3d curTargetPos { get => pathTraverser.CurrentTarget.Clone(); }
		protected long[] getFollowers { get => (targetEntity.WatchedAttributes.GetAttribute("followerEntityUids") as LongArrayAttribute)?.value; }

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			this.curMoveSpeed = taskConfig["curMoveSpeed"].AsFloat(0.03f);
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

		public override bool CanContinueExecute() {
			return pathTraverser.Ready && entity.ruleOrder[1];
		}

		public override bool ContinueExecute(float dt) {
			if (isAPassenger) {
				if (targetEntity.WatchedAttributes["mountedOn"] == null || boatSeatsEnt == null) {
					isAPassenger = !entity.TryUnmount();
					boatSeatsEnt = null;
					return false;
				}
				entity.TeleportTo(boatSeatsEnt.MountPosition);
				return true;
			}
			if (stuckCounter > 9) {
				return false;
			}
			if (entity.World.ElapsedMilliseconds > castCooldown) {
				castCooldown = entity.World.ElapsedMilliseconds + 4000;
				RetargetFormation();
			}
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
							StopAnimation();
							curAnimation = new string(seat.SuggestedAnimation);
							entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = curAnimation, Code = curAnimation, MulWithWalkSpeed = true, BlendMode = EnumAnimationBlendMode.Average, EaseInSpeed = 999f, EaseOutSpeed = 1f }.Init());
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
				if (prvMoveSpeed != curMoveSpeed) {
					prvMoveSpeed = curMoveSpeed;
					MoveAnimation();
					pathTraverser.Stop();
					pathTraverser.NavigateTo_Async(targetEntity.ServerPos.XYZ, prvMoveSpeed, 0.5f, OnGoals, IsStuck, NoPaths);
				}
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
				entity.WatchedAttributes.SetLong("guardedEntityId", 0);
				pathTraverser.Stop();
			}
			StopAnimation();
		}

		public virtual void RetargetFormation() {
			try { targetEntity = GetGuardedEntity(); } catch { return; }
			if (!targetEntity.WatchedAttributes.HasAttribute("followerEntityUids")) {
				targetEntity.WatchedAttributes.SetAttribute("followerEntityUids", new LongArrayAttribute(new long[] { entity.EntityId }));
			}
			prvMoveSpeed = curMoveSpeed;
			List<long> followers = getFollowers.ToList();
			if (!followers.Contains(entity.EntityId)) {
				followers.Add(entity.EntityId);
				targetEntity.WatchedAttributes.SetAttribute("followerEntityUids", new LongArrayAttribute(followers.ToArray()));
			}
			float distance = targetEntity.OriginCollisionBox.XSize + (entity.OriginCollisionBox.XSize + followers.IndexOf(entity.EntityId)) * 2;
			targetOffset = new Vec3d(0, 0, distance).FormationRotate(ParadesUtil.SnapRadians(targetEntity.ServerPos.Yaw));
			MoveAnimation();
			pathTraverser.NavigateTo_Async(targetEntity.ServerPos.XYZ.AddCopy(targetOffset), prvMoveSpeed, 0.5f, OnGoals, IsStuck, NoPaths);
			//targetOffset = ParadesUtil.FormsOffset(followers.Count, followers.IndexOf(entity.EntityId), targetEntity.ServerPos.Yaw);
			//entity.Api.Logger.Notification($"targetOffset for {entity.EntityId} is [{Math.Round(targetOffset.X, 1)}, {Math.Round(targetOffset.Y, 1)}, {Math.Round(targetOffset.Z, 1)}] their position is {followers.IndexOf(entity.EntityId)}");
			//MoveAnimation();
			//pathTraverser.NavigateTo_Async(targetEntity.ServerPos.XYZ.FormationOffset(targetOffset, ParadesUtil.SnapRadians(targetEntity.ServerPos.Yaw)), prvMoveSpeed, 0.5f, OnGoals, IsStuck, NoPaths);
		}

		protected void OnGoals() {
			stuck = false;
			stuckCounter = 0;
		}

		protected void IsStuck() {
			stuck = true;
			stuckCounter += 1;
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
			BlockPos blocksPos = entity.ServerPos.AsBlockPos.Copy();
			bool laddersAround = false;
			bool needsToGoUpTo = (targetEntity.ServerPos.Y - entity.ServerPos.Y) > 2;
			bool needsToGoDown = (entity.ServerPos.Y - targetEntity.ServerPos.Y) > 5;

			entity.World.BlockAccessor.WalkBlocks(entity.SidedPos.AsBlockPos.AddCopy(-1, -1, -1), entity.SidedPos.AsBlockPos.AddCopy(1, 1, 1), (block, x, y, z) => {
				BlockPos pos = new(x, y, z, entity.SidedPos.Dimension);
				if (!laddersAround && (needsToGoUpTo || needsToGoDown) && block.Climbable) {
					if (!entity.World.BlockAccessor.IsNotTraversable(pos.AddCopy(0, 1, 0))) {
						laddersAround = true;
						if (needsToGoUpTo) {
							int dist = (int)(targetEntity.ServerPos.Y - entity.ServerPos.Y);
							for (int i = 0; i < dist; i++) {
								if (entity.World.BlockAccessor.GetBlock(pos.AddCopy(0, i, 0))?.Climbable ?? false) {
									continue;
								}
								laddersAround = targetEntity.ServerPos.Y - (pos.Y + i) < 2;
								break;
							}
						}
						if (needsToGoDown) {
							int dist = (int)(entity.ServerPos.Y - targetEntity.ServerPos.Y);
							for (int i = 0; i < dist; i++) {
								if (entity.World.BlockAccessor.GetBlock(pos.AddCopy(0, -i, 0))?.Climbable ?? false) {
									continue;
								}
								laddersAround = targetEntity.ServerPos.Y - (pos.Y - i) > -5;
								break;
							}
						}
						if (laddersAround) {
							blocksPos = pos.Copy();
						}
					}
				}
				if ((!needsToGoUpTo && !needsToGoDown) && block is BlockBaseDoor) {
					blocksPos = pos.Copy();
				}
			});
			MoveAnimation();
			pathTraverser.WalkTowards(blocksPos.ToVec3d(), entity.cachedData.moveSpeed, 0.1f, OnGoals, OnStuck);
		}

		private void MoveAnimation() {
			if (!entity.ruleOrder[1] || !pathTraverser.Active) {
				StopAnimation();
				return;
			}
			double distance = entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos.XYZ.AddCopy(targetOffset));
			if (entity.Swimming) {
				curMoveSpeed = entity.cachedData.moveSpeed;
				curAnimation = new string(entity.cachedData.swimAnims);
			} else if (distance > 36f) {
				curMoveSpeed = entity.cachedData.moveSpeed;
				curAnimation = new string(entity.cachedData.moveAnims);
			} else if (distance > targetOffset.HorizontalSquareDistanceTo(Vec3d.Zero)) {
				curMoveSpeed = entity.cachedData.walkSpeed;
				curAnimation = new string(entity.cachedData.walkAnims);
			}
			entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = curAnimation, Code = curAnimation, MulWithWalkSpeed = true, BlendMode = EnumAnimationBlendMode.Average, EaseInSpeed = 999f, EaseOutSpeed = 999f }.Init());
		}

		private void StopAnimation() {
			curMoveSpeed = 0;
			if (curAnimation != null) {
				entity.AnimManager.StopAnimation(curAnimation);
			}
			entity.AnimManager.StopAnimation(entity.cachedData.walkAnims);
			entity.AnimManager.StopAnimation(entity.cachedData.moveAnims);
			entity.AnimManager.StopAnimation(entity.cachedData.swimAnims);
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
	}
}