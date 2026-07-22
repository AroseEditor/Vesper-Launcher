package dev.vesper.mixin;

import dev.vesper.VesperMod;
import dev.vesper.config.VesperConfig;
import dev.vesper.module.VesperModule;
import net.minecraft.client.Minecraft;
import net.minecraft.client.renderer.culling.Frustum;
import net.minecraft.client.renderer.entity.EntityRenderer;
import net.minecraft.world.entity.Entity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

@Mixin(EntityRenderer.class)
public class EntityRendererMixin<T extends Entity> {

    @Inject(method = "shouldRender", at = @At("HEAD"), cancellable = true)
    private void vesper$cullDistantEntities(
            T entity,
            Frustum frustum,
            double cameraX,
            double cameraY,
            double cameraZ,
            CallbackInfoReturnable<Boolean> callback) {

        VesperConfig config = VesperMod.config();

        if (!config.enabled(VesperModule.ENTITY_DISTANCE)) {
            return;
        }

        if (entity == Minecraft.getInstance().player) {
            return;
        }

        double limit = Math.max(16, config.entityRenderDistance);

        if (entity.distanceToSqr(cameraX, cameraY, cameraZ) > limit * limit) {
            callback.setReturnValue(false);
        }
    }
}
