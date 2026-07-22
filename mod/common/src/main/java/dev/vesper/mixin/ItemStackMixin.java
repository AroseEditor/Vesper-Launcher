package dev.vesper.mixin;

import dev.vesper.VesperMod;
import dev.vesper.module.VesperModule;
import net.minecraft.world.item.ItemStack;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

@Mixin(ItemStack.class)
public class ItemStackMixin {

    @Inject(method = "hasFoil", at = @At("HEAD"), cancellable = true)
    private void vesper$removeGlint(CallbackInfoReturnable<Boolean> callback) {
        if (VesperMod.config().enabled(VesperModule.NO_ENCHANT_GLINT)) {
            callback.setReturnValue(false);
        }
    }
}
