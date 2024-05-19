using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VSKingdom {
	public class KingdomData {
		private static string command_create = LangUtility.Get("command-success-create");
		private static string command_delete = LangUtility.Get("command-success-delete");
		private static string command_invite = LangUtility.Get("command-success-invite");
		private static string command_become = LangUtility.Get("command-success-become");
		private static string command_depart = LangUtility.Get("command-success-depart");
		private static string command_rename = LangUtility.Get("command-success-rename");
		private static string command_prefix = LangUtility.Get("command-success-prefix");
		private static string command_suffix = LangUtility.Get("command-success-suffix");

		public Kingdom kingdom;

		public Dictionary<Priviliges, ClassPrivileges> classright = new Dictionary<Priviliges, ClassPrivileges>();
		public List<IServerPlayer> JoinedPlayers = new List<IServerPlayer>();
		public List<IServerPlayer> OnlinePlayers = new List<IServerPlayer>();
		public List<IServerPlayer> WantedPlayers = new List<IServerPlayer>();
		public List<Entity> AliveEntities = new List<Entity>();
		public List<Entity> EnemyEntities = new List<Entity>();
		public List<Kingdom> EnemyKingdoms = new List<Kingdom>();

		public void PlayerJoin(IServerPlayer player) {
			JoinedPlayers.Add(player);
			OnlinePlayers.Add(player);
			AliveEntities.Add(player.Entity);
		}

		private void GiveRights(Priviliges classRole, string right) {
			switch (right) {
				case "ownLand": classright[classRole].ownLand = true; return;
				case "ownArms": classright[classRole].ownArms = true; return;
				case "canVote": classright[classRole].canVote = true; return;
				case "canLock": classright[classRole].canLock = true; return;
				case "canKill": classright[classRole].canKill = true; return;
				default: return;
			}
		}

		private void TakeRights(Priviliges classRole, string right) {
			switch (right) {
				case "ownLand": classright[classRole].ownLand = false; return;
				case "ownArms": classright[classRole].ownArms = false; return;
				case "canVote": classright[classRole].canVote = false; return;
				case "canLock": classright[classRole].canLock = false; return;
				case "canKill": classright[classRole].canKill = false; return;
				default: return;
			}
		}

		private void VoteElection(Kingdom kingdom) {
			if (kingdom.PlayerUIDs.Count <= 1) {
				return;
			}
			// TODO: Implement elections.
		}

		private bool CanStartVote(Kingdom kingdom, IPlayer caller) {
			Priviliges role = kingdom.PlayerRoles[caller.PlayerUID];

			if (!kingdom.classright[role].canVote) {
				return false;
			}
			if (kingdom.governance != Governance.REPUBLIC) {
				if (kingdom.LeadersUID == caller.PlayerUID) {
					return true;
				}
				return kingdom.classright[role].canVote;
			}
			if (kingdom.governance == Governance.REPUBLIC) {
				return true;
			}
			return false;
		}

		private bool RequiresVote(Kingdom kingdom, IPlayer caller) {
			if (caller.Role.Code == "admin") {
				return false;
			}
			if (kingdom.governance != Governance.REPUBLIC && kingdom.LeadersUID == caller.PlayerUID) {
				return false;
			}
			if (kingdom.PlayerUIDs.Count == 1) {
				return false;
			}
			return true;
		}

		private bool IsDemocratic(Kingdom kingdom) {
			return kingdom.governance == Governance.REPUBLIC;
		}
	}
}
