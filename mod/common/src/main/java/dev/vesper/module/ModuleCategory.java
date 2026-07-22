package dev.vesper.module;

public enum ModuleCategory {
    PERFORMANCE("Performance"),
    VISUAL("Visual"),
    HUD("HUD"),
    QUALITY_OF_LIFE("Quality of life"),
    COSMETIC("Cosmetic");

    private final String label;

    ModuleCategory(String label) {
        this.label = label;
    }

    public String label() {
        return label;
    }
}
