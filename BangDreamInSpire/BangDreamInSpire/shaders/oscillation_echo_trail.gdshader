shader_type canvas_item;
render_mode blend_add;

// 回波拖尾：一条从球后方散开的锥形尾迹。
// 刻意不用「拉伸一张光斑贴图」那种做法 —— 那样每一段等宽等亮，读成一根横杠。
// 这里沿航向二次收细、亮度按 (1-t)^taper_power 衰减，短拖尾才有甩出去的方向感。
// 局部 +x 指向飞行方向，球在 +x 端，尾巴向 -x 展开。
// 见 docs/mechanic-authoring.md「震荡」章节。

instance uniform vec2 quad_size = vec2(300.0, 100.0);
instance uniform float strength = 1.0;
instance uniform vec4 tint = vec4(0.48, 0.70, 1.0, 1.0);

// 衰减指数：越大尾越短越干脆。
uniform float taper_power = 3.0;

// 尾端收细程度（尾端宽度 / 根部宽度）。
uniform float tip_ratio = 0.07;

// 软饱和：alpha 一旦硬 clamp 到 1，加色层会在「恰好等于 1」的位置留下一条等值线硬边
// （球心看起来像被切开一条缝，且边缘成锯齿）。0.85 以下保持原样，之上平滑收敛到 1。
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
	vec2 p = (UV - 0.5) * quad_size;
	float half_x = max(quad_size.x * 0.5, 0.0001);
	float half_y = max(quad_size.y * 0.5, 0.0001);

	// x = +1 在球那一端，-1 在尾端。
	float x = p.x / half_x;
	float back = clamp((1.0 - x) * 0.5, 0.0, 1.0);

	float along = pow(max(1.0 - back, 0.0), taper_power);
	float width = mix(0.86, tip_ratio, pow(back, 0.7));
	float lateral = exp(-pow(p.y / (half_y * width), 2.0));

	// 前端淡出：拖尾根部埋在球体里，但画布的前边缘不能留一条硬切。
	float head = smoothstep(1.0, 0.86, x);

	// 画布边缘羽化：任何一条边都不许被方形画布切出直边。
	float border = min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y));
	float edge = smoothstep(0.0, 0.10, border);

	float alpha = along * lateral * head * edge * strength;
	COLOR = vec4(tint.rgb, SaturateAlpha(alpha));
}
