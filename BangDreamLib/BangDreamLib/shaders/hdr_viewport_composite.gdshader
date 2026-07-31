shader_type canvas_item;
render_mode unshaded, blend_add;

varying vec4 vertex_color;

vec3 convert_linear_to_srgb(vec3 color) {
	vec3 linear_segment = color * 12.92;
	vec3 gamma_segment = 1.055 * pow(color, vec3(1.0 / 2.4)) - 0.055;
	return mix(linear_segment, gamma_segment, step(vec3(0.0031308), color));
}

void vertex() {
	vertex_color = COLOR;
}

void fragment() {
	vec4 viewport_color = texture(TEXTURE, UV);
	vec3 srgb = convert_linear_to_srgb(max(viewport_color.rgb, vec3(0.0)));
	COLOR = vec4(srgb * vertex_color.rgb, 1.0);
}
