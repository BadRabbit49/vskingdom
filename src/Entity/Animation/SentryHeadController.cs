using System;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;

namespace VSKingdom {
	public class SentryHeadController : EntityHeadController {
		#pragma warning disable CS0108
		public EntityAgent entity;
		public EntityAgent target;
		#pragma warning restore CS0108
		protected bool turnOpposite;
		protected bool rotateTpYawNow;
		protected float curTurnAngle;

		public SentryHeadController(IAnimationManager animator, EntityAgent entity, Shape shape) : base(animator, entity, shape) {
			this.entity = entity;
			this.animManager = animator;
			this.HeadPose = animator.Animator.GetPosebyName("Head");
			this.NeckPose = animator.Animator.GetPosebyName("Neck");
			this.UpperTorsoPose = animator.Animator.GetPosebyName("UpperTorso");
			this.LowerTorsoPose = animator.Animator.GetPosebyName("LowerTorso");
			this.UpperFootRPose = animator.Animator.GetPosebyName("UpperFootR");
			this.UpperFootLPose = animator.Animator.GetPosebyName("UpperFootL");
		}

		public override void OnFrame(float dt) {
			if (target != null) {
				Vec3d ownPos = entity.Pos.XYZ.AddCopy(entity.LocalEyePos);
				Vec3d hisPos = target.Pos.XYZ.AddCopy(target.LocalEyePos);
				float yawAngle = GameMath.AngleRadDistance(entity.Pos.HeadYaw, (float)Math.Atan2(hisPos.X - ownPos.X, hisPos.Z - ownPos.Z));
				float pitAngle = GameMath.AngleRadDistance(entity.Pos.HeadPitch, (float)Math.Atan2(hisPos.X - ownPos.X, hisPos.Z - ownPos.Z));
				base.entity.Pos.HeadYaw += GameMath.Clamp(yawAngle, (0f - curTurnAngle) * dt, curTurnAngle * dt);
				base.entity.Pos.HeadYaw %= (MathF.PI * 2f);
			}
			base.OnFrame(dt);
		}
	}
}