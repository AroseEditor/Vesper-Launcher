package dev.vesper.client;

import dev.vesper.VesperMod;
import dev.vesper.config.VesperConfig;
import dev.vesper.hud.HudElement;
import dev.vesper.hud.HudModule;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.gui.components.Button;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.network.chat.Component;

public final class VesperHudEditScreen extends Screen {

    private static final int LINE_HEIGHT = 10;

    private static final int[] PALETTE = {
        0, 0xFFF2EEF6, 0xFF1FC7FF, 0xFF3FD07A, 0xFFFFC842, 0xFFFF4D4F, 0xFFB57EDC,
    };

    private final Screen parent;
    private HudModule dragging;
    private int dragOffsetX;
    private int dragOffsetY;

    public VesperHudEditScreen(Screen parent) {
        super(Component.literal("Vesper HUD Layout"));
        this.parent = parent;
    }

    @Override
    protected void init() {
        addRenderableWidget(Button.builder(Component.literal("Reset layout"), button -> {
            VesperMod.config().hud = VesperConfig.defaultHud();
            VesperMod.save();
        }).bounds(width / 2 - 100, height - 52, 200, 20).build());

        addRenderableWidget(Button.builder(Component.literal("Done"), button -> onClose())
                .bounds(width / 2 - 100, height - 28, 200, 20)
                .build());
    }

    @Override
    public void render(GuiGraphics graphics, int mouseX, int mouseY, float partialTick) {
        super.render(graphics, mouseX, mouseY, partialTick);

        graphics.drawCenteredString(font,
                "Drag to move, scroll to resize, right-click to recolour. Right Shift or Done to finish.",
                width / 2, 12, 0xFFB57EDC);

        VesperConfig config = VesperMod.config();

        for (HudModule module : HudModule.values()) {
            HudElement element = config.element(module);
            String label = module.label();
            int textWidth = font.width(label);
            boolean active = module == dragging;

            graphics.fill(element.x - 2, element.y - 2,
                    element.x + textWidth + 2, element.y + LINE_HEIGHT + 1,
                    active ? 0xCCB57EDC : 0x88000000);

            int labelColour = active
                    ? 0xFF14141A
                    : element.colour != 0 ? element.colour : 0xFFF2EEF6;
            graphics.drawString(font, label, element.x, element.y, labelColour);
        }
    }

    @Override
    public boolean mouseClicked(double mouseX, double mouseY, int button) {
        if (button == 0 || button == 1) {
            VesperConfig config = VesperMod.config();
            HudModule[] modules = HudModule.values();

            for (int i = modules.length - 1; i >= 0; i--) {
                HudModule module = modules[i];
                HudElement element = config.element(module);

                if (element.contains((int) mouseX, (int) mouseY,
                        font.width(module.label()), LINE_HEIGHT)) {
                    if (button == 1) {
                        element.colour = nextColour(element.colour);
                        return true;
                    }

                    dragging = module;
                    dragOffsetX = (int) mouseX - element.x;
                    dragOffsetY = (int) mouseY - element.y;
                    return true;
                }
            }
        }

        return super.mouseClicked(mouseX, mouseY, button);
    }

    private static int nextColour(int current) {
        for (int i = 0; i < PALETTE.length; i++) {
            if (PALETTE[i] == current) {
                return PALETTE[(i + 1) % PALETTE.length];
            }
        }

        return PALETTE[0];
    }

    @Override
    public boolean mouseDragged(double mouseX, double mouseY, int button, double dragX, double dragY) {
        if (dragging != null && button == 0) {
            HudElement element = VesperMod.config().element(dragging);
            element.moveTo((int) mouseX - dragOffsetX, (int) mouseY - dragOffsetY,
                    width, height, font.width(dragging.label()), LINE_HEIGHT);
            return true;
        }

        return super.mouseDragged(mouseX, mouseY, button, dragX, dragY);
    }

    @Override
    public boolean mouseScrolled(double mouseX, double mouseY, double scrollX, double scrollY) {
        VesperConfig config = VesperMod.config();
        HudModule[] modules = HudModule.values();

        for (int i = modules.length - 1; i >= 0; i--) {
            HudModule module = modules[i];
            HudElement element = config.element(module);

            if (element.contains((int) mouseX, (int) mouseY,
                    font.width(module.label()), LINE_HEIGHT)) {
                element.scale = Math.max(0.5f, Math.min(2.0f,
                        element.scale + (float) scrollY * 0.1f));
                return true;
            }
        }

        return super.mouseScrolled(mouseX, mouseY, scrollX, scrollY);
    }

    @Override
    public boolean mouseReleased(double mouseX, double mouseY, int button) {
        if (dragging != null) {
            VesperMod.config().element(dragging).snap(2);
            dragging = null;
            return true;
        }

        return super.mouseReleased(mouseX, mouseY, button);
    }

    @Override
    public boolean keyPressed(int keyCode, int scanCode, int modifiers) {
        if (keyCode == VesperMod.MENU_KEY) {
            onClose();
            return true;
        }

        return super.keyPressed(keyCode, scanCode, modifiers);
    }

    @Override
    public boolean isPauseScreen() {
        return false;
    }

    @Override
    public void onClose() {
        VesperMod.save();

        if (minecraft != null) {
            minecraft.setScreen(parent);
        }
    }
}
