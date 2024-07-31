using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VSKingdom {
	public class EntityBehaviorLoyalties : EntityBehavior {
		public EntityBehaviorLoyalties(Entity entity) : base(entity) { }

		public ITreeAttribute loyalties {
			get {
				return entity.WatchedAttributes.GetOrAddTreeAttribute("loyalties");
			}
			set {
				entity.WatchedAttributes.SetAttribute("loyalties", value);
				entity.WatchedAttributes.MarkPathDirty("loyalties");
			}
		}

		public EnlistedStatus recruitTYPE {
			get => Enum.Parse<EnlistedStatus>(loyalties.GetString("recruit_type"));
			set => loyalties.SetString("recruit_type", value.ToString());
		}

		public string kingdomGUID {
			get => loyalties.GetString("kingdom_guid");
			set => loyalties.SetString("kingdom_guid", value);
		}

		public string cultureGUID {
			get => loyalties.GetString("culture_guid");
			set => loyalties.SetString("culture_guid", value);
		}

		public string leadersGUID {
			get => loyalties.GetString("leaders_guid");
			set => loyalties.SetString("leaders_guid", value);
		}

		public double outpostSIZE {
			get => loyalties.GetDouble("outpost_size");
			set => loyalties.SetDouble("outpost_size", value);
		}

		public BlockPos outpostXYZD {
			get => loyalties.GetBlockPos("outpost_xyzd");
			set => loyalties.SetBlockPos("outpost_xyzd", value);
		}

		public bool commandWANDER {
			get => loyalties.GetBool("command_wander");
			set => loyalties.SetBool("command_wander", value);
		}

		public bool commandFOLLOW {
			get => loyalties.GetBool("command_follow");
			set => loyalties.SetBool("command_follow", value);
		}

		public bool commandFIRING {
			get => loyalties.GetBool("command_firing");
			set => loyalties.SetBool("command_firing", value);
		}

		public bool commandPURSUE {
			get => loyalties.GetBool("command_pursue");
			set => loyalties.SetBool("command_pursue", value);
		}

		public bool commandSHIFTS {
			get => loyalties.GetBool("command_shifts");
			set => loyalties.SetBool("command_shifts", value);
		}

		public bool commandNIGHTS {
			get => loyalties.GetBool("command_nights");
			set => loyalties.SetBool("command_nights", value);
		}

		public bool commandRETURN {
			get => loyalties.GetBool("command_return");
			set => loyalties.SetBool("command_return", value);
		}

		public override string PropertyName() {
			return "KingdomLoyalties";
		}

		public override void AfterInitialized(bool onFirstSpawn) {
			base.AfterInitialized(onFirstSpawn);
			if (entity is EntityPlayer && onFirstSpawn) {
				if (kingdomGUID is null) {
					kingdomGUID = "00000000";
				}
				if (cultureGUID is null) {
					cultureGUID = "00000000";
				}
				loyalties.RemoveAttribute("leaders_guid");
			}
			if (entity is EntitySentry sentry) {
				if (kingdomGUID is null) {
					kingdomGUID = sentry.baseGroup;
				}
				if (cultureGUID is null) {
					cultureGUID = "00000000";
				}
				if (outpostXYZD is null) {
					outpostXYZD = entity.ServerPos.AsBlockPos;
				}
				if (onFirstSpawn) {
					commandWANDER = true;
					commandFOLLOW = false;
					commandFIRING = true;
					commandPURSUE = true;
					commandSHIFTS = false;
					commandNIGHTS = false;
					commandRETURN = false;
				}
				sentry.Loyalties = loyalties;
				sentry.ruleOrder = new bool[7] { commandWANDER, commandFOLLOW, commandFIRING, commandPURSUE, commandSHIFTS, commandNIGHTS, commandRETURN };
				sentry.kingdomID = kingdomGUID ?? sentry.baseGroup;
				sentry.cultureID = cultureGUID ?? "00000000";
				sentry.leadersID = leadersGUID ?? null;
				if (sentry.baseGroup == "xxxxxxxx") {
					loyalties.SetString("kingdom_guid", "xxxxxxxx");
					loyalties.SetString("leaders_guid", null);
					sentry.WatchedAttributes.MarkPathDirty("loyalties");
					sentry.kingdomID = "xxxxxxxx";
					sentry.leadersID = null;
				}
			}
		}
	}
}