package dev.vesper.mixin;

import com.mojang.blaze3d.vertex.PoseStack;
import dev.vesper.VesperMod;
import dev.vesper.client.VesperClient;
import dev.vesper.module.VesperModule;
import net.minecraft.client.Camera;
import net.minecraft.client.renderer.GameRenderer;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

@Mixin(GameRenderer.class)
public class GameRendererMixin {

    @Inject(method = "bobHurt", at = @At("HEAD"), cancellable = true)
    private void vesper$noHurtCameraTilt(PoseStack poseStack, float partialTick, CallbackInfo callback) {
        if (VesperMod.config().enabled(VesperModule.NO_HURT_CAMERA)
                || VesperMod.config().enabled(VesperModule.NO_SCREEN_SHAKE)) {
            callback.cancel();
        }
    }

    @Inject(method = "getFov", at = @At("RETURN"), cancellable = true)
    private void vesper$zoom(
            Camera camera, float partialTicks, boolean useFovSetting,
            CallbackInfoReturnable<Double> callback) {

        if (useFovSetting
                && VesperMod.config().enabled(VesperModule.ZOOM)
                && VesperClient.zoomHeld()) {
            callback.setReturnValue((double) VesperMod.config().zoomFov);
        }
    }
}
