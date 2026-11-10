namespace SheltreSim.Audio
{
    /// <summary>
    /// Every sound key the game uses. AudioRuntimeProbe checks each one against the real imported
    /// clips by reflection, so a typo here fails the check instead of just being silently missing.
    /// </summary>
    public static class Sfx
    {
        public const string FootDirt = "foot_dirt", FootConcrete = "foot_concrete", FootWood = "foot_wood",
            FootMetal = "foot_metal", FootSnow = "foot_snow", FootMud = "foot_mud", FootWater = "foot_water", Land = "land", Jump = "jump",
            PlayerBreath = "player_breath";

        public const string Swing = "swing", HitFlesh = "hit_flesh", HitBlock = "hit_block", RaiderSwing = "raider_swing";

        public const string PlayerHurt = "player_hurt", PlayerDeath = "player_death", RaiderAlert = "raider_alert",
            RaiderHurt = "raider_hurt", RaiderDeath = "raider_death", RaiderGrowl = "raider_growl";

        public const string DoorOpen = "door_open", DoorClose = "door_close", DoorBash = "door_bash";

        public const string GatherWood = "gather_wood", GatherStone = "gather_stone", GatherOre = "gather_ore",
            GatherScrap = "gather_scrap", GatherLeaves = "gather_leaves", Rummage = "rummage", CrateOpen = "crate_open",
            LootFound = "loot_found", LootEmpty = "loot_empty";

        public const string Eat = "eat", Drink = "drink", Heal = "heal", Equip = "equip", Craft = "craft",
            PlacePiece = "place_piece", Sleep = "sleep";

        public const string UiDeny = "ui_deny", UiClick = "ui_click", UiOpen = "ui_open", UiClose = "ui_close",
            UiTick = "ui_tick", GameSaved = "game_saved", GameLoaded = "game_loaded";

        public const string ButtonPress = "button_press", Siren = "siren", Quake = "quake", Heartbeat = "heartbeat",
            WarnVital = "warn_vital", StingerDeath = "stinger_death";

        public const string AmbWasteland = "amb_wasteland", AmbForest = "amb_forest", AmbDesert = "amb_desert",
            AmbSnow = "amb_snow", AmbMarsh = "amb_marsh", AmbHighlands = "amb_highlands", AmbBunker = "amb_bunker",
            AmbRain = "amb_rain", AmbNight = "amb_night";

        public const string AmbBird = "amb_bird", AmbCrow = "amb_crow", AmbCreak = "amb_creak", AmbBoom = "amb_boom",
            AmbWolf = "amb_wolf", AmbFrog = "amb_frog", AmbDrip = "amb_drip", AmbPipe = "amb_pipe",
            Thunder = "thunder", DeerCall = "deer_call", BoarGrunt = "boar_grunt";

        public const string Discovery = "discovery", Upgrade = "upgrade";

        public const string MusicExplore = "music_explore", MusicTension = "music_tension";

        public const string GunPistol = "gun_pistol", GunSmg = "gun_smg", GunShotgun = "gun_shotgun", GunRifle = "gun_rifle",
            GunReload = "gun_reload", GunEmpty = "gun_empty", LaserShot = "laser_shot", PlasmaShot = "plasma_shot",
            EnergyOverheat = "energy_overheat", BulletImpact = "bullet_impact", PlasmaBurst = "plasma_burst", RaiderGunshot = "raider_gunshot";

        public const string TurretFire = "turret_fire", TurretLaser = "turret_laser", TrapSnap = "trap_snap", TrapShock = "trap_shock",
            StructureHit = "structure_hit", StructureBreak = "structure_break", RaidHorn = "raid_horn", AlarmKlaxon = "alarm_klaxon";

        public const string GeneratorStart = "generator_start", GeneratorStop = "generator_stop", GeneratorHum = "generator_hum",
            MachineHum = "machine_hum", FanLoop = "fan_loop", PowerDown = "power_down", RepairTick = "repair_tick";

        public const string RadioStatic = "radio_static", RadioPing = "radio_ping", ObjectiveDone = "objective_done";

        public const string GeigerClick = "geiger_click";

        public const string MotorBuggy = "motor_buggy", MotorVan = "motor_van", BikeChain = "bike_chain";

        public const string DrawerOpen = "drawer_open", CabinetOpen = "cabinet_open", EngineStart = "engine_start", EngineStop = "engine_stop", VehicleHorn = "vehicle_horn",
            AmbInterior = "amb_interior";
    }
}
