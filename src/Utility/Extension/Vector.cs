using System;
using Vintagestory.API.MathTools;

namespace VSKingdom.Extension {
	internal static class VectorExtension {
		public static Vec3d FormationOffset(this Vec3d vtr, int offsetX, int offsetY, int offsetZ, float yaw) {
			double _x = (vtr.X + (double)offsetX * MathF.Cos(yaw - MathF.PI * 2f));
			double _y = (vtr.Y + (double)offsetY);
			double _z = (vtr.Z + (double)offsetZ * MathF.Sin(yaw - MathF.PI / 2f));
			return new Vec3d(_x, _y, _z);
		}

		public static Vec3d FormationOffset(this Vec3d vtr, Vec3d off, float yaw) {
			double _x = (vtr.X + (double)off.X * MathF.Cos(yaw - MathF.PI * 2f));
			double _y = (vtr.Y + (double)off.Y);
			double _z = (vtr.Z + (double)off.Z * MathF.Sin(yaw - MathF.PI / 2f));
			return new Vec3d(_x, _y, _z);
		}

		public static Vec3d FormationRotate(this Vec3d vtr, float yaw) {
			double _x = (vtr.X * MathF.Cos(yaw - MathF.PI * 2f));
			double _y = (vtr.Y);
			double _z = (vtr.Z * MathF.Sin(yaw - MathF.PI / 2f));
			return new Vec3d(_x, _y, _z);
		}
	}
}