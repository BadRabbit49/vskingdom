using Vintagestory.API.Common;

namespace VSKingdom {
	class ModConfig {
		private VSKingdomConfig config;
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
			api.World.Config.SetBool("AllowRenames", config.AllowRenames);
			api.World.Config.SetBool("FriendlyFire", config.FriendlyFire);
			api.World.Config.SetBool("FallDamageOn", config.FallDamageOn);
			api.World.Config.SetBool("ArmorWeights", config.ArmorWeights);
			api.World.Config.SetBool("InfiniteAmmo", config.InfiniteAmmo);
			api.World.Config.SetBool("InfiniteHeal", config.InfiniteHeal);
			api.World.Config.SetBool("DropsOnDeath", config.DropsOnDeath);
			api.World.Config.SetBool("AllowLooting", config.AllowLooting);
			api.World.Config.SetBool("HideAllNames", config.HideAllNames);
			api.World.Config.SetBool("AllowCivilWars", config.AllowCivilWars);
			api.World.Config.SetBool("AllowTeleports", config.AllowTeleports);
			api.World.Config.SetInt("MinCreateLevel", config.MinCreateLevel);
			api.World.Config.SetInt("MinCultureMake", config.MinCultureMake);
			api.World.Config.SetInt("MaxNewKingdoms", config.MaxNewKingdoms);
			api.World.Config.SetInt("MaxNewCultures", config.MaxNewCultures);
			api.World.Config.SetInt("NameRenderDist", config.NameRenderDist);
			api.World.Config.SetString("ServerLanguage", config.ServerLanguage);
			api.World.Config.SetString("BasicMascNames", config.BasicMascNames);
			api.World.Config.SetString("BasicFemmNames", config.BasicFemmNames);
			api.World.Config.SetString("BasicLastNames", config.BasicLastNames);
		}

		private VSKingdomConfig LoadConfig(ICoreAPI api) {
			return api.LoadModConfig<VSKingdomConfig>("VSKingdomConfig.json");
		}

		private void GenerateConfig(ICoreAPI api) {
			api.StoreModConfig<VSKingdomConfig>(new VSKingdomConfig(), "VSKingdomConfig.json");
		}

		private void GenerateConfig(ICoreAPI api, VSKingdomConfig previousConfig) {
			api.StoreModConfig<VSKingdomConfig>(new VSKingdomConfig(previousConfig), "VSKingdomConfig.json");
		}
	}

	class VSKingdomConfig {
		public VSKingdomConfig() { }
		// Default Settings for Configuration.
		public bool AllowRenames { get; set; } = true;
		public bool FriendlyFire { get; set; } = true;
		public bool FallDamageOn { get; set; } = true;
		public bool ArmorWeights { get; set; } = true;
		public bool InfiniteAmmo { get; set; } = false;
		public bool InfiniteHeal { get; set; } = false;
		public bool DropsOnDeath { get; set; } = true;
		public bool AllowLooting { get; set; } = true;
		public bool HideAllNames { get; set; } = true;
		public bool AllowCivilWars { get; set; } = true;
		public bool AllowTeleports { get; set; } = true;
		public int MinCreateLevel { get; set; } = -1;
		public int MinCultureMake { get; set; } = 720;
		public int MaxNewKingdoms { get; set; } = -1;
		public int MaxNewCultures { get; set; } = -1;
		public int NameRenderDist { get; set; } = 500;
		public string ServerLanguage { get; set; } = "en";
		public string BasicMascNames { get; set; } = "Aphid, Eriek, Adachi, Farhad, Barker, Floyd, Temper, Kanin, Fauln, Riftok, Blauld, Canft, Henlir, Rauln, Gautfor, Gouldfor, Mantiel, Shink, Helno, Recksmeal, Marky, Marcus, Hardwin, Timoleon, Leonidas, Yikes, Brutus, William, Maurinus, Hubertus, Bikke";
		public string BasicFemmNames { get; set; } = "Annie, Agnes, Lestli, Candis, Bianca, Hedwig, Demud, Imagina, Johanna, Judith, Magdalena, Philippa, Violat, Walpurga, Josephine, Joana, Yvonne, Tauts, Diane, Olivia, Hermoine, Triss, Harley, Lucia, Mirella, Bernadetta, Tacia, Listle, Willa, Sabrine";
		public string BasicLastNames { get; set; } = "Ponright, Tonio, Elliott, Daniels, Perez, Smith, Parkins, Genright, Harkons, Newoak, Bishop, Gearsmith, Steenwilk, Beckett, Birchwood, Driftern, Caesaran, Lisbeth, Flenk, Tunnleway, Sharps, Reynolds, Cokforth, Reyleigh, Jaunt, Biklin, Brown, Meina, Kant";
		// Loaded Previous Configuration if exists.
		public VSKingdomConfig(VSKingdomConfig prev) {
			AllowRenames = prev.AllowRenames;
			FriendlyFire = prev.FriendlyFire;
			FallDamageOn = prev.FallDamageOn;
			ArmorWeights = prev.ArmorWeights;
			InfiniteAmmo = prev.InfiniteAmmo;
			InfiniteHeal = prev.InfiniteHeal;
			DropsOnDeath = prev.DropsOnDeath;
			AllowLooting = prev.AllowLooting;
			HideAllNames = prev.HideAllNames;
			AllowCivilWars = prev.AllowCivilWars;
			AllowTeleports = prev.AllowTeleports;
			MinCreateLevel = prev.MinCreateLevel;
			MinCultureMake = prev.MinCultureMake;
			MaxNewKingdoms = prev.MaxNewKingdoms;
			MaxNewCultures = prev.MaxNewCultures;
			NameRenderDist = prev.NameRenderDist;
			ServerLanguage = prev.ServerLanguage;
			BasicMascNames = prev.BasicMascNames;
			BasicFemmNames = prev.BasicFemmNames;
			BasicLastNames = prev.BasicLastNames;
		}
	}
}