using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VSKingdom {
	internal static class MathUtility {
		public static IPlayer GetClosest(Vec3d pos, IPlayer[] players) {
			IPlayer closest = players[0];
			double bestDist = pos.SquareDistanceTo(players[0].Entity.ServerPos);
			for (int i = 0; i < players.Length; i++) {
				if (pos.SquareDistanceTo(players[i].Entity.ServerPos) < bestDist) {
					closest = players[i];
					bestDist = pos.SquareDistanceTo(players[i].Entity.ServerPos);
				}
			}
			return closest;
		}

		public static Entity GetClosest(Vec3d pos, Entity[] entities) {
			Entity closest = entities[0];
			double bestDist = pos.SquareDistanceTo(entities[0].ServerPos);
			for (int i = 0; i < entities.Length; i++) {
				if (pos.SquareDistanceTo(entities[i].ServerPos) < bestDist) {
					closest = entities[i];
					bestDist = pos.SquareDistanceTo(entities[i].ServerPos);
				}
			}
			return closest;
		}
	}
}