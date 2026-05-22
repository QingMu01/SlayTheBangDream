shader_type canvas_item;
render_mode blend_add;

uniform sampler2D star_texture : repeat_enable;
uniform float rows : hint_range(1.0, 10.0, 1.0) = 4.0;
uniform float density : hint_range(1.0, 50.0, 1.0) = 15.0;
uniform float size : hint_range(0.1, 3.0, 0.1) = 0.8;

void fragment() {
	vec2 uv = UV;
	int row = int(uv.y * rows);

	uv.y = fract(uv.y * rows);
	uv.x = fract(uv.x * density);
	vec2 centered = (uv - 0.5) / size + 0.5;

	vec4 finalColor = texture(star_texture, centered);
	finalColor.rgb = finalColor.rgb * COLOR.rgb;
	finalColor.a = COLOR.a;

	if (row == 0 || row == int(rows - 1.0)) {
		finalColor *= 1.5;
	}
	COLOR = finalColor;
}