package dev.vesper.render;

import dev.vesper.config.BlurPreset;

public final class MotionBlur {

    public static final float REFERENCE_FPS = 60f;
    public static final float BADLION_FULL_TURN_SPEED = 220f;
    public static final float MAX_RETENTION = 0.96f;
    public static final float MIN_DELTA = 1f / 480f;
    public static final float MAX_DELTA = 1f / 15f;

    private float retention;
    private float lastYaw;
    private float lastPitch;
    private boolean primed;

    public float retention() {
        return retention;
    }

    public boolean active() {
        return retention > 0.01f;
    }

    public void reset() {
        retention = 0f;
        primed = false;
    }

    public float update(BlurPreset preset, float strength, float deltaSeconds, float yaw, float pitch) {
        float angularSpeed = angularSpeed(deltaSeconds, yaw, pitch);
        retention = computeRetention(preset, strength, deltaSeconds, angularSpeed);
        return retention;
    }

    private float angularSpeed(float deltaSeconds, float yaw, float pitch) {
        if (!primed) {
            lastYaw = yaw;
            lastPitch = pitch;
            primed = true;
            return 0f;
        }

        float deltaYaw = wrapDegrees(yaw - lastYaw);
        float deltaPitch = pitch - lastPitch;

        lastYaw = yaw;
        lastPitch = pitch;

        float travelled = (float) Math.sqrt(deltaYaw * deltaYaw + deltaPitch * deltaPitch);
        float clampedDelta = clamp(deltaSeconds, MIN_DELTA, MAX_DELTA);

        return travelled / clampedDelta;
    }

    public static float computeRetention(
            BlurPreset preset, float strength, float deltaSeconds, float angularSpeed) {

        if (preset == BlurPreset.OFF || strength <= 0f) {
            return 0f;
        }

        float target = switch (preset) {
            case LUNAR -> strength;
            case BADLION -> strength * clamp(angularSpeed / BADLION_FULL_TURN_SPEED, 0f, 1f);
            case OFF -> 0f;
        };

        target = clamp(target, 0f, MAX_RETENTION);

        if (target <= 0f) {
            return 0f;
        }

        float delta = clamp(deltaSeconds, MIN_DELTA, MAX_DELTA);
        float exponent = delta * REFERENCE_FPS;

        return clamp((float) Math.pow(target, exponent), 0f, MAX_RETENTION);
    }

    public static float wrapDegrees(float degrees) {
        float wrapped = degrees % 360f;

        if (wrapped >= 180f) {
            wrapped -= 360f;
        }

        if (wrapped < -180f) {
            wrapped += 360f;
        }

        return wrapped;
    }

    public static float clamp(float value, float min, float max) {
        return value < min ? min : Math.min(value, max);
    }
}
