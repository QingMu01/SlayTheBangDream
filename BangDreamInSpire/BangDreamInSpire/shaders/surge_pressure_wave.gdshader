shader_type canvas_item;
render_mode blend_add;

// 回授波包：前缘之后是 2^i 道等幅平行波前（0 道时就是震荡的单道前缘），整列末尾再拖一条衰减尾迹。
// 与 oscillation_pressure_wave 的分工：那边只有"一道前缘＋越跑越弱的尾峰"（衰减），
// 这边把尾峰换成越叠越厚的等幅前列（倍增）。见 docs/mechanic-authoring.md 12.4。

// 覆盖整个波包的方形 quad 尺寸（px），由脚本按最大半径设置。
instance uniform vec2 quad_size = vec2(2048.0, 2048.0);

// 前缘半径（px）。脚本从 0 推到目标距离。
instance uniform float front_radius = 120.0;

// 波前道数（1..8）：第 k 道半径 = front_radius - k * front_gap。
instance uniform float front_count = 1.0;

// 相邻波前间距（px）：传播中收拢，读成整列在聚焦。
instance uniform float front_gap = 46.0;

// 尾迹强度：0 道时取 1（震荡的疏密尾峰），前列越厚越弱。
instance uniform float crest_strength = 1.0;

// 定向：主方向（弧度）。
instance uniform float angle_center = 0.0;

// 波前形状：0 = 从点声源发散的球面弧，1 = 近场平面波（近乎直的一条墙）。
// 相邻两个敌人之间只有几十像素，此时球面弧会缩成一弯"月牙"；接力幕必须是平面波才读得成一道横扫的墙。
instance uniform float flatness = 0.0;

// 平面波的两端微弯（px）：完全笔直会读成一块打光板。
instance uniform float bow = 0.0;

instance uniform float intensity = 1.0;

instance uniform vec4 crest_color : source_color = vec4(0.55, 0.72, 1.0, 1.0);
instance uniform vec4 front_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
instance uniform float peak_alpha = 0.92;

// 单道波前的宽度（px）与软硬。
uniform float front_width = 20.0;
uniform float front_softness = 1.0;

// 后道相对前缘的亮度衰减（0 = 等亮）。
uniform float front_decay = 0.06;

// 尾迹三道峰的振幅与间距（相对整列末尾）。
uniform vec4 crest_amplitude = vec4(0.88, 0.58, 0.32, 0.0);
uniform vec4 crest_spacing = vec4(62.0, 132.0, 216.0, 0.0);
uniform float crest_softness = 1.0;

// 前缘亮线的附加强度。
uniform float front_boost = 1.05;

// 纵向压扁：< 1 时弧变成贴地的椭圆弧。
uniform float squash = 0.80;

// 前缘半张角与后段收窄。
uniform float angle_half = 0.94;
uniform float angle_taper = 0.42;

// 纵向条带半高（px，0 = 不限）。
instance uniform float band_half_height = 330.0;

const int MaxFronts = 8;

float Gaussian(float x, float center, float width)
{
	float d = (x - center) / max(width, 0.0001);
	return exp(-d * d);
}

float AngleDelta(float a, float b)
{
	return atan(sin(a - b), cos(a - b));
}

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
	// 像素坐标（quad 中心为原点，y 向下）。
	vec2 centered = (UV - 0.5) * quad_size;
	vec2 metric = vec2(centered.x, centered.y / squash);
	float radius = length(metric);
	float angle = atan(metric.y, metric.x);

	// 纵深（正值 = 波已扫过）：球面波按半径算，平面波按 x 投影算。
	// 两种形状分开求强度再按 flatness 交叉淡化——直接对纵深做 mix 会得到一条闭合的等值线（读成一个椭圆环）。
	float normalized_y = centered.y / max(quad_size.y * 0.5, 0.0001);
	float depth_radial = front_radius - radius;
	float depth_flat = front_radius - centered.x + bow * (1.0 - normalized_y * normalized_y);

	int count = int(clamp(front_count, 1.0, float(MaxFronts)));
	float last_offset = float(count - 1) * front_gap;
	float span = max(last_offset + crest_spacing.z, 1.0);

	float bands_radial = 0.0;
	float bands_flat = 0.0;
	for (int k = 0; k < MaxFronts; k++) {
		if (k >= count) {
			break;
		}
		float at = float(k) * front_gap;
		// 半径减不过去的列必须丢掉，否则会翻到声源另一侧。
		if (at >= front_radius) {
			break;
		}
		float weight = pow(1.0 - front_decay, float(k));
		float width = front_width * front_softness;
		bands_radial += Gaussian(depth_radial, at, width) * weight;
		bands_flat += Gaussian(depth_flat, at, width) * weight;
	}

	// 尾迹只贴在整列末尾之后：0 道时它就是震荡的那串疏密峰。
	float crest_radial = 0.0;
	float crest_flat = 0.0;
	if (last_offset + crest_spacing.x < front_radius) {
		float c0 = crest_amplitude.x * Gaussian(depth_radial - last_offset, crest_spacing.x, 14.0 * crest_softness);
		float f0 = crest_amplitude.x * Gaussian(depth_flat - last_offset, crest_spacing.x, 14.0 * crest_softness);
		crest_radial += c0;
		crest_flat += f0;
	}
	if (last_offset + crest_spacing.y < front_radius) {
		crest_radial += crest_amplitude.y * Gaussian(depth_radial - last_offset, crest_spacing.y, 20.0 * crest_softness);
		crest_flat += crest_amplitude.y * Gaussian(depth_flat - last_offset, crest_spacing.y, 20.0 * crest_softness);
	}
	if (last_offset + crest_spacing.z < front_radius) {
		crest_radial += crest_amplitude.z * Gaussian(depth_radial - last_offset, crest_spacing.z, 28.0 * crest_softness);
		crest_flat += crest_amplitude.z * Gaussian(depth_flat - last_offset, crest_spacing.z, 28.0 * crest_softness);
	}

	// 定向展开：只有球面弧需要张角，平面波横贯整个条带。张角必须乘在球面那一侧，
	// 否则残留下来的球面分量会绕过声源，画出一整圈同心环。
	float depth_ratio = clamp(depth_radial / span, 0.0, 1.0);
	float half_angle = angle_half * (1.0 - angle_taper * depth_ratio);
	float cone = smoothstep(half_angle, half_angle * 0.42, abs(AngleDelta(angle, angle_center)));

	// 形状交叉淡化。
	float bands = mix(bands_radial * cone, bands_flat, flatness);
	float crests = mix(crest_radial * cone, crest_flat, flatness) * crest_strength;

	// 前缘亮线：比波身更利的一条。
	float front_line = mix(
			Gaussian(depth_radial, 0.0, front_width * 0.5) * cone,
			Gaussian(depth_flat, 0.0, front_width * 0.5),
			flatness) * front_boost;

	// 纵向条带：超出高度的部分淡出。
	float vertical = 1.0;
	if (band_half_height > 0.0) {
		vertical = 1.0 - smoothstep(band_half_height * 0.58, band_half_height, abs(centered.y));
	}

	// 画布边缘羽化。
	float border = min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y));
	float edge = smoothstep(0.0, 0.06, border);

	float alpha = vertical * edge * (bands + crests + front_line) * intensity * peak_alpha;

	float hot = clamp((bands + front_line) / max(bands + front_line + crests, 0.0001), 0.0, 1.0);
	vec3 tint = mix(crest_color.rgb, front_color.rgb, hot);
	COLOR = vec4(tint, SaturateAlpha(alpha));
}
