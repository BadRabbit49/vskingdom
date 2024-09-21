using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VSKingdom {
	public class EntityBehaviorLoyalties : EntityBehavior {
		public EntityBehaviorLoyalties(Entity entity) : base(entity) { }

		public string kingdomGUID {
			get => entity.WatchedAttributes.GetString(KingdomUID);
			set => entity.WatchedAttributes.SetString(KingdomUID, value);
		}

		public string cultureGUID {
			get => entity.WatchedAttributes.GetString(CultureUID);
			set => entity.WatchedAttributes.SetString(CultureUID, value);
		}

		public string leadersGUID {
			get => entity.WatchedAttributes.GetString(LeadersUID);
			set => entity.WatchedAttributes.SetString(LeadersUID, value);
		}

		public override string PropertyName() {
			return "KingdomLoyalties";
		}

		public override void AfterInitialized(bool onFirstSpawn) {
			base.AfterInitialized(onFirstSpawn);
			if (onFirstSpawn && entity is EntityPlayer) {
				if (kingdomGUID is null || !entity.WatchedAttributes.HasAttribute(KingdomUID)) {
					kingdomGUID = CommonerID;
				}
				if (cultureGUID is null || !entity.WatchedAttributes.HasAttribute(CultureUID)) {
					cultureGUID = SeraphimID;
				}
			}
		}
	}
}