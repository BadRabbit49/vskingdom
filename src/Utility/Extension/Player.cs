using Vintagestory.API.Common;

namespace VSKingdom.Extension {
	internal static class PlayerExtension {
		public static IPlayer GetClosest(this IPlayer player, IPlayer[] players) {
			IPlayer closest = players[0];
			double bestDist = player.Entity.ServerPos.SquareDistanceTo(players[0].Entity.ServerPos);
			for (int i = 0; i < players.Length; i++) {
				if (player.Entity.ServerPos.SquareDistanceTo(players[i].Entity.ServerPos) < bestDist) {
					closest = players[i];
					bestDist = player.Entity.ServerPos.SquareDistanceTo(players[i].Entity.ServerPos);
				}
			}
			return closest;
		}
	}
}