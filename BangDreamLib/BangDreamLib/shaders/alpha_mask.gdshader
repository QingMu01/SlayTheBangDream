shader_type canvas_item;
uniform sampler2D mask : repeat_enable;
uniform float mask_scale : hint_range(0.0, 2.0, 0.1);


void fragment() {
	vec4 original_texture = texture(TEXTURE, UV);

	vec2 scaled_uv = (UV - 0.5) * mask_scale + 0.5;
	vec4 mask_texture = texture(mask, scaled_uv);

	original_texture.a *= mask_texture.a;

	COLOR = original_texture;
}
