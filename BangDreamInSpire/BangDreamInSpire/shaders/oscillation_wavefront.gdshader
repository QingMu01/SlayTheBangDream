shader_type canvas_item;
render_mode blend_add;

// 单道压力波前：一条沿 y 弯曲的亮带，用于「波压到物体上」与反射波。
// bow 归零即把弧压平成竖线。见 docs/mechanic-authoring.md 12.3。

instance uniform vec2 quad_size = vec2(512.0, 512.0);

// 弯曲量（px）：归零即压平成竖线。
instance uniform float bow = 46.0;

// 位置（px，quad 中心为原点）：带子整体沿 x 的偏移。
instance uniform float offset = 0.0;

// 带宽（px）与软硬。
instance uniform float thickness = 22.0;
instance uniform float softness = 1.0;

// 上下端的纵向羽化（0..1，按 quad 高度的比例）。
instance uniform float vertical_fade = 0.30;

// 干涉条纹：细密横纹数量（0 = 关）。
instance uniform float fringe_count = 0.0;
instance uniform float fringe_contrast = 0.0;

instance uniform float intensity = 1.0;

uniform vec4 crest_color : source_color = vec4(0.86, 0.93, 1.0, 1.0);
uniform float peak_alpha = 0.95;

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
	// 抛物线近似球面波前：沿 y 二次弯曲。
	float normalized_y = centered.y / max(quad_size.y * 0.5, 0.0001);
	float curve = bow * (1.0 - normalized_y * normalized_y);
	float distance = abs(centered.x - offset - curve);

	float band = 1.0 - smoothstep(thickness * 0.35, thickness * (0.35 + softness), distance);

	// 纵向羽化：上下端淡出。
	float vertical = 1.0 - smoothstep(1.0 - vertical_fade, 1.0, abs(normalized_y));

	// 条纹：沿 x 的高频亮暗（只在 fringe_count > 0 时生效）。
	float fringe = 1.0;
	if (fringe_count > 0.0) {
		float ripple = 0.5 + 0.5 * cos((centered.x - offset) / quad_size.x * 6.2831853 * fringe_count);
		fringe = 1.0 - fringe_contrast + fringe_contrast * ripple;
	}

	float alpha = band * vertical * fringe * intensity * peak_alpha;

	// 画布边缘羽化。
	float border = min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y));
	alpha *= smoothstep(0.0, 0.06, border);

	COLOR = vec4(crest_color.rgb, SaturateAlpha(alpha));
}
