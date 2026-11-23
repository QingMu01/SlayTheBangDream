shader_type canvas_item;
render_mode blend_add;

// 疏密波包：以节点原点为声源，把压缩峰画成一组向后叠放的弧。
// 只有前缘之内才有波；峰间距、峰宽与振幅随时间推移。见 docs/mechanic-authoring.md 12.3。

// 覆盖整个波包的方形 quad 尺寸（px），由脚本按最大半径设置。
instance uniform vec2 quad_size = vec2(2048.0, 2048.0);

// 前缘半径（px）。脚本从 0 推到最大半径。
instance uniform float front_radius = 120.0;

// 第 2/3/4 道压缩峰相对第 1 道的振幅。
instance uniform vec4 crest_amplitude = vec4(0.55, 0.30, 0.14, 0.0);

// 第 2/3/4 道压缩峰与前缘的间距（px）。
instance uniform vec4 crest_spacing = vec4(104.0, 212.0, 330.0, 460.0);

// 各道压缩峰的宽度倍率。
instance uniform float crest_softness = 1.0;

// 纵向压扁：< 1 时弧变成贴地的椭圆弧。
instance uniform float squash = 0.80;

// 定向：主方向（弧度）与半张角。
instance uniform float angle_center = 0.0;
instance uniform float angle_half = 1.30;

// 张角随纵深收窄：后段比前缘窄。
instance uniform float angle_taper = 0.42;

// 纵向条带半高（px，0 = 不限）：把波包收在敌阵高度附近。
// 不夹的话弧会从演奏者上方绕过去，读成「罩子」。
instance uniform float band_half_height = 300.0;

// 前缘锐度（px）：包络到达前缘时收拢的软硬。
instance uniform float edge_softness = 10.0;

// 整体亮度。
instance uniform float intensity = 1.0;

uniform vec4 crest_color : source_color = vec4(0.82, 0.90, 1.0, 1.0);
// 前缘高光色：比波身更白。
uniform vec4 front_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float front_boost = 0.85;
uniform float front_width = 26.0;
// 波包整体的最大不透明度。
uniform float peak_alpha = 0.92;

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

	// 相对前缘的纵深：正值 = 在前缘之内（波已扫过），负值 = 前缘之外。
	float depth = front_radius - radius;

	// 前缘之后（半径更小）才是波包本体。
	// 包络由各道压缩峰自己的高斯形状给出，峰宽、间距、振幅可分开调。
	float crest = Gaussian(depth, 0.0, 9.0 * crest_softness);
	crest += crest_amplitude.x * Gaussian(depth, crest_spacing.x, 14.0 * crest_softness);
	crest += crest_amplitude.y * Gaussian(depth, crest_spacing.y, 20.0 * crest_softness);
	crest += crest_amplitude.z * Gaussian(depth, crest_spacing.z, 28.0 * crest_softness);

	// 定向展开：后段张角收窄，尾迹比前缘窄。
	float depth_ratio = clamp(depth / max(crest_spacing.z, 1.0), 0.0, 1.0);
	float half_angle = angle_half * (1.0 - angle_taper * depth_ratio);
	float angular = smoothstep(half_angle, half_angle * 0.42, abs(AngleDelta(angle, angle_center)));

	// 前缘线：比压缩峰更利的一条亮线。
	float front = Gaussian(depth, 0.0, front_width * 0.5) * front_boost;
	float engagement = smoothstep(-edge_softness, edge_softness, depth);

	// 纵向条带：超出高度的部分淡出。
	float vertical = 1.0;
	if (band_half_height > 0.0) {
		vertical = 1.0 - smoothstep(band_half_height * 0.58, band_half_height, abs(centered.y));
	}

	float alpha = engagement * angular * vertical * (crest + front) * intensity * peak_alpha;

	// 画布边缘羽化。
	float border = min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y));
	alpha *= smoothstep(0.0, 0.06, border);

	vec3 tint = mix(crest_color.rgb, front_color.rgb,
			clamp(front / max(front + crest, 0.0001), 0.0, 1.0));
	COLOR = vec4(tint, SaturateAlpha(alpha));
}
