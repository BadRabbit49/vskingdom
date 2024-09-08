using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class AiTaskSentryFollow : AiTaskStayCloseToGuardedEntity {
		public AiTaskSentryFollow(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		protected bool isAPassenger;
		protected float groupsOffset;
		protected float squaredRange;
		protected float prvMoveSpeed;
		protected float curMoveSpeed;
		protected string curAnimation;
		protected EntityBoatSeat boatSeatsEnt;
		protected float followsRange { get => entity.WatchedAttributes.GetFloat("followRange", 2f); }
		protected Vec3d curTargetPos { get => pathTraverser.CurrentTarget.Clone(); }
		
		
		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			this.curMoveSpeed = taskConfig["curMoveSpeed"].AsFloat(0.03f);
			allowTeleport &= entity.Api.World.Config.GetAsBool("AllowTeleport");
		}

		public override bool ShouldExecute() {
			if (!entity.ruleOrder[1]) {
				return false;
			}
			return base.ShouldExecute();
		}

		public override void StartExecute() {
			if (!targetEntity.WatchedAttributes.HasAttribute("followerEntityUids")) {
				targetEntity.WatchedAttributes.SetAttribute("followerEntityUids", new LongArrayAttribute(new long[] { entity.EntityId }));
			}
			targetEntity = GetGuardedEntity();
			groupsOffset = 1;
			long[] followers = (targetEntity.WatchedAttributes.GetAttribute("followerEntityUids") as LongArrayAttribute)?.value;
			for (int i = 0; i < followers.Length; i++) {
				if (entity.EntityId == followers[i]) {
					groupsOffset = i + 1;
					break;
				}
			}
			groupsOffset *= targetEntity.SelectionBox.XSize + followsRange;
			squaredRange = groupsOffset * groupsOffset;
			MoveAnimation();
			prvMoveSpeed = curMoveSpeed;
			bool go = pathTraverser.NavigateTo_Async(targetEntity.ServerPos.XYZ, prvMoveSpeed, groupsOffset, OnGoals, OnStuck, null, 1000, (int)groupsOffset);
			targetOffset.Set(entity.World.Rand.NextDouble() * 2 - 1, 0, entity.World.Rand.NextDouble() * 2 - 1);
			stuck = false;
			isAPassenger = false;
			// Overridden base method to avoid constant teleporting when stuck.
			if (allowTeleport && entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos.X + targetOffset.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z + targetOffset.Z) > teleportAfterRange * teleportAfterRange) {
				tryTeleport();
			}
		}

		public override bool CanContinueExecute() {
			return pathTraverser.Ready && entity.ruleOrder[1];
		}

		public override bool ContinueExecute(float dt) {
			if (targetEntity is null) {
				return false;
			}
			if (isAPassenger) {
				if (targetEntity.WatchedAttributes["mountedOn"] == null || boatSeatsEnt == null) {
					isAPassenger = !entity.TryUnmount();
					boatSeatsEnt = null;
					return false;
				}
				entity.ServerPos = boatSeatsEnt.MountPosition;
				return true;
			}
			double x = targetEntity.ServerPos.X + targetOffset.X;
			double y = targetEntity.ServerPos.Y;
			double z = targetEntity.ServerPos.Z + targetOffset.Z;
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
				MoveAnimation();
				if (prvMoveSpeed != curMoveSpeed) {
					prvMoveSpeed = curMoveSpeed;
					pathTraverser.Stop();
					pathTraverser.NavigateTo_Async(targetEntity.ServerPos.XYZ, prvMoveSpeed, groupsOffset, OnGoals, OnStuck, null, 1000, (int)groupsOffset);
				}
				return pathTraverser.Active;
			}
			return false;
		}

		public override void FinishExecute(bool cancelled) {
			base.FinishExecute(cancelled);
			if (!entity.ruleOrder[1]) {
				long[] followers = (targetEntity.WatchedAttributes.GetAttribute("followerEntityUids") as LongArrayAttribute)?.value;
				targetEntity.WatchedAttributes.SetAttribute("followerEntityUids", new LongArrayAttribute(followers.Remove(entity.EntityId)));
				entity.WatchedAttributes.SetBool("orderFollow", false);
				entity.WatchedAttributes.SetLong("guardedEntityId", 0);
				pathTraverser.Stop();
			}
			StopAnimation();
		}

		protected void IsStuck() {
			stuck = true;
		}

		protected void OnGoals() {
			MoveAnimation();
			pathTraverser.Retarget();
		}

		private void MoveAnimation() {
			if (!entity.ruleOrder[1]) {
				StopAnimation();
				return;
			}
			double distance = entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos);
			entity.AnimManager.StopAnimation(curAnimation);
			if (entity.FeetInLiquid && !entity.Swimming) {
				curMoveSpeed = entity.cachedData.walkSpeed;
				curAnimation = new string(entity.cachedData.walkAnims);
			} else if (entity.Swimming) {
				curMoveSpeed = entity.cachedData.moveSpeed;
				curAnimation = new string(entity.cachedData.swimAnims);
			} else if (distance > 36f) {
				curMoveSpeed = entity.cachedData.moveSpeed;
				curAnimation = new string(entity.cachedData.moveAnims);
			} else if (distance > squaredRange && distance < 36f) {
				curMoveSpeed = entity.cachedData.walkSpeed;
				curAnimation = new string(entity.cachedData.walkAnims);
			} else {
				StopAnimation();
				return;
			}
			if (!entity.AnimManager.IsAnimationActive(curAnimation)) {
				entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = curAnimation, Code = curAnimation, MulWithWalkSpeed = true, BlendMode = EnumAnimationBlendMode.Average, EaseInSpeed = 999f, EaseOutSpeed = 1f }.Init());
			}
		}

		private void StopAnimation() {
			curMoveSpeed = 0;
			if (curAnimation != null) {
				entity.AnimManager.StopAnimation(curAnimation);
			}
			entity.AnimManager.StopAnimation(new string(entity.cachedData.walkAnims));
			entity.AnimManager.StopAnimation(new string(entity.cachedData.moveAnims));
			entity.AnimManager.StopAnimation(new string(entity.cachedData.swimAnims));
		}
	}
}