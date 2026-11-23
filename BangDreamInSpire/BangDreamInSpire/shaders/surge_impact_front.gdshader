shader_type canvas_item;
render_mode blend_add;

// 压平波前（多道叠层）：主带 + 若干道更暗的跟随带一起朝目标表面压过去，压平后主带留在表面急颤。
// 叠层是「2^i 道一起去砸」在命中幕的唯一载体；冲击尺寸与亮度由脚本按阶数给。见 docs/mechanic-authoring.md 12.4。

instance uniform vec2 quad_size = vec2(700.0, 500.0);

// 弯曲量（px）：归零即压平成竖线。
instance uniform float bow = 46.0;

// 主带位置（px，quad 中心为原点）：沿 +x 方向偏移。
instance uniform float offset = 0.0;

// 带宽（px）与软硬。
instance uniform float thickness = 20.0;
instance uniform float softness = 1.0;

// 叠层道数（1..8）与相邻间距（px）：第 k 道在主带之后 k * stack_gap。
instance uniform float stack_count = 1.0;
instance uniform float stack_gap = 24.0;

// 跟随带相对主带的亮度衰减。
instance uniform float stack_decay = 0.20;

// 上下端的纵向羽化（0..1，按 quad 高度的比例）。
instance uniform float vertical_fade = 0.30;

instance uniform float intensity = 1.0;

instance uniform vec4 crest_color : source_color = vec4(0.86, 0.93, 1.0, 1.0);
instance uniform float peak_alpha = 0.95;

const int MaxStack = 8;

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

	float bands = 0.0;
	int count = int(clamp(stack_count, 1.0, float(MaxStack)));
	for (int k = 0; k < MaxStack; k++) {
		if (k >= count) {
			break;
		}
		float distance = abs(centered.x - offset - curve + float(k) * stack_gap);
		float band = 1.0 - smoothstep(thickness * 0.35, thickness * (0.35 + softness), distance);
		bands += band * pow(1.0 - stack_decay, float(k));
	}

	// 纵向羽化：上下端淡出。
	float vertical = 1.0 - smoothstep(1.0 - vertical_fade, 1.0, abs(normalized_y));

	float alpha = bands * vertical * intensity * peak_alpha;

	// 画布边缘羽化。
	float border = min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y));
	alpha *= smoothstep(0.0, 0.06, border);

	COLOR = vec4(crest_color.rgb, SaturateAlpha(alpha));
}
