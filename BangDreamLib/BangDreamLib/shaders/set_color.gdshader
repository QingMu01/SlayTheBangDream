shader_type canvas_item;

uniform vec4 target_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);

void fragment() {
	vec4 texture_color = texture(TEXTURE, UV);
	float gray = dot(texture_color.rgb, vec3(0.299, 0.587, 0.114));
	COLOR = vec4(target_color.rgb * gray, texture_color.a * target_color.a);
}

