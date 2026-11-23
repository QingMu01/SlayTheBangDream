shader_type canvas_item;

// 演奏墨流：一条带拖尾的墨从左向右扫过槽位。
// 与 parallelogram_clip 同几何（槽位斜切），但它是"经过"而不是"覆盖"——
// 槽位底下的卡图始终留在原地，只被墨短暂压暗，所以卡名与卡面不会整段消失。
//
// 用法：ColorRect 铺满槽位矩形，颜色走 `color`（顶点色），混合为普通的 MIX。

instance uniform vec2 rect_size = vec2(160.0, 50.0);
// 墨带中心在槽位横向的归一化位置；0 = 左端，1 = 右端。越界即完全在画外。
instance uniform float band_position = -0.4;
// 墨带宽度（归一化）；前沿之后按 softness 拖出尾巴。
instance uniform float band_width = 0.55;
instance uniform float band_softness = 0.12;
instance uniform float band_alpha = 0.0;
// 全程薄薄压一层元素底色，用来交代"元素归属"。
instance uniform float base_alpha = 0.0;
// 纵向档的横向收口：墨带两端不能切出生硬竖边（否则读成一块平移的玻璃板）。
// 0 = 不收口（槽位档），>0 = 两侧按该比例羽化。
instance uniform float lateral_fade = 0.0;

uniform float slant_slope : hint_range(-1.0, 1.0, 0.01) = 0.3;
// 纵向模式：墨自脚底升到头顶之上，其余语法不变。演奏身印用这一档。
uniform bool vertical = false;
// 墨带前沿的湿边亮线；没有它，墨就只是一块平移的色块。
uniform float front_light : hint_range(0.0, 1.0, 0.01) = 0.45;
uniform float edge_softness : hint_range(0.0, 4.0, 0.1) = 0.0;

varying vec2 local_position;

void vertex() {
    local_position = VERTEX;
}

void fragment() {
    vec2 size = max(rect_size, vec2(1.0));
    float normalized_y = clamp(local_position.y / size.y, 0.0, 1.0);
    // 倾斜量与切分方向的短边同量级，纵向档不会斜穿整面。
    float slant_offset = slant_slope * (vertical ? size.x : size.y);
    float shape_width = max(size.x - abs(slant_offset), 0.0001);
    float center = size.x * 0.5 + slant_offset * (0.5 - normalized_y);
    float left = center - shape_width * 0.5;
    float right = center + shape_width * 0.5;
    float edge_distance = min(local_position.x - left, right - local_position.x);
    float smoothing = max(fwidth(edge_distance), 0.001) + edge_softness;
    float shape_mask = smoothstep(-smoothing, smoothing, edge_distance);

    // 沿切分方向归一化，保证斜切两端都在墨带范围内；纵向档 0 在脚底、1 在头顶之上。
    float u = vertical
        ? 1.0 - normalized_y
        : clamp((local_position.x - (size.x * 0.5 - shape_width * 0.5)) / shape_width, 0.0, 1.0);
    float offset = u - band_position;
    float half_width = max(band_width * 0.5, 0.0001);
    // 前沿紧、后沿拖长：墨是"被拖着走"的。
    float leading = 1.0 - smoothstep(half_width, half_width + band_softness, offset);
    float trailing = smoothstep(-half_width - band_softness, -half_width, offset);
    float band = leading * trailing;

    float lateral = lateral_fade > 0.0
        ? smoothstep(0.0, lateral_fade, local_position.x / size.x) *
          (1.0 - smoothstep(1.0 - lateral_fade, 1.0, local_position.x / size.x))
        : 1.0;
    float alpha = shape_mask * (band * band_alpha + base_alpha) * lateral;
    float front = 1.0 - smoothstep(0.0, max(band_softness, 0.0001), abs(offset - half_width) * 0.5);
    COLOR.rgb = mix(COLOR.rgb, vec3(1.0), front * front_light * step(0.001, band_alpha));
    COLOR.a *= alpha;
}
