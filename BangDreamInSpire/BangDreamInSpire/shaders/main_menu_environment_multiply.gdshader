shader_type canvas_item;
render_mode unshaded;

// 环境色为固定值（原为实时屏幕取样），A 用于渐变起点，B 用于渐变终点。
uniform vec3 color_a : source_color = vec3(0.211765, 0.188235, 0.247059); // #36303F
uniform vec3 color_b : source_color = vec3(0.250980, 0.274510, 0.313725); // #404650
uniform vec2 gradient_start_uv = vec2(0.5, 0.0);
uniform vec2 gradient_end_uv = vec2(0.5, 1.0);
uniform float multiply_strength : hint_range(0.0, 1.0, 0.01) = 0.3;

void fragment() {
    vec4 sprite = texture(TEXTURE, UV) * COLOR;

    vec2 gradient_axis = gradient_end_uv - gradient_start_uv;
    float gradient_axis_length_squared = dot(gradient_axis, gradient_axis);
    float gradient_position = gradient_axis_length_squared > 0.000001
        ? clamp(dot(UV - gradient_start_uv, gradient_axis) / gradient_axis_length_squared, 0.0, 1.0)
        : 0.0;
    vec3 environment_color = mix(color_a, color_b, gradient_position);

    // HDR 2D colors may exceed 1.0; clamping keeps the operation as multiply rather than brightening.
    environment_color = min(environment_color, vec3(1.0));
    vec3 tint = mix(vec3(1.0), environment_color, multiply_strength);
    COLOR = vec4(sprite.rgb * tint, sprite.a);
}
