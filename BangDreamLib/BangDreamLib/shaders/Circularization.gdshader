shader_type canvas_item;

varying vec4 vertex_color;

uniform float smoothness : hint_range(0.0, 0.2) = 0.02;

void vertex() {
	vertex_color = COLOR;
}

void fragment() {
	vec4 tex_color = texture(TEXTURE, UV);
	float dist = distance(UV, vec2(0.5));
	float radius = 0.5;
	float alpha = 1.0 - smoothstep(radius - smoothness, radius + smoothness, dist);
	COLOR = tex_color * alpha;
}
