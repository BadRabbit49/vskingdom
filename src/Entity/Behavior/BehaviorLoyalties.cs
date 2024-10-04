using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VSKingdom {
	public class EntityBehaviorLoyalties : EntityBehavior {
		public EntityBehaviorLoyalties(Entity entity) : base(entity) { }

		public string kingdomGUID {
			get => entity.WatchedAttributes.GetString(KingdomGUID);
			set => entity.WatchedAttributes.SetString(KingdomGUID, value);
		}

		public string cultureGUID {
			get => entity.WatchedAttributes.GetString(CultureGUID);
			set => entity.WatchedAttributes.SetString(CultureGUID, value);
		}

		public string leadersGUID {
			get => entity.WatchedAttributes.GetString(LeadersGUID);
			set => entity.WatchedAttributes.SetString(LeadersGUID, value);
		}

		public override string PropertyName() {
			return "KingdomLoyalties";
		}

		public override void AfterInitialized(bool onFirstSpawn) {
			base.AfterInitialized(onFirstSpawn);
			if (onFirstSpawn && entity is EntityPlayer) {
				if (kingdomGUID is null || !entity.WatchedAttributes.HasAttribute(KingdomGUID)) {
					kingdomGUID = CommonersID;
				}
				if (cultureGUID is null || !entity.WatchedAttributes.HasAttribute(CultureGUID)) {
					cultureGUID = SeraphimsID;
				}
			}
		}
	}
}