shader_type canvas_item;

instance uniform float facet_phase : hint_range(0.0, 1.0, 0.01) = 0.0;

uniform vec2 atlas_grid = vec2(3.0, 3.0);
uniform float facet_strength : hint_range(0.0, 1.0, 0.01) = 0.4;
uniform float specular_strength : hint_range(0.0, 1.0, 0.01) = 0.58;
uniform float inner_glow_strength : hint_range(0.0, 2.0, 0.05) = 0.32;
uniform float outer_glow_strength : hint_range(0.0, 2.0, 0.05) = 0.9;
uniform float near_glow_pixels : hint_range(0.5, 4.0, 0.1) = 1.8;
uniform float far_glow_pixels : hint_range(2.0, 10.0, 0.1) = 5.0;

void fragment() {
	vec4 modulation = COLOR;
	vec4 texel = texture(TEXTURE, UV);
	vec2 local_uv = fract(UV * atlas_grid);
	vec2 frame_index = min(floor(UV * atlas_grid), atlas_grid - vec2(1.0));
	vec2 frame_min = frame_index / atlas_grid;
	vec2 frame_max = frame_min + 1.0 / atlas_grid;
	vec2 frame_padding = TEXTURE_PIXEL_SIZE * 0.5;
	vec2 sample_min = frame_min + frame_padding;
	vec2 sample_max = frame_max - frame_padding;

	vec2 surface_position = (local_uv - vec2(0.5)) * vec2(1.35, 1.15);
	float surface_depth = sqrt(max(0.0, 1.0 - dot(surface_position, surface_position)));
	vec3 normal = normalize(vec3(surface_position, surface_depth));
	vec3 light_direction = normalize(vec3(
		-0.38 + facet_phase * 0.26,
		-0.56,
		0.74
	));
	float diffuse = max(dot(normal, light_direction), 0.0);
	float faceted_diffuse = floor(diffuse * 4.0 + 0.5) * 0.25;
	float lighting = mix(diffuse, faceted_diffuse, facet_strength);
	float specular = pow(
		max(dot(reflect(-light_direction, normal), vec3(0.0, 0.0, 1.0)), 0.0),
		22.0
	);
	float inner_reflection = pow(max(surface_depth, 0.0), 3.5) * 0.14;

	vec3 shadow_color = modulation.rgb * 0.3;
	vec3 lit_color = mix(modulation.rgb, vec3(1.0), 0.3);
	vec3 final_rgb = mix(shadow_color, lit_color, 0.3 + lighting * 0.7);
	final_rgb += mix(modulation.rgb, vec3(1.0), 0.7) * specular * specular_strength;
	final_rgb += modulation.rgb * inner_reflection;

	vec2 near_offset = TEXTURE_PIXEL_SIZE * near_glow_pixels;
	vec2 middle_offset = TEXTURE_PIXEL_SIZE * mix(near_glow_pixels, far_glow_pixels, 0.55);
	vec2 far_offset = TEXTURE_PIXEL_SIZE * far_glow_pixels;
	float near_alpha = 0.0;
	near_alpha = max(near_alpha, texture(TEXTURE, clamp(UV + vec2(near_offset.x, 0.0), sample_min, sample_max)).a);
	near_alpha = max(near_alpha, texture(TEXTURE, clamp(UV - vec2(near_offset.x, 0.0), sample_min, sample_max)).a);
	near_alpha = max(near_alpha, texture(TEXTURE, clamp(UV + vec2(0.0, near_offset.y), sample_min, sample_max)).a);
	near_alpha = max(near_alpha, texture(TEXTURE, clamp(UV - vec2(0.0, near_offset.y), sample_min, sample_max)).a);
	near_alpha = max(near_alpha, texture(TEXTURE, clamp(UV + near_offset, sample_min, sample_max)).a);
	near_alpha = max(near_alpha, texture(TEXTURE, clamp(UV - near_offset, sample_min, sample_max)).a);
	near_alpha = max(near_alpha, texture(TEXTURE, clamp(UV + vec2(near_offset.x, -near_offset.y), sample_min, sample_max)).a);
	near_alpha = max(near_alpha, texture(TEXTURE, clamp(UV + vec2(-near_offset.x, near_offset.y), sample_min, sample_max)).a);
	float middle_alpha = 0.0;
	middle_alpha = max(middle_alpha, texture(TEXTURE, clamp(UV + vec2(middle_offset.x, 0.0), sample_min, sample_max)).a);
	middle_alpha = max(middle_alpha, texture(TEXTURE, clamp(UV - vec2(middle_offset.x, 0.0), sample_min, sample_max)).a);
	middle_alpha = max(middle_alpha, texture(TEXTURE, clamp(UV + vec2(0.0, middle_offset.y), sample_min, sample_max)).a);
	middle_alpha = max(middle_alpha, texture(TEXTURE, clamp(UV - vec2(0.0, middle_offset.y), sample_min, sample_max)).a);
	middle_alpha = max(middle_alpha, texture(TEXTURE, clamp(UV + middle_offset, sample_min, sample_max)).a);
	middle_alpha = max(middle_alpha, texture(TEXTURE, clamp(UV - middle_offset, sample_min, sample_max)).a);
	middle_alpha = max(middle_alpha, texture(TEXTURE, clamp(UV + vec2(middle_offset.x, -middle_offset.y), sample_min, sample_max)).a);
	middle_alpha = max(middle_alpha, texture(TEXTURE, clamp(UV + vec2(-middle_offset.x, middle_offset.y), sample_min, sample_max)).a);
	float far_alpha = 0.0;
	far_alpha = max(far_alpha, texture(TEXTURE, clamp(UV + vec2(far_offset.x, 0.0), sample_min, sample_max)).a);
	far_alpha = max(far_alpha, texture(TEXTURE, clamp(UV - vec2(far_offset.x, 0.0), sample_min, sample_max)).a);
	far_alpha = max(far_alpha, texture(TEXTURE, clamp(UV + vec2(0.0, far_offset.y), sample_min, sample_max)).a);
	far_alpha = max(far_alpha, texture(TEXTURE, clamp(UV - vec2(0.0, far_offset.y), sample_min, sample_max)).a);
	far_alpha = max(far_alpha, texture(TEXTURE, clamp(UV + far_offset, sample_min, sample_max)).a);
	far_alpha = max(far_alpha, texture(TEXTURE, clamp(UV - far_offset, sample_min, sample_max)).a);
	far_alpha = max(far_alpha, texture(TEXTURE, clamp(UV + vec2(far_offset.x, -far_offset.y), sample_min, sample_max)).a);
	far_alpha = max(far_alpha, texture(TEXTURE, clamp(UV + vec2(-far_offset.x, far_offset.y), sample_min, sample_max)).a);
	float inner_glow = max(near_alpha - texel.a * 0.4, 0.0);
	float outer_glow = max(
		max(middle_alpha * 0.55, far_alpha * 0.25) - texel.a * 0.25,
		0.0
	);
	vec3 glow_color = mix(modulation.rgb, vec3(1.0), 0.52);
	final_rgb += glow_color * inner_glow * inner_glow_strength;
	final_rgb += glow_color * outer_glow * outer_glow_strength;

	float final_alpha = max(texel.a, max(inner_glow * 0.58, outer_glow * 0.42)) * modulation.a;
	COLOR = vec4(final_rgb, final_alpha);
}
