package dev.vesper.client;

import dev.architectury.event.events.client.ClientGuiEvent;
import dev.vesper.VesperMod;
import dev.vesper.config.VesperConfig;
import dev.vesper.hud.HudElement;
import dev.vesper.hud.HudModule;
import dev.vesper.module.VesperModule;
import net.minecraft.client.DeltaTracker;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.world.effect.MobEffectInstance;
import net.minecraft.world.item.ItemStack;

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

    private static double lastReach;
    private static long lastReachTime;

    private VesperHud() {
    }

    public static void init() {
        ClientGuiEvent.RENDER_HUD.register(VesperHud::render);
    }

    public static void recordClick(boolean left) {
        (left ? leftClicks : rightClicks).add(System.currentTimeMillis());
    }

    public static void recordReach(double distance) {
        lastReach = distance;
        lastReachTime = System.currentTimeMillis();
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

        if (config.enabled(VesperModule.FPS_DISPLAY)) {
            text(graphics, client, HudModule.FPS, client.getFps() + " fps", ACCENT);
        }

        if (config.enabled(VesperModule.COORDINATES)) {
            text(graphics, client, HudModule.COORDINATES, String.format(
                    "%.1f  %.1f  %.1f",
                    client.player.getX(), client.player.getY(), client.player.getZ()), TEXT);
        }

        if (config.enabled(VesperModule.DIRECTION)) {
            text(graphics, client, HudModule.DIRECTION, client.player.getDirection().getName(), TEXT);
        }

        if (config.enabled(VesperModule.CPS_DISPLAY)) {
            text(graphics, client, HudModule.CPS, clicksWithinLastSecond(leftClicks) + " | "
                    + clicksWithinLastSecond(rightClicks) + " cps", TEXT);
        }

        if (config.enabled(VesperModule.PING_DISPLAY)) {
            int ping = 0;

            if (client.getConnection() != null) {
                var info = client.getConnection().getPlayerInfo(client.player.getUUID());

                if (info != null) {
                    ping = info.getLatency();
                }
            }

            text(graphics, client, HudModule.PING, ping + " ms", TEXT);
        }

        if (config.enabled(VesperModule.REACH_DISPLAY)
                && System.currentTimeMillis() - lastReachTime < 3000L) {
            text(graphics, client, HudModule.REACH, String.format("%.2f blocks", lastReach), TEXT);
        }

        if (config.enabled(VesperModule.SERVER_ADDRESS) && client.getCurrentServer() != null) {
            text(graphics, client, HudModule.SERVER, client.getCurrentServer().ip, TEXT);
        }

        if (config.enabled(VesperModule.MEMORY_DISPLAY)) {
            Runtime runtime = Runtime.getRuntime();
            long used = (runtime.totalMemory() - runtime.freeMemory()) / 1048576L;
            long max = runtime.maxMemory() / 1048576L;
            text(graphics, client, HudModule.MEMORY, used + " / " + max + " MB", TEXT);
        }

        if (config.enabled(VesperModule.TIME_DISPLAY)) {
            text(graphics, client, HudModule.TIME, LocalTime.now().format(CLOCK), TEXT);
        }

        if (config.enabled(VesperModule.BIOME_DISPLAY)) {
            String biome = client.level
                    .getBiome(client.player.blockPosition())
                    .unwrapKey()
                    .map(key -> key.location().getPath())
                    .orElse("unknown");
            text(graphics, client, HudModule.BIOME, biome, TEXT);
        }

        if (config.enabled(VesperModule.KEYSTROKES)) {
            renderKeystrokes(graphics, client);
        }

        if (config.enabled(VesperModule.ARMOUR_DISPLAY)) {
            renderArmourStatus(graphics, client);
        }

        if (config.enabled(VesperModule.POTION_EFFECTS)) {
            renderPotionEffects(graphics, client);
        }
    }

    private static void renderArmourStatus(GuiGraphics graphics, Minecraft client) {
        HudElement anchor = VesperMod.config().element(HudModule.ARMOUR);
        int x = anchor.x;
        int y = anchor.y;

        renderArmourPiece(graphics, client, client.player.getMainHandItem(), x, y);
        y += 20;

        var armour = client.player.getInventory().armor;
        for (int slot = armour.size() - 1; slot >= 0; slot--) {
            renderArmourPiece(graphics, client, armour.get(slot), x, y);
            y += 20;
        }
    }

    private static void renderArmourPiece(
            GuiGraphics graphics, Minecraft client, ItemStack stack, int x, int y) {

        if (stack.isEmpty()) {
            return;
        }

        graphics.renderItem(stack, x, y);

        if (stack.isDamageableItem()) {
            String durability = String.valueOf(stack.getMaxDamage() - stack.getDamageValue());
            graphics.drawString(client.font, durability, x + 20, y + 5, TEXT);
        }
    }

    private static void renderPotionEffects(GuiGraphics graphics, Minecraft client) {
        HudElement anchor = VesperMod.config().element(HudModule.POTIONS);
        int x = anchor.x;
        int y = anchor.y;

        for (MobEffectInstance effect : client.player.getActiveEffects()) {
            String name = effect.getEffect().value().getDisplayName().getString();
            int level = effect.getAmplifier() + 1;

            if (level > 1) {
                name = name + " " + level;
            }

            String duration = effect.isInfiniteDuration()
                    ? name
                    : name + " " + formatTicks(effect.getDuration());

            graphics.drawString(client.font, duration, x, y, TEXT);
            y += LINE_HEIGHT;
        }
    }

    private static String formatTicks(int ticks) {
        int seconds = ticks / 20;
        return String.format("%d:%02d", seconds / 60, seconds % 60);
    }

    private static void renderKeystrokes(GuiGraphics graphics, Minecraft client) {
        var options = client.options;
        HudElement anchor = VesperMod.config().element(HudModule.KEYSTROKES);
        int size = 20;
        int gap = 2;
        int baseX = anchor.x;
        int baseY = anchor.y;
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

    private static void text(
            GuiGraphics graphics, Minecraft client, HudModule module, String value, int fallback) {

        HudElement element = VesperMod.config().element(module);
        int colour = element.colour != 0 ? element.colour : fallback;

        if (element.scale == 1f) {
            graphics.drawString(client.font, value, element.x, element.y, colour);
            return;
        }

        graphics.pose().pushPose();
        graphics.pose().translate(element.x, element.y, 0);
        graphics.pose().scale(element.scale, element.scale, 1f);
        graphics.drawString(client.font, value, 0, 0, colour);
        graphics.pose().popPose();
    }
}
