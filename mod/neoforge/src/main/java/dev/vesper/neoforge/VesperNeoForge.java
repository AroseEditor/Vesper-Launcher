package dev.vesper.neoforge;

import dev.vesper.VesperMod;
import dev.vesper.client.VesperClient;
import net.neoforged.api.distmarker.Dist;
import net.neoforged.bus.api.IEventBus;
import net.neoforged.fml.common.Mod;
import net.neoforged.fml.loading.FMLPaths;

@Mod(value = VesperMod.MOD_ID, dist = Dist.CLIENT)
public final class VesperNeoForge {

    public VesperNeoForge(IEventBus eventBus) {
        VesperMod.init(FMLPaths.GAMEDIR.get());
        VesperClient.init();
    }
}
