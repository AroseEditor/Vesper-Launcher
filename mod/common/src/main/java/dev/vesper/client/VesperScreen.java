package dev.vesper.client;

import dev.vesper.VesperMod;
import dev.vesper.config.BlurPreset;
import dev.vesper.config.VesperConfig;
import dev.vesper.module.ModuleCategory;
import dev.vesper.module.VesperModule;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.gui.components.Button;
import net.minecraft.client.gui.components.Tooltip;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.network.chat.Component;

import java.util.ArrayList;
import java.util.List;

public class VesperScreen extends Screen {

    private static final int ACCENT = 0xFFB57EDC;
    private static final int MUTED = 0xFFA9A2B5;
    private static final int FAINT = 0xFF6E6880;
    private static final int PRIMARY = 0xFFF2EEF6;

    private ModuleCategory category = ModuleCategory.PERFORMANCE;

    public VesperScreen() {
        super(Component.literal("Vesper"));
    }

    @Override
    protected void init() {
        VesperConfig config = VesperMod.config();

        int tabWidth = 104;
        int tabGap = 4;
        ModuleCategory[] categories = ModuleCategory.values();
        int tabsWidth = categories.length * tabWidth + (categories.length - 1) * tabGap;
        int tabX = (this.width - tabsWidth) / 2;

        for (ModuleCategory value : categories) {
            boolean active = value == category;
            Component label = Component.literal(active ? "> " + value.label() : value.label());

            addRenderableWidget(Button.builder(label, button -> {
                category = value;
                rebuildWidgets();
            }).bounds(tabX, 44, tabWidth, 20).build());

            tabX += tabWidth + tabGap;
        }

        List<VesperModule> modules = new ArrayList<>();

        for (VesperModule module : VesperModule.values()) {
            if (module.category() == category) {
                modules.add(module);
            }
        }

        int columns = 2;
        int cellWidth = 214;
        int cellHeight = 24;
        int gridWidth = columns * cellWidth + (columns - 1) * 8;
        int startX = (this.width - gridWidth) / 2;
        int startY = 80;

        for (int i = 0; i < modules.size(); i++) {
            VesperModule module = modules.get(i);
            int column = i % columns;
            int row = i / columns;
            int x = startX + column * (cellWidth + 8);
            int y = startY + row * cellHeight;

            addRenderableWidget(Button.builder(moduleLabel(config, module), button -> {
                config.toggle(module);
                VesperMod.save();
                button.setMessage(moduleLabel(config, module));
            })
                    .bounds(x, y, cellWidth, 20)
                    .tooltip(Tooltip.create(Component.literal(module.description())))
                    .build());
        }

        if (category == ModuleCategory.PERFORMANCE) {
            addRenderableWidget(Button.builder(distanceLabel(config), button -> {
                config.entityRenderDistance += 16;

                if (config.entityRenderDistance > 256) {
                    config.entityRenderDistance = 16;
                }

                VesperMod.save();
                button.setMessage(distanceLabel(config));
            }).bounds(startX, this.height - 62, cellWidth, 20).build());

            addRenderableWidget(Button.builder(particleLabel(config), button -> {
                config.particleLimit += 500;

                if (config.particleLimit > 4000) {
                    config.particleLimit = 500;
                }

                VesperMod.save();
                button.setMessage(particleLabel(config));
            }).bounds(startX + cellWidth + 8, this.height - 62, cellWidth, 20).build());
        }

        if (category == ModuleCategory.VISUAL) {
            addRenderableWidget(Button.builder(blurLabel(config), button -> {
                config.blurPreset = config.blurPreset.next();
                VesperMod.save();
                button.setMessage(blurLabel(config));
            }).bounds(startX, this.height - 62, cellWidth, 20).build());

            addRenderableWidget(Button.builder(strengthLabel(config), button -> {
                config.blurStrength = Math.round((config.blurStrength + 0.1f) * 10f) / 10f;

                if (config.blurStrength > 0.9f) {
                    config.blurStrength = 0.1f;
                }

                VesperMod.save();
                button.setMessage(strengthLabel(config));
            }).bounds(startX + cellWidth + 8, this.height - 62, cellWidth, 20).build());
        }

        addRenderableWidget(Button.builder(Component.literal("Done"), button -> onClose())
                .bounds((this.width - 120) / 2, this.height - 32, 120, 20).build());
    }

    private static Component moduleLabel(VesperConfig config, VesperModule module) {
        return Component.literal((config.enabled(module) ? "[ON]  " : "[OFF] ") + module.label());
    }

    private static Component distanceLabel(VesperConfig config) {
        return Component.literal("Entity distance: " + config.entityRenderDistance + " blocks");
    }

    private static Component particleLabel(VesperConfig config) {
        return Component.literal("Particle limit: " + config.particleLimit);
    }

    private static Component blurLabel(VesperConfig config) {
        return Component.literal("Motion blur preset: " + config.blurPreset.label());
    }

    private static Component strengthLabel(VesperConfig config) {
        return Component.literal("Blur strength: " + Math.round(config.blurStrength * 100f) + "%");
    }

    @Override
    public void render(GuiGraphics graphics, int mouseX, int mouseY, float partialTick) {
        super.render(graphics, mouseX, mouseY, partialTick);

        graphics.drawCenteredString(this.font, "VESPER", this.width / 2, 16, ACCENT);
        graphics.drawCenteredString(this.font, "Right Shift opens this menu", this.width / 2, 28, FAINT);

        int enabled = 0;

        for (VesperModule module : VesperModule.values()) {
            if (VesperMod.config().enabled(module)) {
                enabled++;
            }
        }

        String summary = enabled + " of " + VesperModule.values().length + " modules enabled";
        graphics.drawCenteredString(this.font, summary, this.width / 2, this.height - 46, MUTED);
    }

    @Override
    public boolean isPauseScreen() {
        return false;
    }

    @Override
    public void onClose() {
        VesperMod.save();
        super.onClose();
    }
}
