package dev.vesper.skins;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Locale;
import java.util.Map;
import java.util.Optional;
import java.util.concurrent.ConcurrentHashMap;

public final class SkinResolver {

    public static final String PROFILE_BY_NAME = "https://api.mojang.com/users/profiles/minecraft/";
    public static final String SESSION_PROFILE = "https://sessionserver.mojang.com/session/minecraft/profile/";

    private final Path skinDirectory;
    private final Map<String, Optional<Path>> cache = new ConcurrentHashMap<>();

    public SkinResolver(Path skinDirectory) {
        this.skinDirectory = skinDirectory;
    }

    public Optional<Path> localSkin(String username) {
        return cache.computeIfAbsent(key(username), name -> {
            Path direct = skinDirectory.resolve(name + ".png");

            if (Files.isRegularFile(direct)) {
                return Optional.of(direct);
            }

            Path own = skinDirectory.resolve("skin.png");

            if (Files.isRegularFile(own)) {
                return Optional.of(own);
            }

            return Optional.empty();
        });
    }

    public Optional<Path> localCape(String username) {
        Path cape = skinDirectory.resolve(key(username) + "_cape.png");

        if (Files.isRegularFile(cape)) {
            return Optional.of(cape);
        }

        Path own = skinDirectory.resolve("cape.png");
        return Files.isRegularFile(own) ? Optional.of(own) : Optional.empty();
    }

    public boolean hasLocalOverride(String username) {
        return localSkin(username).isPresent();
    }

    public void forget(String username) {
        cache.remove(key(username));
    }

    public void clear() {
        cache.clear();
    }

    public static String key(String username) {
        return username == null ? "" : username.toLowerCase(Locale.ROOT);
    }

    public static String profileLookupUrl(String username) {
        return PROFILE_BY_NAME + key(username);
    }

    public static String textureLookupUrl(String uuid) {
        return SESSION_PROFILE + uuid.replace("-", "");
    }
}
