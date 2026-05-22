shader_type canvas_item;

uniform float offset_up : hint_range(1.0, 5.0, 0.1) = 1.0f;
uniform float offset_left : hint_range(0.0, 1.0, 0.1) = 0.1f;
uniform float offset_down : hint_range(0.0, 1.0, 0.1) = 0.1f;
uniform float offset_rigth : hint_range(1.0, 5.0, 0.1) = 1.0f;

uniform vec4 color : source_color = vec4(1.0f, 0.0f, 0.0f, 0.5f);

void fragment() {
	vec2 uv = UV;
	vec2 uv_up = uv + vec2(0, TEXTURE_PIXEL_SIZE.y) * offset_up;
	vec2 uv_down = uv + vec2(0, -TEXTURE_PIXEL_SIZE.y) * offset_down;
	vec2 uv_left = uv + vec2(TEXTURE_PIXEL_SIZE.x, 0) * offset_left;
	vec2 uv_right = uv + vec2(-TEXTURE_PIXEL_SIZE.x, 0) * offset_rigth;

	vec4 color_up = texture(TEXTURE, uv_up);
	vec4 color_down = texture(TEXTURE, uv_down);
	vec4 color_left = texture(TEXTURE, uv_left);
	vec4 color_right = texture(TEXTURE, uv_right);

	vec4 original_color = texture(TEXTURE, UV);

	vec4 outline = color_up + color_right + color_left + color_down;
	outline.rgb = color.rgb;

	COLOR = mix(outline, original_color, original_color.a);
}

