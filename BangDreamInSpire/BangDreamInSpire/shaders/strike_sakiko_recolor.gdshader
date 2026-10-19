shader_type canvas_item;
render_mode blend_mix;

// 祥子「打击」的粉边重上色：白刀芯保持不变，只把有色边缘映射到 blue-violet。
// 原版 vfx_attack_slash 的粉边是烘死在贴图里的，直接 modulate 乘蓝紫会发灰发脏；
// 这里按饱和度区分「白色刀芯」与「有色边缘」，只对边缘换色。
uniform vec3 rim_color : source_color = vec3(0.541176, 0.498039, 0.815686); // #8A7FD0
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
