shader_type canvas_item;

instance uniform vec2 rect_size = vec2(1.0);
instance uniform vec4 dot_color : source_color = vec4(1.0, 1.0, 1.0, 0.3);
instance uniform float reveal_progress : hint_range(0.0, 1.0, 0.001) = 1.0;

uniform float slant_slope : hint_range(-1.0, 1.0, 0.01) = 0.3;
uniform float edge_softness : hint_range(0.0, 4.0, 0.1) = 0.0;
uniform float edge_coverage_power : hint_range(0.1, 1.0, 0.05) = 0.25;
uniform sampler2D gradient_texture : source_color, repeat_disable, filter_linear;
uniform float gradient_angle : hint_range(-180.0, 180.0, 1.0) = 0.0;
uniform float dot_radius : hint_range(0.0, 8.0, 0.1) = 1.5;
uniform float dot_spacing : hint_range(1.0, 32.0, 0.5) = 6.5;
uniform vec2 dot_offset = vec2(0.0);

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
	float base_shape_mask = smoothstep(-smoothing, smoothing, edge_distance);
	float shape_mask = pow(base_shape_mask, edge_coverage_power);

	float reveal_edge = reveal_progress * safe_size.x;
	float reveal_smoothing = max(fwidth(local_position.x), 0.001);
	float reveal_mask = 1.0 - smoothstep(reveal_edge, reveal_edge + reveal_smoothing, local_position.x);

	float angle = radians(gradient_angle);
	vec2 gradient_direction = vec2(cos(angle), sin(angle));
	float projection_range = abs(gradient_direction.x) + abs(gradient_direction.y);
	float gradient_weight = dot(local_position / safe_size - 0.5, gradient_direction) /
			projection_range + 0.5;
	vec4 gradient_color = texture(
			gradient_texture,
			vec2(clamp(gradient_weight, 0.0, 1.0), 0.5)
	);

	vec2 dot_cell = mod(local_position - dot_offset + dot_spacing * 0.5, dot_spacing) -
			dot_spacing * 0.5;
	float dot_distance = length(dot_cell);
	float dot_smoothing = fwidth(dot_distance);
	float dot_mask = 1.0 - smoothstep(
			dot_radius - dot_smoothing,
			dot_radius + dot_smoothing,
			dot_distance
	);
	float dot_alpha = dot_color.a * dot_mask * gradient_color.a;
	float overlay_alpha = gradient_color.a + dot_alpha * (1.0 - gradient_color.a);
	vec3 overlay_color = mix(gradient_color.rgb, dot_color.rgb, dot_alpha);

	COLOR = vec4(overlay_color, overlay_alpha) * vertex_color;
	COLOR.a *= shape_mask * reveal_mask;
}
