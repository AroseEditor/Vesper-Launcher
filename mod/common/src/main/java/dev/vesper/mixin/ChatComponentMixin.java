package dev.vesper.mixin;

import dev.vesper.VesperMod;
import dev.vesper.module.VesperModule;
import net.minecraft.ChatFormatting;
import net.minecraft.client.gui.components.ChatComponent;
import net.minecraft.network.chat.Component;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.ModifyVariable;

import java.time.LocalTime;
import java.time.format.DateTimeFormatter;

@Mixin(ChatComponent.class)
public class ChatComponentMixin {

    private static final DateTimeFormatter VESPER_CHAT_CLOCK = DateTimeFormatter.ofPattern("HH:mm");

    @ModifyVariable(
            method = "addMessage(Lnet/minecraft/network/chat/Component;Lnet/minecraft/network/chat/MessageSignature;Lnet/minecraft/client/GuiMessageTag;)V",
            at = @At("HEAD"),
            argsOnly = true)
    private Component vesper$timestamp(Component message) {
        if (!VesperMod.config().enabled(VesperModule.CHAT_TIMESTAMPS)) {
            return message;
        }

        Component prefix = Component
                .literal("[" + LocalTime.now().format(VESPER_CHAT_CLOCK) + "] ")
                .withStyle(ChatFormatting.DARK_GRAY);

        return Component.empty().append(prefix).append(message);
    }
}
