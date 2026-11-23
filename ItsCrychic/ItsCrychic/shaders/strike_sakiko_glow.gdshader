shader_type canvas_item;
render_mode blend_add;

// 用原图亮度重建蓝紫辉光；不能改用 CanvasItemMaterial(ADD) + 紫色 modulate，
// 否则乘法染色会在线性 2D 混合中把粉色边压成偏红的灰紫。
uniform vec3 glow_color : source_color = vec3(0.541176, 0.498039, 0.815686);
uniform float intensity : hint_range(0.5, 2.5) = 1.2;

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	float mx = max(max(tex.r, tex.g), tex.b);
	vec4 modulate = COLOR;
	COLOR = vec4(glow_color * mx * intensity * modulate.rgb, tex.a * modulate.a);
}
