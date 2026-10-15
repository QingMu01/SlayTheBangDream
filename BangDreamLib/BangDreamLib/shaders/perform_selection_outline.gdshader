shader_type canvas_item;

instance uniform vec2 rect_size = vec2(160.0, 50.0);
instance uniform float hovered = 0.0;

varying vec2 local_position;
varying vec4 vertex_color;

void vertex() {
    local_position = VERTEX;
    vertex_color = COLOR;
}

void fragment() {
    // 与槽位卡面的平行四边形一致；镜像由槽位节点的变换统一处理。
    vec2 size = max(rect_size, vec2(1.0));
    float slant = 0.3 * size.y;
    float width = max(size.x - slant, 1.0);
    float center = size.x * 0.5 + slant * (0.5 - local_position.y / size.y);
    float side_distance = (width * 0.5 - abs(local_position.x - center)) / sqrt(1.09);
    float distance = min(side_distance, min(local_position.y, size.y - local_position.y));
    float smoothing = max(fwidth(distance), 0.5);
    float mask = smoothstep(-smoothing, smoothing, distance);
    float pulse = 0.5 + 0.5 * sin(TIME * 4.2);
    float border_width = mix(2.4, 4.0, hovered);
    float border = 1.0 - smoothstep(border_width, border_width + smoothing, distance);
    float glow = exp(-max(distance, 0.0) / 6.0) * 0.18;
    float brightness = mix(0.48 + pulse * 0.32, 1.0, hovered);
    vec3 tint = mix(vec3(1.0, 0.78, 0.34), vec3(1.0, 0.92, 0.66), hovered);
    COLOR = vec4(tint, mask * (border + glow) * brightness) * vertex_color;
}
