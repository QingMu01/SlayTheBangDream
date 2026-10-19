shader_type canvas_item;

instance uniform vec2 rect_size = vec2(1.0);
instance uniform float reveal_progress : hint_range(0.0, 1.0, 0.001) = 1.0;

uniform float slant_slope : hint_range(-1.0, 1.0, 0.01) = 0.3;
uniform float edge_softness : hint_range(0.0, 4.0, 0.1) = 0.0;

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
	float reveal_edge = reveal_progress * safe_size.x;
	float reveal_smoothing = max(fwidth(local_position.x), 0.001);
	float reveal_mask = 1.0 - smoothstep(reveal_edge, reveal_edge + reveal_smoothing, local_position.x);

	vec4 texture_color = texture(TEXTURE, UV);
	COLOR = texture_color * vertex_color;
	COLOR.a *= mask * reveal_mask;
}
