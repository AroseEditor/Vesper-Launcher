#version 150

uniform sampler2D CurrentSampler;
uniform sampler2D HistorySampler;
uniform float Retention;

in vec2 texCoord;
out vec4 fragColor;

void main() {
    vec4 current = texture(CurrentSampler, texCoord);
    vec4 history = texture(HistorySampler, texCoord);
    fragColor = vec4(mix(current.rgb, history.rgb, Retention), 1.0);
}
