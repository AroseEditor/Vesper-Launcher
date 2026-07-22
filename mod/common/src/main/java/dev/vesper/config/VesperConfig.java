package dev.vesper.config;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import dev.vesper.hud.HudElement;
import dev.vesper.hud.HudModule;
import dev.vesper.module.VesperModule;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.EnumMap;
import java.util.Map;

public final class VesperConfig {

    private static final Gson GSON = new GsonBuilder().setPrettyPrinting().create();

    public BlurPreset blurPreset = BlurPreset.LUNAR;
    public float blurStrength = 0.55f;

    public boolean fullbright = false;
    public boolean toggleSprint = false;
    public float zoomFov = 24f;

    public boolean showCape = true;
    public boolean resolveSkinsByUsername = true;

    public boolean discordPresence = true;

    public Map<HudModule, HudElement> hud = defaultHud();

    public Map<VesperModule, Boolean> modules = defaultModules();

    public int entityRenderDistance = 64;
    public int particleLimit = 2000;
    public float animationRate = 1.0f;

    public static Map<VesperModule, Boolean> defaultModules() {
        Map<VesperModule, Boolean> map = new EnumMap<>(VesperModule.class);

        for (VesperModule module : VesperModule.values()) {
            map.put(module, module.enabledByDefault());
        }

        return map;
    }

    public boolean enabled(VesperModule module) {
        return modules.computeIfAbsent(module, VesperModule::enabledByDefault);
    }

    public void setEnabled(VesperModule module, boolean value) {
        modules.put(module, value);
    }

    public boolean toggle(VesperModule module) {
        boolean value = !enabled(module);
        setEnabled(module, value);
        return value;
    }

    public static Map<HudModule, HudElement> defaultHud() {
        Map<HudModule, HudElement> map = new EnumMap<>(HudModule.class);
        int y = 6;

        for (HudModule module : HudModule.values()) {
            map.put(module, new HudElement(6, y, 1f, module.enabledByDefault()));
            y += 14;
        }

        return map;
    }

    public HudElement element(HudModule module) {
        return hud.computeIfAbsent(module, key -> new HudElement(6, 6, 1f, key.enabledByDefault()));
    }

    public float clampedStrength() {
        return Math.max(0f, Math.min(blurStrength, 0.95f));
    }

    public static VesperConfig load(Path path) {
        try {
            if (Files.exists(path)) {
                String json = Files.readString(path);
                VesperConfig config = GSON.fromJson(json, VesperConfig.class);

                if (config != null) {
                    if (config.hud == null) {
                        config.hud = defaultHud();
                    }
                    if (config.modules == null) {
                        config.modules = defaultModules();
                    }
                    return config;
                }
            }
        } catch (IOException | RuntimeException ignored) {
            return new VesperConfig();
        }

        return new VesperConfig();
    }

    public void save(Path path) {
        try {
            Files.createDirectories(path.getParent());
            Files.writeString(path, GSON.toJson(this));
        } catch (IOException ignored) {
            return;
        }
    }
}
