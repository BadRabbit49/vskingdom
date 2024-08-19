using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

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

		public override string PropertyName() {
			return "KingdomLoyalties";
		}

		public override void AfterInitialized(bool onFirstSpawn) {
			base.AfterInitialized(onFirstSpawn);
			if (onFirstSpawn && entity is EntityPlayer) {
				if (kingdomGUID is null || !loyalties.HasAttribute("kingdom_guid")) {
					kingdomGUID = GlobalCodes.commonerGUID;
				}
				if (cultureGUID is null || !loyalties.HasAttribute("culture_guid")) {
					cultureGUID = GlobalCodes.seraphimGUID;
				}
				entity.WatchedAttributes.MarkPathDirty("loyalties");
			}
		}
	}
}