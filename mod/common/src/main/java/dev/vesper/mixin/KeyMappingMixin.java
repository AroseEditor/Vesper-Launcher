package dev.vesper.mixin;

import dev.vesper.VesperMod;
import dev.vesper.client.VesperClient;
import dev.vesper.module.VesperModule;
import net.minecraft.client.KeyMapping;
import net.minecraft.client.Minecraft;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

@Mixin(KeyMapping.class)
public class KeyMappingMixin {

    @Inject(method = "isDown", at = @At("HEAD"), cancellable = true)
    private void vesper$holdToggle(CallbackInfoReturnable<Boolean> callback) {
        Minecraft client = Minecraft.getInstance();

        if (client.options == null) {
            return;
        }

        Object self = this;

        if (self == client.options.keySprint
                && VesperMod.config().enabled(VesperModule.TOGGLE_SPRINT)
                && VesperClient.sprintToggled()) {
            callback.setReturnValue(true);
        } else if (self == client.options.keyShift
                && VesperMod.config().enabled(VesperModule.TOGGLE_SNEAK)
                && VesperClient.sneakToggled()) {
            callback.setReturnValue(true);
        }
    }
}
