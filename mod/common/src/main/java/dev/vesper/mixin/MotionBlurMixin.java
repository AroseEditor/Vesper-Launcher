package dev.vesper.mixin;

import dev.vesper.render.VesperMotionBlur;
import net.minecraft.client.DeltaTracker;
import net.minecraft.client.renderer.GameRenderer;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

@Mixin(GameRenderer.class)
public class MotionBlurMixin {

    @Inject(method = "renderLevel", at = @At("TAIL"))
    private void vesper$applyMotionBlur(DeltaTracker deltaTracker, CallbackInfo callback) {
        VesperMotionBlur.onWorldRendered();
    }
}
