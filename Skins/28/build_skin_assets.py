from __future__ import annotations

from pathlib import Path
import math

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parent

BASE_WIDTH = 114
BASE_HEIGHT = 56
CORNER_RADIUS = 15


def scale_dimension(value: int, scale_percent: int) -> int:
    return int(math.floor(((value * scale_percent) / 100.0) + 0.5))


TARGETS = [
    ("TrafficView.panel.90.png", scale_dimension(BASE_WIDTH, 90), scale_dimension(BASE_HEIGHT, 90)),
    ("TrafficView.panel.png", BASE_WIDTH, BASE_HEIGHT),
    ("TrafficView.panel.110.png", scale_dimension(BASE_WIDTH, 110), scale_dimension(BASE_HEIGHT, 110)),
    ("TrafficView.panel.125.png", scale_dimension(BASE_WIDTH, 125), scale_dimension(BASE_HEIGHT, 125)),
    ("TrafficView.panel.150.png", scale_dimension(BASE_WIDTH, 150), scale_dimension(BASE_HEIGHT, 150)),
]


def new_layer(size: tuple[int, int]) -> Image.Image:
    return Image.new("RGBA", size, (0, 0, 0, 0))


def rounded_mask(size: tuple[int, int], radius: int) -> Image.Image:
    mask = Image.new("L", size, 0)
    draw = ImageDraw.Draw(mask)
    draw.rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), radius=radius, fill=255)
    return mask


def add_glow_stroke(
    image: Image.Image,
    box: tuple[int, int, int, int],
    radius: int,
    color: tuple[int, int, int, int],
    width: int,
    blur: int,
) -> None:
    layer = new_layer(image.size)
    draw = ImageDraw.Draw(layer)
    draw.rounded_rectangle(box, radius=radius, outline=color, width=width)
    image.alpha_composite(layer.filter(ImageFilter.GaussianBlur(blur)))


def add_line_glow(
    image: Image.Image,
    points: list[tuple[float, float]],
    color: tuple[int, int, int, int],
    width: int,
    blur: int,
) -> None:
    layer = new_layer(image.size)
    draw = ImageDraw.Draw(layer)
    draw.line(points, fill=color, width=width, joint="curve")
    image.alpha_composite(layer.filter(ImageFilter.GaussianBlur(blur)))


def build_base_panel(size: tuple[int, int]) -> Image.Image:
    width, height = size
    panel = new_layer(size)
    draw = ImageDraw.Draw(panel)

    for y in range(height):
        ratio = y / max(1, height - 1)
        r = int(7 + (6 * ratio))
        g = int(14 + (18 * ratio))
        b = int(22 + (34 * ratio))
        a = 255
        draw.line((0, y, width, y), fill=(r, g, b, a))

    vignette = new_layer(size)
    vdraw = ImageDraw.Draw(vignette)
    vdraw.rounded_rectangle(
        (1, 1, width - 2, height - 2),
        radius=max(4, int(round(min(width, height) * 0.22))),
        outline=(14, 30, 52, 210),
        width=max(1, int(round(width * 0.025))),
    )
    panel.alpha_composite(vignette.filter(ImageFilter.GaussianBlur(max(1, int(round(width * 0.01))))))

    gloss = new_layer(size)
    gdraw = ImageDraw.Draw(gloss)
    gdraw.rounded_rectangle(
        (4, 2, width - 4, int(round(height * 0.43))),
        radius=max(4, int(round(height * 0.16))),
        fill=(200, 230, 255, 28),
    )
    panel.alpha_composite(gloss.filter(ImageFilter.GaussianBlur(max(2, int(round(width * 0.03))))))

    top_sheen = new_layer(size)
    sdraw = ImageDraw.Draw(top_sheen)
    sdraw.rounded_rectangle(
        (6, 4, width - 8, 9),
        radius=3,
        fill=(170, 225, 255, 26),
    )
    panel.alpha_composite(top_sheen.filter(ImageFilter.GaussianBlur(2)))

    panel.putalpha(rounded_mask(size, max(6, int(round(height * 0.28)))))
    return panel


def draw_hud_details(panel: Image.Image) -> None:
    width, height = panel.size

    hud = new_layer(panel.size)
    draw = ImageDraw.Draw(hud)

    for y_ratio in (0.22, 0.50, 0.73):
        y = int(round(height * y_ratio))
        draw.line((7, y, width - 12, y), fill=(40, 160, 220, 38), width=1)

    for x_ratio in (0.32, 0.45, 0.58):
        x = int(round(width * x_ratio))
        draw.line((x, 8, x, height - 8), fill=(30, 150, 220, 26), width=1)

    ring_x = int(round(width * 0.60))
    ring_y = int(round(height * 0.54))
    draw.ellipse((ring_x - 14, ring_y - 14, ring_x + 14, ring_y + 14), outline=(40, 220, 255, 36), width=2)
    draw.ellipse((ring_x - 8, ring_y - 8, ring_x + 8, ring_y + 8), outline=(80, 230, 255, 44), width=2)

    right_blob = new_layer(panel.size)
    bdraw = ImageDraw.Draw(right_blob)
    bdraw.ellipse(
        (int(round(width * 0.84)), int(round(height * 0.48)), width - 4, height - 5),
        fill=(20, 220, 255, 52),
    )
    panel.alpha_composite(right_blob.filter(ImageFilter.GaussianBlur(max(3, int(round(width * 0.025))))))
    panel.alpha_composite(hud.filter(ImageFilter.GaussianBlur(1)))

    small_glows = new_layer(panel.size)
    sdraw = ImageDraw.Draw(small_glows)
    accents = [
        (int(round(width * 0.23)), int(round(height * 0.14))),
        (int(round(width * 0.52)), int(round(height * 0.18))),
        (int(round(width * 0.73)), int(round(height * 0.62))),
    ]
    for cx, cy in accents:
        sdraw.ellipse((cx - 2, cy - 2, cx + 2, cy + 2), fill=(110, 245, 255, 150))
    panel.alpha_composite(small_glows.filter(ImageFilter.GaussianBlur(3)))


def draw_left_overlay(panel: Image.Image) -> None:
    width, height = panel.size
    overlay_box = (
        int(round(width * 0.07)),
        int(round(height * 0.11)),
        int(round(width * 0.56)),
        int(round(height * 0.83)),
    )
    radius = max(6, int(round(height * 0.20)))

    fill_layer = new_layer(panel.size)
    fdraw = ImageDraw.Draw(fill_layer)
    fdraw.rounded_rectangle(overlay_box, radius=radius, fill=(8, 20, 35, 88))
    panel.alpha_composite(fill_layer.filter(ImageFilter.GaussianBlur(1)))

    edge_layer = new_layer(panel.size)
    edraw = ImageDraw.Draw(edge_layer)
    edraw.rounded_rectangle(overlay_box, radius=radius, outline=(135, 240, 255, 115), width=max(1, int(round(width * 0.016))))
    edraw.rounded_rectangle(
        (overlay_box[0] + 2, overlay_box[1] + 2, overlay_box[2] - 2, overlay_box[3] - 2),
        radius=max(4, radius - 2),
        outline=(255, 255, 255, 28),
        width=1,
    )
    panel.alpha_composite(edge_layer.filter(ImageFilter.GaussianBlur(1)))

    beam = new_layer(panel.size)
    bdraw = ImageDraw.Draw(beam)
    beam_height = max(3, int(round(height * 0.10)))
    bdraw.rounded_rectangle(
        (
            overlay_box[0] + 4,
            overlay_box[1] + beam_height,
            overlay_box[2] - 8,
            overlay_box[1] + (beam_height * 2),
        ),
        radius=max(2, beam_height // 2),
        fill=(180, 235, 255, 26),
    )
    bdraw.line(
        (
            overlay_box[0] + int(round((overlay_box[2] - overlay_box[0]) * 0.14)),
            overlay_box[1] + 3,
            overlay_box[0] + int(round((overlay_box[2] - overlay_box[0]) * 0.14)),
            overlay_box[3] - 3,
        ),
        fill=(150, 240, 255, 55),
        width=max(1, int(round(width * 0.014))),
    )
    panel.alpha_composite(beam.filter(ImageFilter.GaussianBlur(3)))


def draw_bottom_curves(panel: Image.Image) -> None:
    width, height = panel.size
    green = [
        (width * 0.02, height * 0.90),
        (width * 0.16, height * 0.78),
        (width * 0.32, height * 0.72),
        (width * 0.47, height * 0.74),
        (width * 0.61, height * 0.82),
    ]
    yellow = [
        (width * 0.10, height * 0.83),
        (width * 0.24, height * 0.79),
        (width * 0.39, height * 0.66),
        (width * 0.53, height * 0.72),
        (width * 0.67, height * 0.80),
    ]

    add_line_glow(panel, green, (54, 255, 140, 130), max(2, int(round(width * 0.018))), max(2, int(round(width * 0.016))))
    add_line_glow(panel, yellow, (255, 210, 30, 140), max(2, int(round(width * 0.018))), max(2, int(round(width * 0.016))))

    draw = ImageDraw.Draw(panel)
    draw.line(green, fill=(74, 255, 120, 240), width=max(1, int(round(width * 0.016))), joint="curve")
    draw.line(yellow, fill=(255, 196, 8, 250), width=max(1, int(round(width * 0.016))), joint="curve")


def draw_right_ring_carrier(panel: Image.Image) -> None:
    width, height = panel.size
    center_x = int(round(width * 0.763))
    center_y = int(round(height * 0.45))
    outer_r = int(round(height * 0.38))
    mid_r = int(round(height * 0.30))
    inner_r = int(round(height * 0.18))

    glow = new_layer(panel.size)
    gdraw = ImageDraw.Draw(glow)
    gdraw.ellipse(
        (center_x - outer_r - 4, center_y - outer_r - 4, center_x + outer_r + 4, center_y + outer_r + 4),
        outline=(20, 185, 255, 105),
        width=max(2, int(round(width * 0.018))),
    )
    panel.alpha_composite(glow.filter(ImageFilter.GaussianBlur(max(2, int(round(width * 0.018))))))

    ring = new_layer(panel.size)
    draw = ImageDraw.Draw(ring)

    draw.ellipse(
        (center_x - outer_r, center_y - outer_r, center_x + outer_r, center_y + outer_r),
        outline=(24, 180, 255, 130),
        width=max(2, int(round(width * 0.018))),
    )
    draw.ellipse(
        (center_x - mid_r, center_y - mid_r, center_x + mid_r, center_y + mid_r),
        outline=(42, 126, 255, 120),
        width=max(1, int(round(width * 0.014))),
    )
    draw.ellipse(
        (center_x - inner_r, center_y - inner_r, center_x + inner_r, center_y + inner_r),
        outline=(90, 230, 255, 110),
        width=max(1, int(round(width * 0.012))),
    )

    segment_radius = int(round(height * 0.33))
    segment_width = max(1, int(round(width * 0.013)))
    for angle in range(0, 360, 18):
        start = angle - 86
        sweep = 8
        color = (42, 210, 255, 105) if angle % 36 == 0 else (70, 125, 255, 82)
        draw.arc(
            (
                center_x - segment_radius,
                center_y - segment_radius,
                center_x + segment_radius,
                center_y + segment_radius,
            ),
            start=start,
            end=start + sweep,
            fill=color,
            width=segment_width,
        )

    core = new_layer(panel.size)
    cdraw = ImageDraw.Draw(core)
    cdraw.ellipse(
        (center_x - inner_r + 2, center_y - inner_r + 2, center_x + inner_r - 2, center_y + inner_r - 2),
        fill=(6, 12, 26, 160),
    )
    panel.alpha_composite(core.filter(ImageFilter.GaussianBlur(2)))
    panel.alpha_composite(ring)


def add_highlights(panel: Image.Image) -> None:
    width, height = panel.size

    streak = new_layer(panel.size)
    draw = ImageDraw.Draw(streak)
    draw.polygon(
        [
            (width * 0.05, height * 0.10),
            (width * 0.56, height * 0.10),
            (width * 0.48, height * 0.23),
            (width * 0.02, height * 0.23),
        ],
        fill=(255, 255, 255, 18),
    )
    panel.alpha_composite(streak.filter(ImageFilter.GaussianBlur(max(2, int(round(width * 0.018))))))

    side_glow = new_layer(panel.size)
    sdraw = ImageDraw.Draw(side_glow)
    sdraw.ellipse(
        (int(round(width * 0.84)), int(round(height * 0.62)), width + 8, height + 2),
        fill=(190, 255, 255, 70),
    )
    panel.alpha_composite(side_glow.filter(ImageFilter.GaussianBlur(max(3, int(round(width * 0.022))))))

    add_glow_stroke(
        panel,
        (1, 1, width - 2, height - 2),
        max(5, int(round(height * 0.24))),
        (34, 176, 255, 72),
        max(1, int(round(width * 0.012))),
        max(2, int(round(width * 0.018))),
    )


def build_asset(width: int, height: int) -> Image.Image:
    panel = build_base_panel((width, height))
    draw_hud_details(panel)
    draw_left_overlay(panel)
    draw_bottom_curves(panel)
    draw_right_ring_carrier(panel)
    add_highlights(panel)
    return panel


def main() -> None:
    for name, width, height in TARGETS:
        build_asset(width, height).save(ROOT / name)
        print(ROOT / name)


if __name__ == "__main__":
    main()
