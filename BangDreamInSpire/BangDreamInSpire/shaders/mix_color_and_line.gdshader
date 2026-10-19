shader_type canvas_item;

uniform vec4 bg_color : source_color;
uniform vec4 line_color : source_color;
uniform int line_count : hint_range(0, 10, 1);

void fragment() {
	float line_mask = 0.0;
	if (line_count > 0) {
		float half_pixel = fwidth(UV.x) * 0.1;
		float cell = 1.0 / float(line_count + 1);
		for (int i = 1; i <= line_count; i++) {
			float line_x = float(i) * cell;
			float dist = abs(UV.x - line_x);
			line_mask += 1.0 - smoothstep(half_pixel, half_pixel + fwidth(UV.x), dist);
		}
		line_mask = clamp(line_mask, 0.0, 1.0);
	}
	COLOR = mix(bg_color, line_color, line_mask);
}