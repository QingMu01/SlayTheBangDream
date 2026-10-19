shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear;
uniform vec2 sample_uv_a = vec2(0.35, 0.35);
uniform vec2 sample_uv_b = vec2(0.65, 0.75);
uniform vec2 gradient_start_uv = vec2(0.5, 0.0);
uniform vec2 gradient_end_uv = vec2(0.5, 1.0);
uniform float sample_radius_pixels : hint_range(0.0, 16.0, 0.5) = 4.0;
uniform float multiply_strength : hint_range(0.0, 1.0, 0.01) = 0.3;
uniform float minimum_tint : hint_range(0.0, 1.0, 0.01) = 0.12;

vec3 sample_environment(vec2 center_uv, vec2 screen_pixel_size) {
    vec2 offset = screen_pixel_size * sample_radius_pixels;
    vec3 result = vec3(0.0);

    for (int y = -1; y <= 1; y++) {
        for (int x = -1; x <= 1; x++) {
            result += texture(screen_texture, center_uv + vec2(float(x), float(y)) * offset).rgb;
        }
    }

    return result / 9.0;
}

void fragment() {
    vec4 sprite = texture(TEXTURE, UV) * COLOR;
    vec3 color_a = sample_environment(sample_uv_a, SCREEN_PIXEL_SIZE);
    vec3 color_b = sample_environment(sample_uv_b, SCREEN_PIXEL_SIZE);

    vec2 gradient_axis = gradient_end_uv - gradient_start_uv;
    float gradient_axis_length_squared = dot(gradient_axis, gradient_axis);
    float gradient_position = gradient_axis_length_squared > 0.000001
        ? clamp(dot(UV - gradient_start_uv, gradient_axis) / gradient_axis_length_squared, 0.0, 1.0)
        : 0.0;
    vec3 environment_color = mix(color_a, color_b, gradient_position);

    // HDR 2D colors may exceed 1.0; clamping keeps the operation as multiply rather than brightening.
    environment_color = clamp(environment_color, vec3(minimum_tint), vec3(1.0));
    vec3 tint = mix(vec3(1.0), environment_color, multiply_strength);
    COLOR = vec4(sprite.rgb * tint, sprite.a);
}
