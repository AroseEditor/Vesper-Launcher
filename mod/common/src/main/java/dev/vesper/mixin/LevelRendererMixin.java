package dev.vesper.mixin;

import com.mojang.blaze3d.vertex.PoseStack;
import dev.vesper.VesperMod;
import dev.vesper.module.VesperModule;
import net.minecraft.client.renderer.LevelRenderer;
import net.minecraft.client.renderer.LightTexture;
import org.joml.Matrix4f;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

@Mixin(LevelRenderer.class)
public class LevelRendererMixin {

    @Inject(method = "renderSnowAndRain", at = @At("HEAD"), cancellable = true)
    private void vesper$hideWeather(
            LightTexture lightTexture,
            float partialTick,
            double camX,
            double camY,
            double camZ,
            CallbackInfo callback) {

        if (VesperMod.config().enabled(VesperModule.HIDE_WEATHER)) {
            callback.cancel();
        }
    }

    @Inject(method = "renderClouds", at = @At("HEAD"), cancellable = true)
    private void vesper$hideClouds(
            PoseStack poseStack,
            Matrix4f frustumMatrix,
            Matrix4f projectionMatrix,
            float partialTick,
            double camX,
            double camY,
            double camZ,
            CallbackInfo callback) {

        if (VesperMod.config().enabled(VesperModule.HIDE_CLOUDS)) {
            callback.cancel();
        }
    }
}
