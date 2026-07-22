package dev.vesper.client;

import com.mojang.blaze3d.platform.InputConstants;
import dev.architectury.event.events.client.ClientTickEvent;
import dev.architectury.registry.client.keymappings.KeyMappingRegistry;
import dev.vesper.VesperMod;
import net.minecraft.client.KeyMapping;
import net.minecraft.client.Minecraft;

public final class VesperClient {

    public static final String CATEGORY = "key.categories.vesper";
    public static final int ZOOM_KEY = 67;

    private static KeyMapping menuKey;
    private static KeyMapping zoomKey;

    private VesperClient() {
    }

    public static void init() {
        menuKey = new KeyMapping(
                "key.vesper.menu", InputConstants.Type.KEYSYM, VesperMod.MENU_KEY, CATEGORY);
        zoomKey = new KeyMapping(
                "key.vesper.zoom", InputConstants.Type.KEYSYM, ZOOM_KEY, CATEGORY);

        KeyMappingRegistry.register(menuKey);
        KeyMappingRegistry.register(zoomKey);

        ClientTickEvent.CLIENT_POST.register(VesperClient::onClientTick);
        VesperHud.init();
    }

    public static KeyMapping menuKey() {
        return menuKey;
    }

    public static KeyMapping zoomKey() {
        return zoomKey;
    }

    public static boolean zoomHeld() {
        return zoomKey != null && zoomKey.isDown();
    }

    private static void onClientTick(Minecraft client) {
        if (menuKey == null) {
            return;
        }

        boolean opened = false;

        while (menuKey.consumeClick()) {
            opened = true;
        }

        if (opened && client.screen == null) {
            client.setScreen(new VesperScreen());
        }
    }
}
