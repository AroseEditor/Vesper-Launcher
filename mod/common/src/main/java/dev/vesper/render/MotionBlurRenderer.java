package dev.vesper.render;

import com.mojang.blaze3d.pipeline.RenderTarget;
import com.mojang.blaze3d.pipeline.TextureTarget;
import com.mojang.blaze3d.platform.GlStateManager;
import com.mojang.blaze3d.systems.RenderSystem;
import com.mojang.blaze3d.vertex.BufferBuilder;
import com.mojang.blaze3d.vertex.BufferUploader;
import com.mojang.blaze3d.vertex.DefaultVertexFormat;
import com.mojang.blaze3d.vertex.Tesselator;
import com.mojang.blaze3d.vertex.VertexFormat;
import com.mojang.blaze3d.vertex.VertexSorting;
import net.minecraft.client.Minecraft;
import net.minecraft.client.renderer.GameRenderer;
import org.joml.Matrix4f;
import org.lwjgl.opengl.GL30;

public final class MotionBlurRenderer {

    private RenderTarget history;
    private int width;
    private int height;
    private boolean seeded;

    public void render(Minecraft client, float retention) {
        RenderTarget main = client.getMainRenderTarget();

        if (main == null || main.width <= 0 || main.height <= 0) {
            return;
        }

        ensureHistory(main);

        if (seeded && retention > 0.01f) {
            blendHistoryOnto(retention);
        }

        copyIntoHistory(main);
        seeded = true;
    }

    public void reset() {
        if (history != null) {
            history.destroyBuffers();
            history = null;
        }

        seeded = false;
    }

    private void ensureHistory(RenderTarget main) {
        if (history != null && width == main.width && height == main.height) {
            return;
        }

        if (history != null) {
            history.destroyBuffers();
        }

        history = new TextureTarget(main.width, main.height, false, Minecraft.ON_OSX);
        history.setFilterMode(GL30.GL_LINEAR);
        history.setClearColor(0f, 0f, 0f, 1f);
        history.clear(Minecraft.ON_OSX);

        width = main.width;
        height = main.height;
        seeded = false;

        main.bindWrite(false);
    }

    private void blendHistoryOnto(float alpha) {
        Matrix4f projectionBackup = new Matrix4f(RenderSystem.getProjectionMatrix());
        VertexSorting sortingBackup = RenderSystem.getVertexSorting();
        Matrix4f modelView = RenderSystem.getModelViewMatrix();
        Matrix4f modelViewBackup = new Matrix4f(modelView);

        RenderSystem.setProjectionMatrix(new Matrix4f(), VertexSorting.ORTHOGRAPHIC_Z);
        modelView.identity();

        RenderSystem.disableDepthTest();
        RenderSystem.depthMask(false);
        RenderSystem.enableBlend();
        RenderSystem.defaultBlendFunc();
        RenderSystem.setShader(GameRenderer::getPositionTexShader);
        RenderSystem.setShaderTexture(0, history.getColorTextureId());
        RenderSystem.setShaderColor(1f, 1f, 1f, alpha);

        BufferBuilder buffer = Tesselator.getInstance()
                .begin(VertexFormat.Mode.QUADS, DefaultVertexFormat.POSITION_TEX);

        buffer.addVertex(-1f, -1f, 0f).setUv(0f, 0f);
        buffer.addVertex(1f, -1f, 0f).setUv(1f, 0f);
        buffer.addVertex(1f, 1f, 0f).setUv(1f, 1f);
        buffer.addVertex(-1f, 1f, 0f).setUv(0f, 1f);

        BufferUploader.drawWithShader(buffer.buildOrThrow());

        RenderSystem.setShaderColor(1f, 1f, 1f, 1f);
        RenderSystem.disableBlend();
        RenderSystem.depthMask(true);
        RenderSystem.enableDepthTest();

        modelView.set(modelViewBackup);
        RenderSystem.setProjectionMatrix(projectionBackup, sortingBackup);
    }

    private void copyIntoHistory(RenderTarget main) {
        GlStateManager._glBindFramebuffer(GL30.GL_READ_FRAMEBUFFER, main.frameBufferId);
        GlStateManager._glBindFramebuffer(GL30.GL_DRAW_FRAMEBUFFER, history.frameBufferId);

        GlStateManager._glBlitFrameBuffer(
                0, 0, width, height,
                0, 0, width, height,
                GL30.GL_COLOR_BUFFER_BIT, GL30.GL_NEAREST);

        main.bindWrite(false);
    }
}
