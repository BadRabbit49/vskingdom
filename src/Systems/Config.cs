using Vintagestory.API.Common;

namespace VSKingdom {
	class ModConfig {
		private SoldierConfig config;
		class RegisterConfig : ModSystem {
			ModConfig config = new ModConfig();

			public override void StartPre(ICoreAPI api) {
				base.StartPre(api);
				config.ReadConfig(api);
			}

			public override void Dispose() {
				base.Dispose();
				config = null;
			}
		}
		// Read values from config file.
		public void ReadConfig(ICoreAPI api) {
			try {
				config = LoadConfig(api);
				// Only generate new files to default when config is null.
				if (config is null) {
					GenerateConfig(api);
					config = LoadConfig(api);
				} else {
					GenerateConfig(api, config);
				}
			} catch {
				GenerateConfig(api);
				config = LoadConfig(api);
			}
			api.World.Config.SetBool("PvpOff", config.PvpOff);
			api.World.Config.SetBool("Groups", config.Groups);
			api.World.Config.SetBool("FriendlyFireO", config.FriendlyFireO);
			api.World.Config.SetBool("FriendlyFireG", config.FriendlyFireG);
			api.World.Config.SetBool("GroupRelation", config.GroupRelation);
			api.World.Config.SetBool("ArmorWeightOn", config.ArmorWeightOn);
			api.World.Config.SetBool("AllowResupply", config.AllowResupply);
			api.World.Config.SetBool("InfiniteAmmos", config.InfiniteAmmos);
			api.World.Config.SetBool("InfiniteHeals", config.InfiniteHeals);
			api.World.Config.SetBool("FallDamageOff", config.FallDamageOff);
			api.World.Config.SetBool("AllowTeleport", config.AllowTeleport);
		}

		private SoldierConfig LoadConfig(ICoreAPI api) {
			return api.LoadModConfig<SoldierConfig>("SoldierConfig.json");
		}

		private void GenerateConfig(ICoreAPI api) {
			api.StoreModConfig<SoldierConfig>(new SoldierConfig(), "SoldierConfig.json");
		}

		private void GenerateConfig(ICoreAPI api, SoldierConfig previousConfig) {
			api.StoreModConfig<SoldierConfig>(new SoldierConfig(previousConfig), "SoldierConfig.json");
		}
	}

	class SoldierConfig {
		public SoldierConfig() { }

		// Default Settings for Configuration.
		public bool PvpOff { get; set; } = false;
		public bool Groups { get; set; } = true;
		public bool FriendlyFireO { get; set; } = false;
		public bool FriendlyFireG { get; set; } = false;
		public bool GroupRelation { get; set; } = true;
		public bool ArmorWeightOn { get; set; } = true;
		public bool AllowResupply { get; set; } = true;
		public bool InfiniteAmmos { get; set; } = true;
		public bool InfiniteHeals { get; set; } = true;
		public bool FallDamageOff { get; set; } = true;
		public bool AllowTeleport { get; set; } = true;

		// Loaded Previous Configuration if exists.
		public SoldierConfig(SoldierConfig prev) {
			PvpOff = prev.PvpOff;
			Groups = prev.Groups;
			FriendlyFireO = prev.FriendlyFireO;
			FriendlyFireG = prev.FriendlyFireG;
			GroupRelation = prev.GroupRelation;
			ArmorWeightOn = prev.ArmorWeightOn;
			AllowResupply = prev.AllowResupply;
			InfiniteAmmos = prev.InfiniteAmmos;
			InfiniteHeals = prev.InfiniteHeals;
			FallDamageOff = prev.FallDamageOff;
			AllowTeleport = prev.AllowTeleport;
		}
	}
}