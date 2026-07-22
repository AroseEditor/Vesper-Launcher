package dev.vesper.client;

import com.mojang.blaze3d.platform.InputConstants;
import dev.architectury.event.events.client.ClientGuiEvent;
import dev.architectury.event.events.client.ClientTickEvent;
import dev.architectury.hooks.client.screen.ScreenAccess;
import dev.architectury.registry.client.keymappings.KeyMappingRegistry;
import dev.vesper.VesperMod;
import net.minecraft.client.KeyMapping;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.components.Button;
import net.minecraft.client.gui.screens.PauseScreen;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.network.chat.Component;

public final class VesperClient {

    public static final String CATEGORY = "key.categories.vesper";
    public static final int ZOOM_KEY = 67;

    public static final double FULLBRIGHT_GAMMA = 15.0;

    private static KeyMapping menuKey;
    private static KeyMapping zoomKey;
    private static Double savedGamma;

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
        ClientGuiEvent.INIT_POST.register(VesperClient::onScreenInit);
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

    private static void onScreenInit(Screen screen, ScreenAccess access) {
        if (!(screen instanceof PauseScreen)) {
            return;
        }

        int width = 204;
        int x = screen.width / 2 - width / 2;
        int y = Math.min(screen.height / 4 + 144, screen.height - 28);

        access.addRenderableWidget(Button.builder(
                        Component.literal("Vesper Settings"),
                        button -> Minecraft.getInstance().setScreen(new VesperScreen()))
                .bounds(x, y, width, 20)
                .build());
    }

    private static void applyFullbright(Minecraft client) {
        if (client.options == null) {
            return;
        }

        boolean wanted = VesperMod.config().enabled(dev.vesper.module.VesperModule.FULLBRIGHT);

        if (wanted) {
            if (savedGamma == null) {
                savedGamma = client.options.gamma().get();
            }

            client.options.gamma().set(FULLBRIGHT_GAMMA);
        } else if (savedGamma != null) {
            client.options.gamma().set(savedGamma);
            savedGamma = null;
        }
    }

    private static void onClientTick(Minecraft client) {
        if (!dev.vesper.skins.LocalSkins.isLoaded() && VesperMod.gameDirectory() != null) {
            dev.vesper.skins.LocalSkins.load(VesperMod.gameDirectory());
        }

        applyFullbright(client);

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
