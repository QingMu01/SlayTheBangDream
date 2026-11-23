shader_type canvas_item;
render_mode blend_add;

// 回波拖尾：沿航向二次收细、按 (1-t)^taper_power 衰减的锥形尾迹。
// 局部 +x 指向飞行方向，球在 +x 端，尾巴向 -x 展开。
// 见 docs/mechanic-authoring.md 12.3。

instance uniform vec2 quad_size = vec2(300.0, 100.0);
instance uniform float strength = 1.0;
instance uniform vec4 tint = vec4(0.48, 0.70, 1.0, 1.0);

// 衰减指数：越大尾越短越干脆。
uniform float taper_power = 3.0;

// 尾端收细程度（尾端宽度 / 根部宽度）。
uniform float tip_ratio = 0.07;

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
	vec2 p = (UV - 0.5) * quad_size;
	float half_x = max(quad_size.x * 0.5, 0.0001);
	float half_y = max(quad_size.y * 0.5, 0.0001);

	// x = +1 在球那一端，-1 在尾端。
	float x = p.x / half_x;
	float back = clamp((1.0 - x) * 0.5, 0.0, 1.0);

	float along = pow(max(1.0 - back, 0.0), taper_power);
	float width = mix(0.86, tip_ratio, pow(back, 0.7));
	float lateral = exp(-pow(p.y / (half_y * width), 2.0));

	// 前端淡出：拖尾根部埋在球体里，但画布前边缘不能留硬切。
	float head = smoothstep(1.0, 0.86, x);

	// 画布边缘羽化。
	float border = min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y));
	float edge = smoothstep(0.0, 0.10, border);

	float alpha = along * lateral * head * edge * strength;
	COLOR = vec4(tint.rgb, SaturateAlpha(alpha));
}
