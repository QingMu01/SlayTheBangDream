shader_type canvas_item;
render_mode blend_mix, unshaded;

// 暖白纸面叠加斑驳、纤维、颗粒与边缘压痕；圆角外 alpha 归零以贴合 Panel。

uniform vec2 area_size = vec2(1420.0, 670.0);
uniform float corner_radius : hint_range(0.0, 64.0) = 3.0;

uniform vec4 paper_color : source_color = vec4(0.988, 0.980, 0.961, 1.0);
uniform vec4 fiber_color : source_color = vec4(0.902, 0.875, 0.816, 1.0);

uniform float grain_strength : hint_range(0.0, 0.2) = 0.035;
uniform float fiber_strength : hint_range(0.0, 0.4) = 0.09;
uniform float mottle_strength : hint_range(0.0, 0.3) = 0.06;
uniform float edge_strength : hint_range(0.0, 0.6) = 0.10;
uniform float edge_width : hint_range(1.0, 80.0) = 18.0;
uniform float grain_scale : hint_range(0.2, 4.0) = 1.0;
uniform vec4 rim_color : source_color = vec4(0.780, 0.749, 0.694, 1.0);
uniform float rim_strength : hint_range(0.0, 0.6) = 0.22;
uniform float opacity : hint_range(0.0, 1.0) = 1.0;

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	float a = hash12(i);
	float b = hash12(i + vec2(1.0, 0.0));
	float c = hash12(i + vec2(0.0, 1.0));
	float d = hash12(i + vec2(1.0, 1.0));
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm2(vec2 p) {
	float v = 0.0;
	float amp = 0.5;
	for (int i = 0; i < 4; i++) {
		v += amp * value_noise(p);
		p *= 2.03;
		amp *= 0.5;
	}
	return v;
}

float rounded_rect_sdf(vec2 p, vec2 half_size, float radius) {
	vec2 q = abs(p) - half_size + vec2(radius);
	return length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - radius;
}

void fragment() {
	vec2 px = UV * area_size;
	vec2 centered = px - area_size * 0.5;

	float mottle = fbm2(px * 0.0032 * grain_scale);
	float fiber = value_noise(vec2(px.x * 0.05, px.y * 0.92) * grain_scale);
	float grain = (value_noise(px * 0.85 * grain_scale) - 0.5)
			+ (value_noise(px * 2.15 * grain_scale + vec2(31.7, 11.3)) - 0.5) * 0.5;

	vec3 col = paper_color.rgb;
	col *= 1.0 - mottle_strength * (mottle - 0.5) * 2.0;
	col = mix(col, fiber_color.rgb, fiber_strength * smoothstep(0.55, 0.88, fiber));
	col += grain * grain_strength;

	float sdf = rounded_rect_sdf(centered, area_size * 0.5 - vec2(0.5), corner_radius);
	float edge = smoothstep(-edge_width, 0.0, sdf);
	col *= 1.0 - edge_strength * edge * edge;
	float rim = 1.0 - smoothstep(0.0, 2.5, -sdf);
	col = mix(col, rim_color.rgb, rim * rim_strength);

	float alpha = 1.0 - smoothstep(-1.0, 0.5, sdf);
	COLOR = vec4(col, paper_color.a * alpha * opacity);
}
