shader_type canvas_item;

instance uniform vec2 rect_size = vec2(1.0);

uniform float slant_ratio : hint_range(-0.49, 0.49, 0.005) = 0.15;
uniform float edge_softness : hint_range(0.0, 0.1, 0.001) = 0.0;

uniform sampler2D gradient_texture : source_color, repeat_disable, filter_linear;
uniform float gradient_angle : hint_range(-180.0, 180.0, 1.0) = 0.0;

uniform vec4 dot_color : source_color = vec4(1.0, 1.0, 1.0, 0.3);
uniform float dot_radius : hint_range(0.0, 8.0, 0.1) = 1.0;
uniform float dot_spacing : hint_range(1.0, 32.0, 0.5) = 6.0;
uniform vec2 dot_offset = vec2(0.0);

varying vec2 shape_uv;
varying vec2 local_position;
varying vec4 vertex_color;

void vertex() {
	local_position = VERTEX;
	shape_uv = VERTEX / max(rect_size, vec2(0.0001));
	vertex_color = COLOR;
}

void fragment() {
	float width = 1.0 - abs(slant_ratio);
	float center = 0.5 + slant_ratio * (0.5 - shape_uv.y);
	float left = center - width * 0.5;
	float right = center + width * 0.5;

	float edge_distance = min(shape_uv.x - left, right - shape_uv.x);
	float smoothing = fwidth(edge_distance) + edge_softness;
	float mask = smoothstep(-smoothing, smoothing, edge_distance);

	float angle = radians(gradient_angle);
	vec2 gradient_direction = vec2(cos(angle), sin(angle));
	float projection_range = abs(gradient_direction.x) + abs(gradient_direction.y);
	float gradient_weight = dot(shape_uv - 0.5, gradient_direction) / projection_range + 0.5;
	vec4 gradient_color = texture(gradient_texture, vec2(clamp(gradient_weight, 0.0, 1.0), 0.5));

	vec2 dot_cell = mod(local_position - dot_offset + dot_spacing * 0.5, dot_spacing) - dot_spacing * 0.5;
	float dot_distance = length(dot_cell);
	float dot_smoothing = fwidth(dot_distance);
	float dot_mask = 1.0 - smoothstep(dot_radius - dot_smoothing, dot_radius + dot_smoothing, dot_distance);

	vec4 texture_color = texture(TEXTURE, UV);
	texture_color.rgb = mix(texture_color.rgb, dot_color.rgb, dot_color.a * dot_mask * gradient_color.a);
	texture_color.rgb = mix(texture_color.rgb, gradient_color.rgb, gradient_color.a);

	COLOR = texture_color * vertex_color;
	COLOR.a *= mask;
}
