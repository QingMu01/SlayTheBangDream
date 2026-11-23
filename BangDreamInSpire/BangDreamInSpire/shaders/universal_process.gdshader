shader_type canvas_item;

uniform float opacity : hint_range(0.0, 1.0) = 1.0;

uniform bool enable_glow = false;
uniform float glow_intensity : hint_range(0.0, 5.0) = 1.0;
uniform float glow_radius : hint_range(0.0, 64.0) = 8.0;
uniform vec4 glow_color : source_color = vec4(1.0, 0.8, 0.4, 1.0);
uniform float glow_threshold : hint_range(0.0, 1.0) = 0.2;
uniform float glow_softness : hint_range(0.0, 2.0) = 1.0;
uniform float bloom_intensity : hint_range(0.0, 3.0) = 0.0;
uniform float bloom_radius : hint_range(0.0, 32.0) = 4.0;

uniform bool enable_blur = false;
uniform float blur_radius : hint_range(0.0, 20.0) = 2.0;
// 0=双向，1=水平，2=垂直
uniform int blur_direction : hint_range(0, 2) = 0;
uniform float motion_blur_angle : hint_range(0.0, 360.0) = 0.0;
uniform float motion_blur_distance : hint_range(0.0, 20.0) = 0.0;

uniform bool enable_sharpen = false;
uniform float sharpen_amount : hint_range(0.0, 3.0) = 1.0;
uniform float sharpen_radius : hint_range(0.5, 3.0) = 1.0;

uniform bool enable_distortion = false;
uniform float ripple_intensity : hint_range(0.0, 0.1) = 0.0;
uniform float ripple_frequency : hint_range(0.0, 50.0) = 10.0;
uniform float ripple_speed : hint_range(-10.0, 10.0) = 1.0;
uniform float swirl_intensity : hint_range(0.0, 2.0) = 0.0;
uniform vec2 swirl_center = vec2(0.5, 0.5);
uniform float lens_distort : hint_range(-1.0, 1.0) = 0.0;
uniform float chromatic_aberration : hint_range(0.0, 0.05) = 0.0;

uniform float hue_shift : hint_range(-180.0, 180.0) = 0.0;
uniform float saturation : hint_range(0.0, 2.0) = 1.0;
uniform float brightness : hint_range(-1.0, 1.0) = 0.0;
uniform float contrast : hint_range(0.0, 2.0) = 1.0;
uniform bool enable_color_overlay = false;
uniform vec4 overlay_color : source_color = vec4(1.0, 0.0, 0.0, 0.5);
uniform int overlay_blend_mode : hint_range(0, 5) = 0; // 0=正常,1=正片叠底,2=滤色,3=叠加,4=柔光,5=颜色加深
uniform bool enable_gradient_map = false;
uniform vec4 gradient_color1 : source_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform vec4 gradient_color2 : source_color = vec4(1.0, 1.0, 1.0, 1.0);

uniform bool enable_outline = false;
uniform float outline_width : hint_range(0.0, 8.0) = 2.0;
uniform vec4 outline_color : source_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform float pixelate_size : hint_range(0.0, 64.0) = 0.0;
uniform bool invert_colors = false;
uniform bool grayscale = false;
uniform bool sepia = false;
uniform float threshold_level : hint_range(0.0, 1.0) = 0.0;

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

vec3 blend(vec3 base, vec3 blend, int mode) {
	if (mode == 1) return base * blend;
	if (mode == 2) return 1.0 - (1.0 - base) * (1.0 - blend);
	if (mode == 3) {
		return mix(2.0 * base * blend, 1.0 - 2.0 * (1.0 - base) * (1.0 - blend), step(0.5, base));
	}
	if (mode == 4) {
		return mix(2.0 * base * blend + base * base * (1.0 - 2.0 * blend),
			sqrt(base) * (2.0 * blend - 1.0) + 2.0 * base * (1.0 - blend),
			step(0.5, blend));
	}
	if (mode == 5) return 1.0 - (1.0 - blend) / (base + 0.001);
	return blend;
}

vec4 gaussian_blur(sampler2D tex, vec2 uv, vec2 pixel_size, float radius, int direction) {
	if (radius <= 0.0) return texture(tex, uv);

	vec4 color = vec4(0.0);
	float total_weight = 0.0;

	int samples = int(min(radius * 2.0 + 1.0, 32.0));

	for (int i = -samples; i <= samples; i++) {
		float fi = float(i);
		float weight = exp(-(fi * fi) / (2.0 * radius * radius + 0.001));

		vec2 offset = vec2(0.0);
		if (direction == 0 || direction == 1) offset.x = fi * pixel_size.x;
		if (direction == 0 || direction == 2) offset.y = fi * pixel_size.y;

		color += texture(tex, uv + offset) * weight;
		total_weight += weight;
	}

	return color / total_weight;
}

vec4 gaussian_blur_2d(sampler2D tex, vec2 uv, vec2 pixel_size, float radius) {
	if (radius <= 0.0) return texture(tex, uv);

	vec4 horizontal = vec4(0.0);
	float total_weight = 0.0;
	int samples = int(min(radius * 2.0 + 1.0, 32.0));

	for (int i = -samples; i <= samples; i++) {
		float fi = float(i);
		float weight = exp(-(fi * fi) / (2.0 * radius * radius + 0.001));
		horizontal += texture(tex, uv + vec2(fi * pixel_size.x, 0.0)) * weight;
		total_weight += weight;
	}
	horizontal /= total_weight;

	vec4 vertical = vec4(0.0);
	total_weight = 0.0;
	for (int i = -samples; i <= samples; i++) {
		float fi = float(i);
		float weight = exp(-(fi * fi) / (2.0 * radius * radius + 0.001));
		vertical += texture(tex, uv + vec2(0.0, fi * pixel_size.y)) * weight;
		total_weight += weight;
	}
	vertical /= total_weight;

	return (horizontal + vertical) * 0.5;
}

vec4 motion_blur(sampler2D tex, vec2 uv, vec2 pixel_size, float distance, float angle_deg) {
	if (distance <= 0.0) return texture(tex, uv);

	float angle = radians(angle_deg);
	vec2 dir = vec2(cos(angle), sin(angle)) * pixel_size * distance;

	vec4 color = vec4(0.0);
	int samples = 16;
	for (int i = 0; i < samples; i++) {
		float t = float(i) / float(samples - 1);
		color += texture(tex, uv + dir * (t - 0.5));
	}
	return color / float(samples);
}

vec4 get_glow(sampler2D tex, vec2 uv, vec2 pixel_size, float radius, float threshold, float intensity, vec4 glow_col, float softness) {
	if (radius <= 0.0 || intensity <= 0.0) return vec4(0.0);

	vec4 glow = vec4(0.0);
	float total_weight = 0.0;
	int samples = int(min(radius * 1.5 + 4.0, 24.0));

	for (int x = -samples; x <= samples; x++) {
		for (int y = -samples; y <= samples; y++) {
			if (x == 0 && y == 0) continue;
			vec2 offset = vec2(float(x), float(y)) * pixel_size * radius;
			vec4 sample_col = texture(tex, uv + offset);

			float lum = dot(sample_col.rgb, vec3(0.299, 0.587, 0.114));
			float mask = smoothstep(threshold, threshold + 0.1 + softness * 0.2, lum);

			float dist = length(vec2(float(x), float(y)));
			float weight = exp(-dist / (radius * 0.5 + 0.1));

			glow += sample_col * mask * weight;
			total_weight += weight;
		}
	}

	glow = glow / total_weight * intensity;
	return glow * glow_col;
}

vec4 sharpen(sampler2D tex, vec2 uv, vec2 pixel_size, float amount, float radius) {
	vec4 center = texture(tex, uv);
	vec4 blurred = gaussian_blur_2d(tex, uv, pixel_size, radius);
	return center + (center - blurred) * amount;
}

vec4 outline(sampler2D tex, vec2 uv, vec2 pixel_size, float width, vec4 line_color) {
	float alpha = 0.0;
	int steps = 8;
	for (int i = 0; i < steps; i++) {
		float angle = float(i) / float(steps) * 6.28318;
		vec2 offset = vec2(cos(angle), sin(angle)) * pixel_size * width;
		alpha = max(alpha, texture(tex, uv + offset).a);
	}

	float center_alpha = texture(tex, uv).a;
	alpha = alpha * (1.0 - center_alpha);

	return vec4(line_color.rgb, alpha * line_color.a);
}

vec2 distort_uv(vec2 uv) {
	vec2 distorted = uv;

	if (pixelate_size > 0.0) {
		distorted = floor(distorted * pixelate_size) / pixelate_size;
	}

	if (swirl_intensity > 0.0) {
		vec2 center = swirl_center;
		vec2 delta = distorted - center;
		float dist = length(delta);
		float angle = atan(delta.y, delta.x);
		float swirl = swirl_intensity * (1.0 - dist) * 3.14159;
		angle += swirl * smoothstep(1.0, 0.0, dist * 2.0);
		distorted = center + vec2(cos(angle), sin(angle)) * dist;
	}

	if (ripple_intensity > 0.0) {
		float ripple = sin(distorted.x * ripple_frequency + TIME * ripple_speed)
				* cos(distorted.y * ripple_frequency + TIME * ripple_speed);
		distorted += vec2(cos(ripple * 3.14159), sin(ripple * 3.14159)) * ripple_intensity;
	}

	if (lens_distort != 0.0) {
		vec2 center = vec2(0.5, 0.5);
		vec2 delta = distorted - center;
		float dist = length(delta);
		float factor = 1.0 + lens_distort * dist * dist;
		distorted = center + delta * factor;
	}

	return distorted;
}

vec3 adjust_color(vec3 color) {
	vec3 hsv = rgb2hsv(color);
	hsv.x += hue_shift / 360.0;
	hsv.x = fract(hsv.x);
	hsv.y *= saturation;
	hsv.z += brightness;
	color = hsv2rgb(hsv);

	color = (color - 0.5) * contrast + 0.5;

	if (invert_colors) color = 1.0 - color;

	if (grayscale) {
		float lum = dot(color, vec3(0.299, 0.587, 0.114));
		color = vec3(lum);
	}

	if (sepia) {
		color = vec3(
			dot(color, vec3(0.393, 0.769, 0.189)),
			dot(color, vec3(0.349, 0.686, 0.168)),
			dot(color, vec3(0.272, 0.534, 0.131))
		);
	}

	if (threshold_level > 0.0) {
		float lum = dot(color, vec3(0.299, 0.587, 0.114));
		color = vec3(step(threshold_level, lum));
	}

	if (enable_gradient_map) {
		float lum = dot(clamp(color, 0.0, 1.0), vec3(0.299, 0.587, 0.114));
		color = mix(gradient_color1.rgb, gradient_color2.rgb, lum);
	}

	return color;
}

void fragment() {
	vec2 pixel_size = TEXTURE_PIXEL_SIZE;
	vec2 uv = UV;

	vec2 distorted_uv = distort_uv(uv);

	vec4 tex_color;
	if (chromatic_aberration > 0.0 && enable_distortion) {
		float r = texture(TEXTURE, distorted_uv + vec2(chromatic_aberration, 0.0)).r;
		float g = texture(TEXTURE, distorted_uv).g;
		float b = texture(TEXTURE, distorted_uv - vec2(chromatic_aberration, 0.0)).b;
		float a = texture(TEXTURE, distorted_uv).a;
		tex_color = vec4(r, g, b, a);
	} else {
		tex_color = texture(TEXTURE, distorted_uv);
	}

	vec4 glow = vec4(0.0);
	if (enable_glow) {
		glow = get_glow(TEXTURE, distorted_uv, pixel_size, glow_radius, glow_threshold, glow_intensity, glow_color, glow_softness);
	}

	vec4 bloom = vec4(0.0);
	if (bloom_intensity > 0.0) {
		bloom = get_glow(TEXTURE, distorted_uv, pixel_size, bloom_radius, 0.3, bloom_intensity, vec4(1.0), 0.5);
	}

	vec4 blur = vec4(0.0);
	if (enable_blur) {
		if (motion_blur_distance > 0.0) {
			blur = motion_blur(TEXTURE, distorted_uv, pixel_size, motion_blur_distance, motion_blur_angle);
		} else if (blur_direction == 0) {
			blur = gaussian_blur_2d(TEXTURE, distorted_uv, pixel_size, blur_radius);
		} else {
			blur = gaussian_blur(TEXTURE, distorted_uv, pixel_size, blur_radius, blur_direction);
		}
	}

	vec4 sharpened = vec4(0.0);
	if (enable_sharpen) {
		sharpened = sharpen(TEXTURE, distorted_uv, pixel_size, sharpen_amount, sharpen_radius);
	}

	vec4 base_color = tex_color;
	if (enable_blur) base_color = mix(base_color, blur, 0.8);
	if (enable_sharpen) base_color = sharpened;

	base_color.rgb = adjust_color(base_color.rgb);

	if (enable_color_overlay) {
		base_color.rgb = blend(base_color.rgb, overlay_color.rgb, overlay_blend_mode);
		base_color.a *= overlay_color.a;
	}

	vec4 outline_col = vec4(0.0);
	if (enable_outline) {
		outline_col = outline(TEXTURE, distorted_uv, pixel_size, outline_width, outline_color);
	}

	vec3 final_rgb = base_color.rgb + glow.rgb + bloom.rgb;
	float final_alpha = max(base_color.a, outline_col.a);

	if (outline_col.a > 0.0) {
		final_rgb = mix(final_rgb, outline_col.rgb, outline_col.a);
	}

	final_alpha *= opacity;

	COLOR = vec4(final_rgb, final_alpha);
}