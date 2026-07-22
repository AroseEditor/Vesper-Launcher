package dev.vesper.hud;

public enum HudModule {
    FPS("FPS", true),
    CPS("CPS", false),
    COORDINATES("Coordinates", true),
    DIRECTION("Direction", false),
    PING("Ping", false),
    KEYSTROKES("Keystrokes", false),
    ARMOUR("Armour", false),
    TIME("Time", false),
    BIOME("Biome", false),
    MEMORY("Memory", false);

    private final String label;
    private final boolean enabledByDefault;

    HudModule(String label, boolean enabledByDefault) {
        this.label = label;
        this.enabledByDefault = enabledByDefault;
    }

    public String label() {
        return label;
    }

    public boolean enabledByDefault() {
        return enabledByDefault;
    }
}
