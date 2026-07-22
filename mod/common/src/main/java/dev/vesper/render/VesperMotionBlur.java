package dev.vesper.render;

import dev.vesper.VesperMod;
import dev.vesper.config.BlurPreset;
import dev.vesper.config.VesperConfig;
import dev.vesper.module.VesperModule;
import net.minecraft.client.Camera;
import net.minecraft.client.Minecraft;

public final class VesperMotionBlur {

    private static final MotionBlurRenderer RENDERER = new MotionBlurRenderer();

    private static long lastFrameNanos;

    private VesperMotionBlur() {
    }

    public static void onWorldRendered() {
        Minecraft client = Minecraft.getInstance();
        VesperConfig config = VesperMod.config();

        if (!config.enabled(VesperModule.MOTION_BLUR) || config.blurPreset == BlurPreset.OFF) {
            RENDERER.reset();
            lastFrameNanos = 0L;
            return;
        }

        if (client.level == null || client.player == null) {
            RENDERER.reset();
            lastFrameNanos = 0L;
            return;
        }

        float delta = measureDelta();

        if (delta <= 0f) {
            return;
        }

        Camera camera = client.gameRenderer.getMainCamera();

        float retention = VesperMod.motionBlur().update(
                config.blurPreset,
                config.clampedStrength(),
                delta,
                camera.getYRot(),
                camera.getXRot());

        try {
            RENDERER.render(client, retention);
        } catch (RuntimeException e) {
            RENDERER.reset();
        }
    }

    public static void invalidate() {
        RENDERER.reset();
        lastFrameNanos = 0L;
    }

    private static float measureDelta() {
        long now = System.nanoTime();

        if (lastFrameNanos == 0L) {
            lastFrameNanos = now;
            return 0f;
        }

        float delta = (now - lastFrameNanos) / 1_000_000_000f;
        lastFrameNanos = now;
        return delta;
    }
}
