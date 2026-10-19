shader_type canvas_item;
render_mode blend_add;

// 蓝紫辉光层：用原图亮度重建紫色，再加法叠加。
// 不能直接用 CanvasItemMaterial(ADD) + 紫色 modulate——乘法染色在 2D 线性混合空间里
// 会把原图的粉色边压成偏红的灰紫，看起来像粉色残留边。
uniform vec3 glow_color : source_color = vec3(0.541176, 0.498039, 0.815686); // #8A7FD0
uniform float intensity : hint_range(0.5, 2.5) = 1.2;

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	float mx = max(max(tex.r, tex.g), tex.b);
	vec4 modulate = COLOR;
	COLOR = vec4(glow_color * mx * intensity * modulate.rgb, tex.a * modulate.a);
}
