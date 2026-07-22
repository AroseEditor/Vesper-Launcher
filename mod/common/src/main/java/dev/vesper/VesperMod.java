package dev.vesper;

import dev.vesper.config.VesperConfig;
import dev.vesper.render.MotionBlur;
import dev.vesper.skins.SkinResolver;

import java.nio.file.Path;

public final class VesperMod {

    public static final String MOD_ID = "vesper";
    public static final String NAME = "Vesper";
    public static final int MENU_KEY = 344;

    private static VesperConfig config = new VesperConfig();
    private static MotionBlur motionBlur = new MotionBlur();
    private static SkinResolver skinResolver;
    private static Path configPath;

    private VesperMod() {
    }

    public static void init(Path gameDirectory) {
        configPath = gameDirectory.resolve("config").resolve(MOD_ID + ".json");
        config = VesperConfig.load(configPath);
        motionBlur = new MotionBlur();
        skinResolver = new SkinResolver(gameDirectory.resolve(MOD_ID).resolve("skins"));
    }

    public static VesperConfig config() {
        return config;
    }

    public static MotionBlur motionBlur() {
        return motionBlur;
    }

    public static SkinResolver skins() {
        return skinResolver;
    }

    public static void save() {
        if (configPath != null) {
            config.save(configPath);
        }
    }
}
