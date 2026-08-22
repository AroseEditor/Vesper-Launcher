package dev.vesper.client;

import dev.architectury.event.events.client.ClientGuiEvent;
import dev.vesper.VesperMod;
import dev.vesper.config.VesperConfig;
import dev.vesper.module.VesperModule;
import net.minecraft.client.DeltaTracker;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.GuiGraphics;

import java.time.LocalTime;
import java.time.format.DateTimeFormatter;
import java.util.ArrayList;
import java.util.List;

public final class VesperHud {

    private static final int ACCENT = 0xFFB57EDC;
    private static final int TEXT = 0xFFF2EEF6;
    private static final int LINE_HEIGHT = 11;
    private static final DateTimeFormatter CLOCK = DateTimeFormatter.ofPattern("HH:mm");

    private static final List<Long> leftClicks = new ArrayList<>();
    private static final List<Long> rightClicks = new ArrayList<>();

    private VesperHud() {
    }

    public static void init() {
        ClientGuiEvent.RENDER_HUD.register(VesperHud::render);
    }

    public static void recordClick(boolean left) {
        (left ? leftClicks : rightClicks).add(System.currentTimeMillis());
    }

    private static int clicksWithinLastSecond(List<Long> clicks) {
        long cutoff = System.currentTimeMillis() - 1000L;
        clicks.removeIf(time -> time < cutoff);
        return clicks.size();
    }

    private static void render(GuiGraphics graphics, DeltaTracker delta) {
        Minecraft client = Minecraft.getInstance();

        if (client.player == null || client.level == null || client.options.hideGui) {
            return;
        }

        if (client.screen != null) {
            return;
        }

        VesperConfig config = VesperMod.config();
        int x = 4;
        int y = 4;

        if (config.enabled(VesperModule.FPS_DISPLAY)) {
            y = line(graphics, client, x, y, client.getFps() + " fps", ACCENT);
        }

        if (config.enabled(VesperModule.COORDINATES)) {
            String position = String.format(
                    "%.1f  %.1f  %.1f",
                    client.player.getX(), client.player.getY(), client.player.getZ());
            y = line(graphics, client, x, y, position, TEXT);
        }

        if (config.enabled(VesperModule.DIRECTION)) {
            y = line(graphics, client, x, y,
                    client.player.getDirection().getName(), TEXT);
        }

        if (config.enabled(VesperModule.CPS_DISPLAY)) {
            String cps = clicksWithinLastSecond(leftClicks) + " | "
                    + clicksWithinLastSecond(rightClicks) + " cps";
            y = line(graphics, client, x, y, cps, TEXT);
        }

        if (config.enabled(VesperModule.MEMORY_DISPLAY)) {
            Runtime runtime = Runtime.getRuntime();
            long used = (runtime.totalMemory() - runtime.freeMemory()) / 1048576L;
            long max = runtime.maxMemory() / 1048576L;
            y = line(graphics, client, x, y, used + " / " + max + " MB", TEXT);
        }

        if (config.enabled(VesperModule.TIME_DISPLAY)) {
            y = line(graphics, client, x, y, LocalTime.now().format(CLOCK), TEXT);
        }

        if (config.enabled(VesperModule.BIOME_DISPLAY)) {
            String biome = client.level
                    .getBiome(client.player.blockPosition())
                    .unwrapKey()
                    .map(key -> key.location().getPath())
                    .orElse("unknown");
            line(graphics, client, x, y, biome, TEXT);
        }
    }

    private static int line(
            GuiGraphics graphics, Minecraft client, int x, int y, String text, int colour) {

        graphics.drawString(client.font, text, x, y, colour);
        return y + LINE_HEIGHT;
    }
}
