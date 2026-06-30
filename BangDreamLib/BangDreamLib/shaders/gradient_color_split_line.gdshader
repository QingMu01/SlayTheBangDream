shader_type canvas_item;

uniform sampler2D gradient_color;
uniform sampler2D character_texture;

uniform int line_count : hint_range(0, 10, 1) = 5;
uniform vec3 line_color :source_color = vec3(1.);
uniform float line_width : hint_range(0., 0.2, 0.005) = 0.005;
uniform float line_angle : hint_range(-3.14, 3.14, 0.1) = -0.2;
uniform float line_spacing : hint_range(0.01, 2.0, 0.01) = 0.15;
uniform float line_offset : hint_range(-1.0, 1.0, 0.01) = 0.6; 
uniform float line_smoothing : hint_range(0.0, 0.05, 0.001) = 0.003;

vec3 shadow_and_line(vec2 uv){
	vec2 uv_center = 2.0 * uv - 1.0;
    float slope = tan(line_angle);
    float base_line = uv_center.y - slope * uv_center.x;
    float norm_factor = sqrt(1.0 + slope * slope);

    float min_dist = 1e10;
    for (int i = 0; i < line_count; i++) {
        float offset = -1.0 + line_offset + float(i) * line_spacing;
        float line_i = base_line - offset;
        float dist_i = abs(line_i) / norm_factor;
        min_dist = min(min_dist, dist_i);
    }

    float feather = line_smoothing;
    float edge_start = max(0.0, line_width - feather);
    float edge_end = line_width + feather;
    return vec3(1.0 - smoothstep(edge_start, edge_end, min_dist));
}

void fragment() {
    vec4 gradient = texture(gradient_color, vec2(UV.y, UV.x));
    COLOR.rgb = mix(gradient.rgb, line_color, shadow_and_line(UV));
}