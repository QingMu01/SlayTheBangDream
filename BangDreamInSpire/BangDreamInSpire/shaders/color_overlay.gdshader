shader_type canvas_item;

uniform vec3 overlay_color : source_color = vec3(1.0, 0.0, 0.0);

// 亮度低于此值的区域被视为「暗区」
uniform float dark_threshold : hint_range(0.0, 1.0) = 0.2;
// 明暗过渡的平滑宽度（越大过渡越柔和）
uniform float dark_falloff   : hint_range(0.001, 0.5) = 0.2;
// 暗区的叠加强度倍率（0 = 暗区完全不上色，1 = 与亮区一致）
uniform float dark_intensity : hint_range(0.0, 1.0) = 0.0;

float overlay_channel(float base, float over) {
    return (base < 0.5)
        ? 2.0 * base * over
        : 1.0 - 2.0 * (1.0 - base) * (1.0 - over);
}

vec3 overlay_blend(vec3 base, vec3 over) {
    return vec3(
        overlay_channel(base.r, over.r),
        overlay_channel(base.g, over.g),
        overlay_channel(base.b, over.b)
    );
}

float get_luminance_hsl(vec3 c) {
    float c_max = max(c.r, max(c.g, c.b));
    float c_min = min(c.r, min(c.g, c.b));
    return (c_max + c_min) * 0.5;
}

void fragment() {
    vec4 tex = texture(TEXTURE, UV);

    vec3 color = tex.rgb;

    float lum = get_luminance_hsl(color);
    color = vec3(lum);

    float mask = smoothstep(dark_threshold - dark_falloff,
                            dark_threshold + dark_falloff, lum);

    float blend_strength = mix(dark_intensity, 1.0, mask);

    vec3 tinted = overlay_blend(color, overlay_color);
    color = mix(color, tinted, blend_strength);

    color = clamp(color, 0.0, 1.0);

    COLOR = vec4(color, tex.a);
}