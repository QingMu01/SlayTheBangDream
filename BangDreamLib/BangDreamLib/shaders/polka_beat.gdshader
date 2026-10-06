shader_type canvas_item;
render_mode blend_add, unshaded;

// 波点律动着色器：把一张网点贴图与程序化点阵合成，再按节拍脉冲与向外波前重新着色。
// 之所以必须用着色器：逐像素的节拍波前、主题色重着色、命中时的径向飞散，
// 这三件事用精灵摆放做不到。尤其源网点贴图本身是青色的，用 modulate 染不成主题色，
// 因此这里只取它的 alpha 作「网点浓度」，颜色完全由 tint 决定。

// 网点贴图：只取 alpha。未绑定时引擎传入默认纹理（alpha = 1），
// halftone 恒为 1，退化为纯程序化点阵，不报错。
uniform sampler2D dot_pattern : filter_linear;
uniform float pattern_weight : hint_range(0.0, 1.0) = 0.55;

// 程序化点阵：cells × cells 个格子，每格中心一个点。
uniform float cells : hint_range(2.0, 32.0) = 13.0;
uniform float dot_radius : hint_range(0.02, 0.5) = 0.17;
uniform float dot_softness : hint_range(0.005, 0.3) = 0.04;

// 每格抖动与点径差异：打破死板的屏幕点阵，读作手绘波点。
uniform float jitter_amount : hint_range(0.0, 0.5) = 0.22;
uniform float size_variation : hint_range(0.0, 1.0) = 0.45;

// 向外传播的波前：位于 wave_radius 附近的点被放大并加亮。
uniform float wave_radius : hint_range(0.0, 1.2) = 0.0;
uniform float wave_width : hint_range(0.02, 0.6) = 0.12;
uniform float wave_strength : hint_range(0.0, 3.0) = 1.8;

// 当前拍的脉冲：拍点 = 1，拍尾衰减到 0。
uniform float beat_pulse : hint_range(0.0, 1.0) = 0.0;

// 命中后点阵向外飞散量（0 = 原地，1 = 完全散开）。
uniform float scatter : hint_range(0.0, 1.0) = 0.0;

// 扩张前沿：半径之外的点完全透明。点阵场的「变大」靠的是这条前沿向外推开
// （透明度变化），而不是把整块贴图缩放——缩放只会让点一起变大，读不出扩张。
// 场景里的作者默认值取「前沿推到底」的满场（0.48），否则编辑器里只剩空场、无法摆位。
uniform float reveal_radius : hint_range(0.0, 0.9) = 0.48;

// 前沿边缘柔度。别调大：它同时压缩了「完全不透明」的核心区，
// 0.14 时前沿 0.18 只剩半径 20px 的实心核心，整场看着像没显示。
uniform float reveal_softness : hint_range(0.01, 0.4) = 0.08;

// 整场不透明度增益：随拍呼吸（拍点最实、拍内回落）。
uniform float field_alpha : hint_range(0.0, 1.5) = 1.0;

// 亮度倍率（命中瞬间冲高）。
uniform float brightness : hint_range(0.0, 4.0) = 1.0;

// 主题色；alpha 同时充当整段特效的包络（淡入 / 收尾）。
uniform vec4 tint : source_color = vec4(1.0, 1.0, 1.0, 1.0);

/// 格坐标 → 0..1 的确定性哈希，用于抖动与点径差异。
float hash21(vec2 p) {
	return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

void fragment() {
	vec2 p = UV - 0.5;
	float dist = length(p);

	// 飞散：格坐标随距离外扩，读作点阵被冲散。
	vec2 grid_uv = p / (1.0 + scatter * dist * 3.0);
	vec2 cell_id = floor((grid_uv + 0.5) * cells);
	float h = hash21(cell_id);

	// 每格抖动中心与点径，避免整齐划一的屏幕点阵。
	vec2 jitter = vec2(h, hash21(cell_id + 17.0)) - 0.5;
	vec2 cell = fract((grid_uv + 0.5) * cells) - 0.5 - jitter * jitter_amount;
	float radius = dot_radius * (1.0 + (h - 0.5) * size_variation);
	float lattice = 1.0 - smoothstep(max(radius - dot_softness, 0.001), radius, length(cell));

	// 网点贴图：alpha 越高处点越实，形成中心密、外缘疏的浓度分布。
	float halftone = smoothstep(0.3, 0.7, texture(dot_pattern, UV).a);
	float mask = lattice * mix(1.0, halftone, pattern_weight);

	// 扩张前沿：前沿之内可见，之外完全透明，边缘按 reveal_softness 柔化。
	float reveal = 1.0 - smoothstep(reveal_radius - reveal_softness, reveal_radius, dist);

	// 波前：距波前越近增益越高。
	float band = exp(-pow((dist - wave_radius) / wave_width, 2.0));
	float gain = 1.0 + band * wave_strength * beat_pulse;

	float alpha = mask * clamp(gain, 0.0, 3.0) * reveal * field_alpha * tint.a;

	COLOR = vec4(tint.rgb * brightness, alpha);
}