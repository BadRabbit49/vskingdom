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

		public override string PropertyName() {
			return "KingdomLoyalties";
		}

		public override void AfterInitialized(bool onFirstSpawn) {
			base.AfterInitialized(onFirstSpawn);
			if (entity is EntityPlayer && onFirstSpawn) {
				if (kingdomGUID is null || !loyalties.HasAttribute("kingdom_guid")) {
					kingdomGUID = GlobalCodes.commonerGUID;
				}
				if (cultureGUID is null || !loyalties.HasAttribute("culture_guid")) {
					cultureGUID = GlobalCodes.seraphimGUID;
				}
				loyalties.RemoveAttribute("leaders_guid");
				loyalties.RemoveAttribute("outpost_size");
				loyalties.RemoveAttribute("outpost_xyzd");
				entity.WatchedAttributes.MarkPathDirty("loyalties");
			}
			if (entity is EntitySentry sentry) {
				bool hasCachedData = sentry.cachedData != null;
				if (!loyalties.HasAttribute("kingdom_guid") || kingdomGUID is null) {
					kingdomGUID = sentry.Properties.Attributes["baseSides"].AsString(GlobalCodes.commonerGUID);
				} else if (hasCachedData) {
					sentry.cachedData.kingdomGUID = kingdomGUID;
				}
				if (!loyalties.HasAttribute("culture_guid") || cultureGUID is null) {
					cultureGUID = sentry.Properties.Attributes["baseGroup"].AsString(GlobalCodes.seraphimGUID);
				} else if (hasCachedData) {
					sentry.cachedData.cultureGUID = cultureGUID;
				}
				if (!loyalties.HasAttribute("leaders_guid") || leadersGUID is null) {
					leadersGUID = null;
				} else if (hasCachedData) {
					sentry.cachedData.leadersGUID = leadersGUID;
				}
				if (!loyalties.HasAttribute("outpost_xyzd") || outpostXYZD is null) {
					outpostXYZD = entity.ServerPos.AsBlockPos.Copy();
					outpostSIZE = 3;
				}
				if (kingdomGUID == GlobalCodes.banditryGUID) {
					loyalties.SetString("kingdom_guid", GlobalCodes.banditryGUID);
					loyalties.SetString("leaders_guid", null);
					sentry.WatchedAttributes.MarkPathDirty("loyalties");
					if (hasCachedData) {
						sentry.cachedData.kingdomGUID = GlobalCodes.banditryGUID;
						sentry.cachedData.leadersGUID = null;
					}
				}
				sentry.Loyalties = loyalties;
			}
		}
	}
}