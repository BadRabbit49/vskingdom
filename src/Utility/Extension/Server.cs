using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VSKingdom.Extension {
	internal static class ServerExtension {
		public static IServerPlayer GetAPlayer(this ICoreServerAPI sapi, string playersNAME) {
			return sapi.World.AllPlayers.ToList<IPlayer>().Find(playerMatch => playerMatch.PlayerName == playersNAME) as IServerPlayer ?? null;
		}
	}
}