shader_type canvas_item;
render_mode blend_add;

// 余音回波球：热核 + 薄壳 + 壳内驻波 + 壳外辉光 + 两处斜向高光。
// 见 docs/mechanic-authoring.md 12.3。

// 画布尺寸（px）。球直径约为画布边长的 0.72，外圈靠辉光吃到画布边缘。
instance uniform vec2 quad_size = vec2(230.0, 230.0);

// 飞行进度 0..1：驱动呼吸与内部条纹相位。
instance uniform float phase = 0.0;

instance uniform float intensity = 1.0;

// 逐球微差：不同分音给不同色温。
instance uniform vec4 core_color = vec4(0.96, 0.98, 1.0, 1.0);
instance uniform vec4 shell_color = vec4(0.45, 0.72, 1.0, 1.0);

// 壳半径（占画布半边的比例）。
uniform float shell_radius = 0.42;

// 外辉光强度：给大了整颗球会糊成一团光。
uniform float halo_gain = 0.30;

// 热核的过曝余量：HDR 下让核亮过 1，交给场景辉光去晕开。
uniform float core_boost = 1.0;

// 内部填充：树胶球要能看见「里面」，全空会读成一个圆环。
uniform float fill_gain = 0.15;

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
	float rn = length(p) / max(quad_size.x * 0.5, 0.0001);
	float angle = atan(p.y, p.x);

	// 呼吸：壳半径随飞行轻微起伏。
	float breathe = 1.0 + 0.055 * sin(phase * 18.8495559);
	float shell_radius_now = shell_radius * breathe;

	float core = exp(-pow(rn / 0.12, 2.0));

	// 薄壳：球的唯一硬边。
	float shell = exp(-pow((rn - shell_radius_now) / 0.035, 2.0));

	// 壳内填充与一道内环：给出体积。
	float fill = exp(-pow(rn / (shell_radius_now * 0.80), 2.0)) * fill_gain;
	float inner_ring = exp(-pow((rn - shell_radius_now * 0.56) / 0.05, 2.0))
			* (0.10 + 0.06 * cos(phase * 12.0));

	float halo = exp(-pow(rn / 0.52, 2.0)) * halo_gain;

	// 斜向高光：主光斑随飞行摆动 + 对侧弱斑。
	float glint_main = exp(-pow((rn - shell_radius_now * 0.84) / 0.06, 2.0))
			* pow(max(cos(angle - 2.35 + sin(phase * 9.424778) * 0.25), 0.0), 7.0);
	float glint_weak = exp(-pow((rn - shell_radius_now * 0.62) / 0.055, 2.0))
			* pow(max(cos(angle + 1.15), 0.0), 14.0) * 0.30;

	float energy = core * core_boost + shell * 0.85 + fill + inner_ring + halo
			+ glint_main * 0.5 + glint_weak * 0.5;
	vec3 tint = core_color.rgb * (core * core_boost + glint_main * 0.5 + glint_weak * 0.5)
			+ shell_color.rgb * (shell * 0.95 + fill * 0.7 + inner_ring + halo);

	vec3 rgb = tint / max(energy, 0.0001);
	COLOR = vec4(rgb, SaturateAlpha(energy * intensity));
}
