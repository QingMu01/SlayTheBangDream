shader_type canvas_item;

// 基于 alpha 边缘检测的 2D 纹理描边 shader

// 描边颜色
instance uniform vec4 outline_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
// 描边宽度（像素）
uniform float outline_width : hint_range(0.0, 64.0, 0.5) = 3.0;
// 描边强度倍率（1.0 为 outline_color 原始透明度）
uniform float outline_intensity : hint_range(0.0, 2.0, 0.05) = 1.0;
// 实体判定阈值：alpha 高于此值视为实体像素
uniform float alpha_threshold : hint_range(0.0, 1.0, 0.01) = 0.5;
// 边缘过渡软度（0 = 硬边，越大过渡越柔和，不稀释描边强度）
uniform float softness : hint_range(0.0, 0.5, 0.01) = 0.02;
// 采样方向数（8~64，越大越圆滑，性能越低）
uniform int sample_count : hint_range(8, 64, 4) = 24;
// 同心采样层数（宽度较大时建议加大，避免边缘出现缺口）
uniform int ring_count : hint_range(1, 8, 1) = 3;
// 描边模式：0 = 外部，1 = 内部，2 = 内外
uniform int outline_mode : hint_enum("Outer", "Inner", "Both") = 0;

void fragment() {
	vec4 tex_color = texture(TEXTURE, UV) * COLOR;
	float center_alpha = tex_color.a;

	// 图集适配：REGION_RECT 是当前帧在整张纹理中的 UV 区域
	// （非图集时为 (0, 0, 1, 1)），据此计算帧内局部坐标
	vec2 region_size = max(REGION_RECT.zw, vec2(0.0001));
	vec2 local_uv = (UV - REGION_RECT.xy) / region_size;

	float outer_alpha = 0.0;
	float inner_alpha = 1.0;

	if (outline_width > 0.0) {
		float rings = float(ring_count);
		for (int ring = 0; ring < ring_count; ring++) {
			float radius = outline_width * (float(ring) + 1.0) / rings;
			vec2 uv_step = TEXTURE_PIXEL_SIZE * radius;
			vec2 local_step = uv_step / region_size;
			for (int i = 0; i < sample_count; i++) {
				float angle = float(i) / float(sample_count) * 6.2831853;
				vec2 direction = vec2(cos(angle), sin(angle));
				// 采样点超出当前帧时视为透明，避免采到相邻帧
				vec2 sample_local = local_uv + direction * local_step;
				float in_frame = step(0.0, sample_local.x) * step(0.0, sample_local.y)
						* (1.0 - step(1.0, sample_local.x)) * (1.0 - step(1.0, sample_local.y));
				float neighbor_alpha = in_frame * texture(TEXTURE, UV + direction * uv_step).a * COLOR.a;
				outer_alpha = max(outer_alpha, neighbor_alpha);
				inner_alpha = min(inner_alpha, neighbor_alpha);
			}
		}
	}

	// 阈值二值化：实体/透明判定，避免半透明羽化边缘稀释描边强度
	float half_smooth = max(fwidth(center_alpha), 0.0001) + softness;
	float center_solid = smoothstep(
		alpha_threshold - half_smooth,
		alpha_threshold + half_smooth,
		center_alpha
	);
	float outer_solid = smoothstep(
		alpha_threshold - half_smooth,
		alpha_threshold + half_smooth,
		outer_alpha
	);
	float inner_solid = smoothstep(
		alpha_threshold - half_smooth,
		alpha_threshold + half_smooth,
		inner_alpha
	);

	// 外描边：周围有实体像素而自身透明
	float outer_edge = outer_solid * (1.0 - center_solid);
	// 内描边：自身为实体而周围存在透明像素
	float inner_edge = center_solid * (1.0 - inner_solid);

	float edge = 0.0;
	if (outline_mode == 1) {
		edge = inner_edge;
	} else if (outline_mode == 2) {
		edge = max(outer_edge, inner_edge);
	} else {
		edge = outer_edge;
	}

	float outline_alpha = edge * outline_intensity * outline_color.a;
	
	vec3 final_rgb = mix(tex_color.rgb, outline_color.rgb, edge);
	float final_alpha = max(center_alpha, outline_alpha);

	COLOR = vec4(final_rgb, final_alpha);
}
