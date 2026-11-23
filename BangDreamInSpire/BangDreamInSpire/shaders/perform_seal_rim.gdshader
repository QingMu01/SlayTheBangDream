shader_type canvas_item;
render_mode blend_add;

// 演奏印框：沿槽位斜切平行四边形的一圈"边缘吃光"。
// 不是矢量描边——卡图里没有硬边，所以只给柔光衰减 + 一点点极细的亮线做定义。
// 自左向右点亮（draw_progress），再从右向左溢出熄灭（release_progress）。
// 五个元素印共用同一套参数，只有 tint 与节拍不同，读起来才像"同一枚印"。

instance uniform vec2 rect_size = vec2(160.0, 50.0);
instance uniform float draw_progress : hint_range(0.0, 1.0, 0.001) = 1.0;
instance uniform float release_progress : hint_range(0.0, 1.0, 0.001) = 0.0;
instance uniform float energy : hint_range(0.0, 2.0, 0.01) = 1.0;
instance uniform vec4 tint : source_color = vec4(1.0, 1.0, 1.0, 1.0);

// 与槽位一致的斜切斜率；改动会与卡面错位。
uniform float slant_slope : hint_range(-1.0, 1.0, 0.01) = 0.3;
// 边缘柔光的衰减长度（像素）。
uniform float edge_glow : hint_range(1.0, 20.0, 0.5) = 5.5;
// 极细亮线占多少，0 = 纯柔光。
uniform float edge_hot : hint_range(0.0, 1.0, 0.01) = 0.35;
// 推进前沿的过曝量。
uniform float lead_hot : hint_range(0.0, 2.0, 0.05) = 0.5;

varying vec2 local_position;

void vertex() {
    local_position = VERTEX;
}

void fragment() {
    vec2 size = max(rect_size, vec2(1.0));
    float slant_offset = slant_slope * size.y;
    float shape_width = max(size.x - abs(slant_offset), 0.0001);
    float center = size.x * 0.5 + slant_offset * (0.5 - local_position.y / size.y);
    float side_distance = (shape_width * 0.5 - abs(local_position.x - center)) / sqrt(1.0 + slant_slope * slant_slope);
    float vertical_distance = min(local_position.y, size.y - local_position.y);
    float border_distance = min(side_distance, vertical_distance);

    float smoothing = max(fwidth(border_distance), 0.5);
    float inside = smoothstep(-smoothing, smoothing, border_distance);
    float soft_edge = exp(-max(border_distance, 0.0) / max(edge_glow, 0.001)) * 0.55;
    float hard_line = (1.0 - smoothstep(0.4, 1.6, border_distance)) * edge_hot * 0.6;

    // 横向归一化：0 在左端、1 在右端，作为"划开"的推进方向。
    float u = clamp(local_position.x / size.x, 0.0, 1.0);

    float draw_front = draw_progress * 1.25 - 0.12;
    float draw_mask = smoothstep(u - 0.10, u + 0.01, draw_progress * 1.25);
    float draw_lead = exp(-max(draw_front - u, 0.0) / 0.06) * step(u, draw_front + 0.001)
        * (1.0 - smoothstep(0.80, 1.0, draw_progress));

    float release_front = release_progress * 1.30 - 0.15;
    float release_mask = 1.0 - smoothstep(u - 0.12, u + 0.02, release_progress * 1.30);
    float release_lead = exp(-abs(u - release_front) / 0.05) * step(0.001, release_progress);

    float alpha = (soft_edge + hard_line) * inside * draw_mask * release_mask * energy;
    float hot = 1.0 + lead_hot * (draw_lead + release_lead);
    COLOR = vec4(tint.rgb * hot, tint.a * alpha);
}
