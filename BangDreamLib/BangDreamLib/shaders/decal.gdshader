shader_type canvas_item;
render_mode blend_add;

uniform float density : hint_range(1.0, 50.0, 1.0) = 15.0;
uniform float size : hint_range(0.1, 3.0, 0.1) = 0.8;

const float ROW_COUNT = 5.0;
const float DOT_RADIUS = 0.24;
const float LINE_HALF_WIDTH = 0.055;

float get_pixel_step(float value) {
	vec2 derivative = vec2(dFdx(value), dFdy(value));
	return max(length(derivative), 0.0001);
}

void fragment() {
	float row_uv = UV.y * ROW_COUNT;
	float row_local_y = fract(row_uv) - 0.5;
	int row = min(int(floor(row_uv)), int(ROW_COUNT) - 1);
	float shape = 0.0;

	if (row == 0 || row == int(ROW_COUNT) - 1) {
		float half_width = LINE_HALF_WIDTH * size;
		float edge_softness = get_pixel_step(row_uv);
		shape = 1.0 - smoothstep(
			half_width - edge_softness,
			half_width + edge_softness,
			abs(row_local_y));
		shape *= 1.5;
	} else {
		vec2 dot_uv = vec2(UV.x * density, row_uv);
		vec2 dot_local_uv = fract(dot_uv) - vec2(0.5);
		float x_pixel_step = get_pixel_step(dot_uv.x);
		float y_pixel_step = get_pixel_step(dot_uv.y);
		vec2 pixel_distance = dot_local_uv / vec2(x_pixel_step, y_pixel_step);
		float radius_in_pixels = DOT_RADIUS * size / y_pixel_step;
		float signed_distance = length(pixel_distance) - radius_in_pixels;
		shape = 1.0 - smoothstep(-1.0, 1.0, signed_distance);
	}

	COLOR *= shape;
}
