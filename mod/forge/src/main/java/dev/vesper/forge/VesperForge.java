package dev.vesper.forge;

import dev.vesper.VesperMod;
import net.minecraftforge.fml.common.Mod;
import net.minecraftforge.fml.loading.FMLPaths;

@Mod(VesperMod.MOD_ID)
public final class VesperForge {

    public VesperForge() {
        VesperMod.init(FMLPaths.GAMEDIR.get());
    }
}
