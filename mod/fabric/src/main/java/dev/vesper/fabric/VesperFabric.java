package dev.vesper.fabric;

import dev.vesper.VesperMod;
import dev.vesper.client.VesperClient;
import net.fabricmc.api.ClientModInitializer;
import net.fabricmc.loader.api.FabricLoader;

public final class VesperFabric implements ClientModInitializer {

    @Override
    public void onInitializeClient() {
        VesperMod.init(FabricLoader.getInstance().getGameDir());
        VesperClient.init();
    }
}
