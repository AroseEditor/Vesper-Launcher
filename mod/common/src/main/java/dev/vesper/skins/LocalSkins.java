package dev.vesper.skins;

import com.mojang.blaze3d.platform.NativeImage;
import net.minecraft.client.Minecraft;
import net.minecraft.client.renderer.texture.DynamicTexture;
import net.minecraft.client.resources.PlayerSkin;
import net.minecraft.resources.ResourceLocation;

import java.io.InputStream;
import java.nio.file.Files;
import java.nio.file.Path;

public final class LocalSkins {

    public static final String FOLDER = "vesper";
    public static final String SKINS = "skins";

    private static ResourceLocation skinTexture;
    private static ResourceLocation capeTexture;
    private static PlayerSkin.Model model = PlayerSkin.Model.WIDE;
    private static boolean loaded;

    private LocalSkins() {
    }

    public static void load(Path gameDirectory) {
        skinTexture = null;
        capeTexture = null;
        model = PlayerSkin.Model.WIDE;

        Path directory = gameDirectory.resolve(FOLDER).resolve(SKINS);

        if (!Files.isDirectory(directory)) {
            loaded = true;
            return;
        }

        skinTexture = register(directory.resolve("skin.png"), "local_skin");
        capeTexture = register(directory.resolve("cape.png"), "local_cape");
        model = readModel(directory.resolve("model.txt"));
        loaded = true;
    }

    public static boolean hasSkin() {
        return skinTexture != null;
    }

    public static boolean isLoaded() {
        return loaded;
    }

    public static PlayerSkin apply(PlayerSkin original) {
        if (original == null || skinTexture == null) {
            return original;
        }

        return new PlayerSkin(
                skinTexture,
                original.textureUrl(),
                capeTexture != null ? capeTexture : original.capeTexture(),
                capeTexture != null ? capeTexture : original.elytraTexture(),
                model,
                original.secure());
    }

    private static ResourceLocation register(Path path, String name) {
        if (!Files.isRegularFile(path)) {
            return null;
        }

        try (InputStream stream = Files.newInputStream(path)) {
            NativeImage image = NativeImage.read(stream);
            DynamicTexture texture = new DynamicTexture(image);
            ResourceLocation id = ResourceLocation.fromNamespaceAndPath(FOLDER, SKINS + "/" + name);

            Minecraft.getInstance().getTextureManager().register(id, texture);
            return id;
        } catch (Exception e) {
            return null;
        }
    }

    private static PlayerSkin.Model readModel(Path path) {
        try {
            if (Files.isRegularFile(path)
                    && Files.readString(path).trim().equalsIgnoreCase("slim")) {
                return PlayerSkin.Model.SLIM;
            }
        } catch (Exception ignored) {
            return PlayerSkin.Model.WIDE;
        }

        return PlayerSkin.Model.WIDE;
    }
}
