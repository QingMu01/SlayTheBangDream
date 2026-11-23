shader_type canvas_item;
render_mode blend_add;

// 反射驻波：表面处压力腹点，条纹间距 λ/2，亮度按拍频脉动、位置不动。
// 局部 +x 指向物体表面（节点按来波方向旋转过）。
// 见 docs/mechanic-authoring.md 12.3。

instance uniform vec2 quad_size = vec2(760.0, 620.0);

// 物体表面在局部 x 的位置（px）。
instance uniform float wall_offset = 0.0;

// 相邻亮纹间距（px，约半波长）。
instance uniform float spacing = 74.0;

// 离开表面后的衰减长度（px）。
instance uniform float fade_length = 230.0;

// 亮纹弯曲量（px）：球面波前不是平面。
instance uniform float bow = 18.0;

// 拍频包络 0..1。
instance uniform float pulse = 1.0;

// 条纹锐度：0 柔和，1 细如刻线。
instance uniform float line_sharpness = 0.55;

instance uniform float intensity = 1.0;

uniform vec4 line_color : source_color = vec4(0.9, 0.95, 1.0, 1.0);
uniform float peak_alpha = 0.9;

// 软饱和：硬 clamp 到 1 会在加色层留下等值线硬边。
float SaturateAlpha(float a)
{
	const float knee = 0.85;
	if (a <= knee) {
		return a;
	}
	return knee + (1.0 - knee) * (1.0 - exp(-(a - knee) / (1.0 - knee)));
}

void fragment()
{
	vec2 centered = (UV - 0.5) * quad_size;
	float normalized_y = centered.y / max(quad_size.y * 0.5, 0.0001);
	float curve = bow * (1.0 - normalized_y * normalized_y);

	// 离表面的距离：只在物体前方（来波一侧）有条纹。
	float distance = wall_offset - centered.x - curve;
	float envelope = step(0.0, distance) * exp(-distance / fade_length);

	// 驻波：表面处为亮纹，向外按 λ/2 交替。
	float profile = cos(distance / max(spacing, 1.0) * 6.2831853);
	float bright = pow(max(profile, 0.0), 1.0 + line_sharpness * 2.5);

	// 纵向羽化，避免矩形边。
	float vertical = 1.0 - smoothstep(0.66, 1.0, abs(normalized_y));

	float alpha = envelope * bright * vertical * (0.30 + 0.70 * pulse) * intensity * peak_alpha;

	// 画布边缘羽化。
	float border = min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y));
	alpha *= smoothstep(0.0, 0.06, border);

	COLOR = vec4(line_color.rgb, SaturateAlpha(alpha));
}
