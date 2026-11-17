shader_type canvas_item;
render_mode blend_add;

uniform float intensity : hint_range(0.5, 2.5) = 1.2;

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	float mx = max(max(tex.r, tex.g), tex.b);
	vec4 modulate = COLOR;
	COLOR = vec4(vec3(mx) * intensity * modulate.rgb, tex.a * modulate.a);
}
