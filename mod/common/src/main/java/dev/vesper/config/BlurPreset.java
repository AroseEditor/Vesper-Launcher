package dev.vesper.config;

public enum BlurPreset {
    OFF("Off"),
    LUNAR("Lunar"),
    BADLION("Badlion");

    private final String label;

    BlurPreset(String label) {
        this.label = label;
    }

    public String label() {
        return label;
    }

    public BlurPreset next() {
        BlurPreset[] values = values();
        return values[(ordinal() + 1) % values.length];
    }
}
