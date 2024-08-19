using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VSKingdom {
	public class EntityBehaviorLoyalties : EntityBehavior {
		public EntityBehaviorLoyalties(Entity entity) : base(entity) { }

		public string kingdomGUID {
			get => entity.WatchedAttributes.GetString("kingdomGUID");
			set => entity.WatchedAttributes.SetString("kingdomGUID", value);
		}

		public string cultureGUID {
			get => entity.WatchedAttributes.GetString("cultureGUID");
			set => entity.WatchedAttributes.SetString("cultureGUID", value);
		}

		public string leadersGUID {
			get => entity.WatchedAttributes.GetString("leadersGUID");
			set => entity.WatchedAttributes.SetString("leadersGUID", value);
		}

		public override string PropertyName() {
			return "KingdomLoyalties";
		}

		public override void AfterInitialized(bool onFirstSpawn) {
			base.AfterInitialized(onFirstSpawn);
			if (onFirstSpawn && entity is EntityPlayer) {
				if (kingdomGUID is null || !entity.WatchedAttributes.HasAttribute("kingdomGUID")) {
					kingdomGUID = GlobalCodes.commonerGUID;
				}
				if (cultureGUID is null || !entity.WatchedAttributes.HasAttribute("cultureGUID")) {
					cultureGUID = GlobalCodes.seraphimGUID;
				}
			}
		}
	}
}