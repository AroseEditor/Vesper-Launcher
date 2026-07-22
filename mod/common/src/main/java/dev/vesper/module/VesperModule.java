package dev.vesper.module;

public enum VesperModule {
    ENTITY_CULLING("Entity culling", ModuleCategory.PERFORMANCE, true,
            "Skips rendering entities that are hidden behind blocks."),
    ENTITY_DISTANCE("Entity render distance", ModuleCategory.PERFORMANCE, true,
            "Stops drawing distant entities before the chunk limit does."),
    PARTICLE_LIMIT("Particle limit", ModuleCategory.PERFORMANCE, true,
            "Caps how many particles can exist at once."),
    HIDE_WEATHER("Hide weather", ModuleCategory.PERFORMANCE, false,
            "Stops rain and snow rendering entirely."),
    HIDE_CLOUDS("Hide clouds", ModuleCategory.PERFORMANCE, false,
            "Removes cloud rendering."),
    NO_BLOCK_PARTICLES("No block break particles", ModuleCategory.PERFORMANCE, false,
            "Removes the particle burst when a block breaks."),
    NO_ENCHANT_GLINT("No enchantment glint", ModuleCategory.PERFORMANCE, false,
            "Drops the animated glint pass on enchanted items."),
    REDUCED_ANIMATIONS("Reduced animations", ModuleCategory.PERFORMANCE, false,
            "Slows texture animations such as fire, water and lava."),
    FAST_ITEM_RENDER("Fast item rendering", ModuleCategory.PERFORMANCE, false,
            "Simplifies dropped item rendering when many are on the ground."),
    LOWER_FIRE_OVERLAY("Lower fire overlay", ModuleCategory.PERFORMANCE, false,
            "Shrinks the burning overlay so it costs less fill rate."),
    NO_SCREEN_SHAKE("No screen shake", ModuleCategory.PERFORMANCE, false,
            "Removes the camera shake from damage and explosions."),

    MOTION_BLUR("Motion blur", ModuleCategory.VISUAL, true,
            "Frame accumulation blur with Lunar and Badlion presets."),
    FULLBRIGHT("Fullbright", ModuleCategory.VISUAL, false,
            "Raises brightness so caves are lit without night vision."),
    ZOOM("Zoom", ModuleCategory.VISUAL, true,
            "Hold the zoom key to narrow your field of view."),
    NO_HURT_CAMERA("No hurt camera tilt", ModuleCategory.VISUAL, false,
            "Removes the camera tilt when you take damage."),
    CLEAR_WATER("Clear water", ModuleCategory.VISUAL, false,
            "Reduces underwater fog so you can see further."),

    FPS_DISPLAY("FPS", ModuleCategory.HUD, true,
            "Shows your current frame rate."),
    CPS_DISPLAY("CPS", ModuleCategory.HUD, false,
            "Shows clicks per second for both mouse buttons."),
    COORDINATES("Coordinates", ModuleCategory.HUD, true,
            "Shows your position and the chunk you are in."),
    DIRECTION("Direction", ModuleCategory.HUD, false,
            "Shows which way you are facing."),
    PING_DISPLAY("Ping", ModuleCategory.HUD, false,
            "Shows your latency to the current server."),
    KEYSTROKES("Keystrokes", ModuleCategory.HUD, false,
            "Draws your movement keys and mouse buttons."),
    ARMOUR_DISPLAY("Armour", ModuleCategory.HUD, false,
            "Shows equipped armour and its durability."),
    MEMORY_DISPLAY("Memory", ModuleCategory.HUD, false,
            "Shows heap usage so you can spot memory pressure."),
    TIME_DISPLAY("Time", ModuleCategory.HUD, false,
            "Shows the real world clock and the in-game day."),
    BIOME_DISPLAY("Biome", ModuleCategory.HUD, false,
            "Shows the biome you are standing in."),

    TOGGLE_SPRINT("Toggle sprint", ModuleCategory.QUALITY_OF_LIFE, false,
            "Sprint stays on until you press the key again."),
    TOGGLE_SNEAK("Toggle sneak", ModuleCategory.QUALITY_OF_LIFE, false,
            "Sneak stays on until you press the key again."),
    AUTO_REJOIN("Auto rejoin", ModuleCategory.QUALITY_OF_LIFE, false,
            "Reconnects to the last server after a disconnect."),
    CHAT_TIMESTAMPS("Chat timestamps", ModuleCategory.QUALITY_OF_LIFE, false,
            "Prefixes chat lines with the time they arrived."),
    DISCORD_PRESENCE("Discord presence", ModuleCategory.QUALITY_OF_LIFE, true,
            "Shows what you are playing in Discord."),

    CUSTOM_CAPE("Custom cape", ModuleCategory.COSMETIC, true,
            "Renders the cape you set in the launcher."),
    OFFLINE_SKINS("Offline skins", ModuleCategory.COSMETIC, true,
            "Resolves skins for offline accounts so players are not all Steve.");

    private final String label;
    private final ModuleCategory category;
    private final boolean enabledByDefault;
    private final String description;

    VesperModule(String label, ModuleCategory category, boolean enabledByDefault, String description) {
        this.label = label;
        this.category = category;
        this.enabledByDefault = enabledByDefault;
        this.description = description;
    }

    public String label() {
        return label;
    }

    public ModuleCategory category() {
        return category;
    }

    public boolean enabledByDefault() {
        return enabledByDefault;
    }

    public String description() {
        return description;
    }
}
