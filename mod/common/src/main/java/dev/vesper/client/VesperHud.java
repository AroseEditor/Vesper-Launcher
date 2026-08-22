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

        if (config.enabled(VesperModule.KEYSTROKES)) {
            renderKeystrokes(graphics, client);
        }
    }

    private static void renderKeystrokes(GuiGraphics graphics, Minecraft client) {
        var options = client.options;
        int size = 20;
        int gap = 2;
        int baseX = 4;
        int baseY = client.getWindow().getGuiScaledHeight() / 2 - 40;
        int total = size * 3 + gap * 2;

        keyBox(graphics, client, baseX + size + gap, baseY, size, size, "W", options.keyUp.isDown());

        int row2 = baseY + size + gap;
        keyBox(graphics, client, baseX, row2, size, size, "A", options.keyLeft.isDown());
        keyBox(graphics, client, baseX + size + gap, row2, size, size, "S", options.keyDown.isDown());
        keyBox(graphics, client, baseX + (size + gap) * 2, row2, size, size, "D", options.keyRight.isDown());

        int mouseRow = row2 + size + gap;
        int half = (total - gap) / 2;
        keyBox(graphics, client, baseX, mouseRow, half, size,
                "LMB " + clicksWithinLastSecond(leftClicks), options.keyAttack.isDown());
        keyBox(graphics, client, baseX + half + gap, mouseRow, half, size,
                "RMB " + clicksWithinLastSecond(rightClicks), options.keyUse.isDown());

        int spaceRow = mouseRow + size + gap;
        keyBox(graphics, client, baseX, spaceRow, total, 12, "", options.keyJump.isDown());
    }

    private static void keyBox(
            GuiGraphics graphics, Minecraft client,
            int x, int y, int w, int h, String label, boolean pressed) {

        graphics.fill(x, y, x + w, y + h, pressed ? ACCENT : 0x66000000);

        if (!label.isEmpty()) {
            int tx = x + (w - client.font.width(label)) / 2;
            int ty = y + (h - 8) / 2;
            graphics.drawString(client.font, label, tx, ty, pressed ? 0xFF14141A : TEXT);
        }
    }

    private static int line(
            GuiGraphics graphics, Minecraft client, int x, int y, String text, int colour) {

        graphics.drawString(client.font, text, x, y, colour);
        return y + LINE_HEIGHT;
    }
}
