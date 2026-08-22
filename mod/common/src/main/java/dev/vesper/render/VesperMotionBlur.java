package dev.vesper.render;

import dev.vesper.VesperMod;
import dev.vesper.config.BlurPreset;
import dev.vesper.config.VesperConfig;
import dev.vesper.module.VesperModule;
import net.minecraft.client.Camera;
import net.minecraft.client.Minecraft;
import net.minecraft.resources.ResourceKey;
import net.minecraft.world.level.Level;

public final class VesperMotionBlur {

    private static final double TELEPORT_THRESHOLD_SQ = 400.0;

    private static final MotionBlurRenderer RENDERER = new MotionBlurRenderer();

    private static long lastFrameNanos;
    private static ResourceKey<Level> lastDimension;
    private static double lastX;
    private static double lastY;
    private static double lastZ;

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

        if (movedDiscontinuously(client)) {
            RENDERER.reset();
            VesperMod.motionBlur().reset();
            lastFrameNanos = 0L;
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

    private static boolean movedDiscontinuously(Minecraft client) {
        boolean reset = false;
        ResourceKey<Level> dimension = client.level.dimension();

        if (!dimension.equals(lastDimension)) {
            lastDimension = dimension;
            reset = true;
        }

        double dx = client.player.getX() - lastX;
        double dy = client.player.getY() - lastY;
        double dz = client.player.getZ() - lastZ;

        if (dx * dx + dy * dy + dz * dz > TELEPORT_THRESHOLD_SQ) {
            reset = true;
        }

        lastX = client.player.getX();
        lastY = client.player.getY();
        lastZ = client.player.getZ();

        return reset;
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
