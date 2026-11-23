shader_type canvas_item;
render_mode blend_mix;

// 按饱和度保留白色刀芯，仅将贴图内烘焙的粉边换为蓝紫；
// 直接 modulate 乘蓝紫会让刀芯和粉边发灰。
uniform vec3 rim_color : source_color = vec3(0.541176, 0.498039, 0.815686);
uniform float saturation_threshold : hint_range(0.0, 0.5) = 0.10;
uniform float saturation_softness : hint_range(0.01, 0.4) = 0.12;
uniform float rim_boost : hint_range(0.5, 1.5) = 1.0;

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	float mx = max(max(tex.r, tex.g), tex.b);
	float mn = min(min(tex.r, tex.g), tex.b);
	float sat = mx - mn;

	vec3 white_core = vec3(mx);
	vec3 violet_rim = rim_color * mx * rim_boost;
	float rim_amount = smoothstep(saturation_threshold, saturation_threshold + saturation_softness, sat);
	vec3 color = mix(white_core, violet_rim, rim_amount);

	vec4 modulate = COLOR;
	COLOR = vec4(color * modulate.rgb, tex.a * modulate.a);
}
