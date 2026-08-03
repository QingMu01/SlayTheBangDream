shader_type canvas_item;
render_mode unshaded, blend_add;

instance uniform vec2 rect_size = vec2(1.0);
instance uniform float reveal_progress : hint_range(0.0, 1.0, 0.001) = 1.0;

uniform float slant_slope : hint_range(-1.0, 1.0, 0.01) = 0.3;
uniform float edge_softness : hint_range(0.0, 4.0, 0.1) = 0.0;
uniform float sweep_band_width : hint_range(1.0, 60.0, 0.5) = 18.0;
uniform float sweep_softness : hint_range(0.0, 8.0, 0.1) = 2.0;

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
	float shape_smoothing = fwidth(edge_distance) + edge_softness;
	float shape_mask = smoothstep(-shape_smoothing, shape_smoothing, edge_distance);

	float half_band_width = min(sweep_band_width * 0.5, shape_width * 0.5);
	float sweep_center = left + reveal_progress * shape_width;
	float distance_to_band = abs(local_position.x - sweep_center);
	float band_smoothing = max(fwidth(distance_to_band), 0.5) + sweep_softness;
	float band_mask = 1.0 - smoothstep(
		max(half_band_width - band_smoothing, 0.0),
		half_band_width + band_smoothing,
		distance_to_band
	);
	float entrance_fade_end = min(
		(half_band_width + band_smoothing) / shape_width,
		1.0
	);
	float entrance_fade = smoothstep(0.0, entrance_fade_end, reveal_progress);
	float exit_fade_start = max(
		1.0 - (half_band_width + band_smoothing) / shape_width,
		0.0
	);
	float exit_fade = 1.0 - smoothstep(exit_fade_start, 1.0, reveal_progress);

	COLOR = vec4(vec3(1.0), vertex_color.a);
	COLOR.a *= shape_mask * band_mask * entrance_fade * exit_fade;
}
