package dev.vesper.mixin;

import dev.vesper.VesperMod;
import dev.vesper.config.VesperConfig;
import dev.vesper.module.VesperModule;
import net.minecraft.client.particle.Particle;
import net.minecraft.client.particle.ParticleEngine;
import net.minecraft.client.particle.ParticleRenderType;
import org.spongepowered.asm.mixin.Final;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.Shadow;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

import java.util.Map;
import java.util.Queue;

@Mixin(ParticleEngine.class)
public class ParticleEngineMixin {

    @Shadow
    @Final
    private Map<ParticleRenderType, Queue<Particle>> particles;

    @Inject(method = "add", at = @At("HEAD"), cancellable = true)
    private void vesper$limitParticles(Particle particle, CallbackInfo callback) {
        VesperConfig config = VesperMod.config();

        if (!config.enabled(VesperModule.PARTICLE_LIMIT)) {
            return;
        }

        int limit = Math.max(64, config.particleLimit);
        int total = 0;

        for (Queue<Particle> queue : particles.values()) {
            total += queue.size();

            if (total >= limit) {
                callback.cancel();
                return;
            }
        }
    }
}
