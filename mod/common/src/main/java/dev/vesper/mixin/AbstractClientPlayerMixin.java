package dev.vesper.mixin;

import dev.vesper.VesperMod;
import dev.vesper.module.VesperModule;
import dev.vesper.skins.LocalSkins;
import net.minecraft.client.Minecraft;
import net.minecraft.client.player.AbstractClientPlayer;
import net.minecraft.client.resources.PlayerSkin;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

@Mixin(AbstractClientPlayer.class)
public class AbstractClientPlayerMixin {

    @Inject(method = "getSkin", at = @At("RETURN"), cancellable = true)
    private void vesper$useLocalSkin(CallbackInfoReturnable<PlayerSkin> callback) {
        if (!VesperMod.config().enabled(VesperModule.OFFLINE_SKINS)) {
            return;
        }

        if (!LocalSkins.hasSkin()) {
            return;
        }

        if ((Object) this != Minecraft.getInstance().player) {
            return;
        }

        callback.setReturnValue(LocalSkins.apply(callback.getReturnValue()));
    }
}
