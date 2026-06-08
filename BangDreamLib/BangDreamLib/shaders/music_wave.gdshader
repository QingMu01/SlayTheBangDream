shader_type canvas_item;
render_mode blend_add;

// 流畅弹性扭曲着色器

uniform sampler2D viewport_texture : source_color, filter_linear;

uniform float speed : hint_range(0.1, 5.0) = 1.0;          // 流动速度
uniform float intensity : hint_range(0.0, 0.15) = 0.035;   // 扭曲强度

uniform float wobble_freq : hint_range(0.5, 8.0) = 3.0;    // 抖动频率（越高波纹越密）
uniform float twist_amount : hint_range(0.0, 1.0) = 0.4;   // 扭转幅度
uniform float breathe_amount : hint_range(0.0, 1.0) = 0.3; // 呼吸膨胀幅度

uniform float hue_shift : hint_range(0.0, 1.0) = 0.0;      // 色相偏移（0~1 对应色环一圈）
uniform float saturation : hint_range(0.0, 2.0) = 1.0;     // 饱和度（1.0 为原始）
uniform float brightness : hint_range(0.0, 2.0) = 1.0;     // 明度（1.0 为原始）

vec3 rgb2hsv(vec3 c) {
	vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
	vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
	float d = q.x - min(q.w, q.y);
	float e = 1.0e-10;
	return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 c) {
	vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
	return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

void fragment() {
	vec4 original = texture(viewport_texture, UV);

	if (original.a < 0.01) {
		COLOR = original;
	} else {

		float t = TIME * speed;

		vec2 center = vec2(0.5);
		vec2 to_center = UV - center;
		float dist = length(to_center);

		float breathe = (sin(t * 1.3) * 0.6 + sin(t * 0.7 + 1.5) * 0.4) * breathe_amount * intensity;
		vec2 distorted_uv = center + to_center * (1.0 + breathe);

		vec2 wobble;
		wobble.x = sin(UV.y * wobble_freq * PI + t * 2.1)
				* cos(UV.x * 1.7 + t * 1.3)
				* intensity * dist;
		wobble.y = cos(UV.x * wobble_freq * PI + t * 1.7)
				* sin(UV.y * 2.3 + t * 0.9)
				* intensity * dist;

		wobble.x += sin(UV.y * 1.1 + t * 0.8 + 2.0) * intensity * dist * 0.5;
		wobble.y += cos(UV.x * 0.9 + t * 0.6 + 0.7) * intensity * dist * 0.5;

		distorted_uv += wobble;

		float twist_angle = twist_amount * intensity
				* (sin(t * 1.1) + sin(t * 0.6 + 1.0) * 0.5)
				* dist;
		float cos_t = cos(twist_angle);
		float sin_t = sin(twist_angle);
		vec2 twisted;
		twisted.x = (distorted_uv.x - center.x) * cos_t - (distorted_uv.y - center.y) * sin_t + center.x;
		twisted.y = (distorted_uv.x - center.x) * sin_t + (distorted_uv.y - center.y) * cos_t + center.y;

		vec4 color = texture(viewport_texture, twisted);
		color.a = min(color.a, original.a);

		vec3 hsv = rgb2hsv(color.rgb);
		hsv.x = fract(hsv.x + hue_shift);
		hsv.y *= saturation;
		hsv.z *= brightness;
		color.rgb = hsv2rgb(hsv);

		COLOR = vec4(color.rgb,original.a);
	}
}
