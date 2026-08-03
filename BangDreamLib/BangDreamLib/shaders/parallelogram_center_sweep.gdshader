shader_type canvas_item;

instance uniform vec2 rect_size = vec2(1.0);
instance uniform float reveal_progress : hint_range(0.0, 1.0, 0.001) = 0.0;

uniform float slant_slope : hint_range(-1.0, 1.0, 0.01) = 0.3;
uniform float edge_softness : hint_range(0.0, 4.0, 0.1) = 0.0;
uniform float sweep_band_width : hint_range(1.0, 60.0, 0.5) = 18.0;
uniform float sweep_edge_glow : hint_range(0.0, 30.0, 0.5) = 8.0;

varying vec2 local_position;
varying vec4 vertex_color;

void vertex() {
    local_position = VERTEX;
    vertex_color = COLOR;
}

void fragment() {
    vec2 safe_size = max(rect_size, vec2(0.0001));
    float normalized_y = clamp(local_position.y / safe_size.y, 0.0, 1.0);
    float slant_offset = slant_slope * safe_size.y;
    float shape_width = max(safe_size.x - abs(slant_offset), 0.0001);
    float center = safe_size.x * 0.5 + slant_offset * (0.5 - normalized_y);
    float left = center - shape_width * 0.5;
    float right = center + shape_width * 0.5;

    float edge_distance = min(local_position.x - left, right - local_position.x);
    float smoothing = fwidth(edge_distance) + edge_softness;
    float mask = smoothstep(-smoothing, smoothing, edge_distance);

    float sweep_distance = abs(local_position.x - center);
    float half_band_width = min(sweep_band_width * 0.5, shape_width * 0.5);
    float sweep_center = reveal_progress * (shape_width * 0.5 + half_band_width + edge_softness + 1.0);
    float distance_to_band = abs(sweep_distance - sweep_center);
    float sweep_smoothing = max(fwidth(distance_to_band), 0.5) + edge_softness;
    float band_mask = 1.0 - smoothstep(
        max(half_band_width - sweep_smoothing, 0.0),
        half_band_width + sweep_smoothing,
        distance_to_band
    );
    float glow_inner = max(half_band_width - sweep_edge_glow, 0.0);
    float glow_mask = (1.0 - smoothstep(glow_inner, max(half_band_width, 0.0001), distance_to_band)) * band_mask;

    vec4 texture_color = texture(TEXTURE, UV);
    COLOR = texture_color * vertex_color;
    COLOR.rgb *= 1.0 + glow_mask;
    COLOR.a *= mask * band_mask;
}
