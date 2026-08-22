package dev.vesper.client;

import com.mojang.blaze3d.platform.InputConstants;
import dev.architectury.event.EventResult;
import dev.architectury.event.events.client.ClientGuiEvent;
import dev.architectury.event.events.client.ClientRawInputEvent;
import dev.architectury.event.events.client.ClientTickEvent;
import dev.architectury.hooks.client.screen.ScreenAccess;
import dev.architectury.registry.client.keymappings.KeyMappingRegistry;
import dev.vesper.VesperMod;
import dev.vesper.module.VesperModule;
import net.minecraft.client.KeyMapping;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.components.Button;
import net.minecraft.client.gui.screens.PauseScreen;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.network.chat.Component;
import net.minecraft.world.phys.AABB;
import net.minecraft.world.phys.Vec3;

public final class VesperClient {

    public static final String CATEGORY = "key.categories.vesper";
    public static final int ZOOM_KEY = 67;

    public static final double FULLBRIGHT_GAMMA = 15.0;

    private static KeyMapping menuKey;
    private static KeyMapping zoomKey;
    private static Double savedGamma;

    private static boolean sprintToggled;
    private static boolean sneakToggled;

    private VesperClient() {
    }

    public static boolean sprintToggled() {
        return sprintToggled;
    }

    public static boolean sneakToggled() {
        return sneakToggled;
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
        ClientRawInputEvent.MOUSE_CLICKED_PRE.register(VesperClient::onMouseClicked);
        VesperHud.init();
    }

    private static EventResult onMouseClicked(Minecraft client, int button, int action, int mods) {
        boolean tracking = VesperMod.config().enabled(VesperModule.CPS_DISPLAY)
                || VesperMod.config().enabled(VesperModule.KEYSTROKES);

        if (action == 1 && tracking) {
            if (button == 0) {
                VesperHud.recordClick(true);
            } else if (button == 1) {
                VesperHud.recordClick(false);
            }
        }

        if (action == 1 && button == 0
                && VesperMod.config().enabled(VesperModule.REACH_DISPLAY)
                && client.player != null && client.crosshairPickEntity != null) {
            VesperHud.recordReach(reachTo(client.player, client.crosshairPickEntity));
        }

        return EventResult.pass();
    }

    private static double reachTo(net.minecraft.world.entity.player.Player player,
                                  net.minecraft.world.entity.Entity target) {
        Vec3 eye = player.getEyePosition();
        AABB box = target.getBoundingBox();
        double dx = Math.max(Math.max(box.minX - eye.x, 0.0), eye.x - box.maxX);
        double dy = Math.max(Math.max(box.minY - eye.y, 0.0), eye.y - box.maxY);
        double dz = Math.max(Math.max(box.minZ - eye.z, 0.0), eye.z - box.maxZ);
        return Math.sqrt(dx * dx + dy * dy + dz * dz);
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

        int width = 120;

        access.addRenderableWidget(Button.builder(
                        Component.literal("Vesper Menu"),
                        button -> Minecraft.getInstance().setScreen(new VesperScreen()))
                .bounds(screen.width - width - 6, 6, width, 20)
                .build());
    }

    private static void updateHoldToggles(Minecraft client) {
        if (client.options == null) {
            return;
        }

        if (VesperMod.config().enabled(VesperModule.TOGGLE_SPRINT)) {
            while (client.options.keySprint.consumeClick()) {
                sprintToggled = !sprintToggled;
            }
        } else {
            sprintToggled = false;
        }

        if (VesperMod.config().enabled(VesperModule.TOGGLE_SNEAK)) {
            while (client.options.keyShift.consumeClick()) {
                sneakToggled = !sneakToggled;
            }
        } else {
            sneakToggled = false;
        }
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
        updateHoldToggles(client);

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
